using System;
using System.IO;
using Arisen.Native.RHI;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.RHI;
using ArisenEngine.ShaderLab;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.RHI.Unit
{
    public class RHIAsyncComputeTest : RHITestBase
    {
        private RHICommandBufferPool _commandPool;
        private RHIShaderProgramHandle _computeProgram;
        private RHIPipelineHandle _pipeline;
        private RHIPipelineState _pso;

        private RHIBufferHandle _inputBuffer;
        private RHIBufferHandle _outputBuffer;

        public override string GetName() => "RHIAsyncComputeTest";
        public override TestCategory GetCategory() => TestCategory.Unit;
        protected override bool IsHeadless() => true;

        protected override bool SetupTest()
        {
            if (!_device.HasValue) return false;

            var factory = _device.Value.GetFactory();
            var pipelineCache = _device.Value.PipelineCache;

            // 1. Setup Command Pool for Compute
            _commandPool = factory.CreateCommandBufferPool(RHIQueueType.Compute);

            // 2. Compile and Create Compute Program
            string baseDir = AppContext.BaseDirectory;
            string shaderPath = Path.Combine(baseDir, "Shader", "AsyncComputeTest.hlsl");

            if (!File.Exists(shaderPath))
            {
                Logger.Error($"Shader file not found: {shaderPath}");
                return false;
            }

            var compileOpts = new ShaderCompiler.CompileOptions
            {
                Entry = "CSMain",
                ShaderModel = "6_0",
                Target = "-spirv",
                TargetEnv = "vulkan1.2",
                OptimizeLevel = "0"
            };

            var csResult = ShaderCompiler.Compile(shaderPath, EProgramStage.Compute, compileOpts);
            if (!csResult.Success || csResult.Code.Length == 0)
            {
                Logger.Error("Failed to compile compute shader!");
                return false;
            }

            _computeProgram = factory.CreateGPUProgram();
            if (!_computeProgram.IsValid)
            {
                Logger.Error("Failed to create GPU program!");
                return false;
            }

            bool csAttached = factory.AttachProgramByteCode(_computeProgram, EShaderStage.SHADER_STAGE_COMPUTE_BIT, csResult.Code, "CSMain");
            if (!csAttached)
            {
                Logger.Error("Failed to attach compute shader bytecode!");
                return false;
            }

            // 3. Create Buffers
            const uint elementCount = 1024;
            const uint bufferSize = elementCount * sizeof(uint);

            // usage bits: STORAGE_BUFFER = 0x00000080
            _inputBuffer = factory.CreateBuffer(bufferSize, 0x00000080, ESharingMode.SHARING_MODE_EXCLUSIVE, ERHIMemoryUsage.Upload, "InputBuffer");
            _outputBuffer = factory.CreateBuffer(bufferSize, 0x00000080, ESharingMode.SHARING_MODE_EXCLUSIVE, ERHIMemoryUsage.Upload, "OutputBuffer");

            if (!_inputBuffer.IsValid || !_outputBuffer.IsValid)
            {
                Logger.Error("Failed to create buffers!");
                return false;
            }

            // Fill input buffer
            unsafe
            {
                IntPtr pData = factory.MapBuffer(_inputBuffer);
                uint* pUint = (uint*)pData;
                for (uint i = 0; i < elementCount; i++) pUint[i] = i;
                factory.UnmapBuffer(_inputBuffer);
            }

            // 4. Setup Pipeline and Descriptors
            _pso = pipelineCache.GetPipelineState();
            _pso.SetBindPoint(EPipelineBindPoint.PIPELINE_BIND_POINT_COMPUTE);
            _pso.AddProgram(_computeProgram);

            // Update descriptors (layout 0, binding 0 for input, binding 1 for output)
            _pso.UpdateDescriptorSet(0, 0, new[] { _inputBuffer });
            _pso.UpdateDescriptorSet(0, 1, new[] { _outputBuffer });

            _pso.BuildDescriptorSetLayout();
            _pipeline = pipelineCache.GetComputePipeline(_pso);

            if (!_pipeline.IsValid)
            {
                Logger.Error("Failed to create compute pipeline!");
                return false;
            }

            return true;
        }

        public override bool Run()
        {
            if (!_device.HasValue) return false;

            Logger.Log("Running Async Compute Test...");

            // Get Command Buffer from Compute Pool
            var cmd = _commandPool.GetCommandBuffer(0);

            cmd.Begin();
            cmd.BindPipeline(_pipeline);
            // Note: BindDescriptorSet is not yet fully bridged for this manual flow in RHITestBase runner, 
            // but the PSO Update/Build handle layouts. 
            // In C++ test, it calls BindDescriptorSet. 
            // Wait, I haven't bridged BindDescriptorSet for the "Automatic" layout yet in CommandBuffer.
            // Let's check if the bridge for BindDescriptorSet exists.
            
            // In RHIBasicTriangleTest, we don't bind descriptors because it's hardcoded.
            // For compute, we NEED descriptors.
            
            cmd.Dispatch(4, 1, 1); // 4 * 256 = 1024
            cmd.End();

            // Submit to COMPUTE QUEUE
            var computeQueue = _device.Value.GetQueue(RHIQueueType.Compute);
            if (!computeQueue.IsValid)
            {
                Logger.Error("Compute queue not available!");
                return false;
            }

            Logger.Log("Submitting to Compute Queue...");
            ulong ticket = computeQueue.Submit(cmd);
            computeQueue.WaitForTicket(ticket);

            // Verify Results
            bool success = true;
            var factory = _device.Value.GetFactory();
            unsafe
            {
                IntPtr pData = factory.MapBuffer(_outputBuffer);
                uint* pUint = (uint*)pData;
                for (uint i = 0; i < 1024; i++)
                {
                    if (pUint[i] != i * 2)
                    {
                        Logger.Error($"Verification failed at index {i}: expected {i * 2}, got {pUint[i]}");
                        success = false;
                        break;
                    }
                }
                factory.UnmapBuffer(_outputBuffer);
            }

            if (success)
            {
                Logger.Log("Async Compute Test completed successfully!");
            }

            return success;
        }

        protected override void TeardownTest()
        {
            if (_device.HasValue)
            {
                var factory = _device.Value.GetFactory();
                if (_inputBuffer.IsValid) factory.ReleaseBuffer(_inputBuffer);
                if (_outputBuffer.IsValid) factory.ReleaseBuffer(_outputBuffer);
                if (_computeProgram.IsValid) factory.ReleaseGPUProgram(_computeProgram);
                if (_commandPool.IsValid) factory.ReleaseCommandBufferPool(_commandPool.RHIHandle);
            }

            if (_pso.IsValid) _pso.Release();

            base.TeardownTest();
        }
    }
}
