using System;
using System.IO;
using Arisen.Native.RHI;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.RHI;
using ArisenEngine.ShaderLab;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.RHI.Rendering
{
    public class RHIBasicTriangleTest : RHIRenderingTestBase
    {
        private RHIPipelineHandle _pipeline;
        private RHIPipelineState _pso;
        private RHIShaderProgramHandle _vsProgram;
        private RHIShaderProgramHandle _psProgram;
        private bool _pipelineReady;

        public override string GetName() => "RHIBasicTriangleTest";

        protected override bool SetupTest()
        {
            if (!base.SetupTest()) return false;

            Logger.Log("Setting up RHIBasicTriangleTest");

            if (!_device.HasValue) return false;

            var factory = _device.Value.GetFactory();
            var pipelineCache = _device.Value.PipelineCache;

            // --- Compile Shaders ---
            string baseDir = AppContext.BaseDirectory;
            string shaderPath = Path.Combine(baseDir, "Shader", "TriangleShader.hlsl");

            if (!File.Exists(shaderPath))
            {
                Logger.Error($"Shader file not found: {shaderPath}");
                return false;
            }

            Logger.Log($"Compiling vertex shader from: {shaderPath}");
            var compileOpts = new ShaderCompiler.CompileOptions
            {
                Entry = "MainVS",
                ShaderModel = "6_4",
                Target = "-spirv",
                TargetEnv = "vulkan1.2",
                OptimizeLevel = "0"
            };
            ShaderCompiler.CompileResult vsResult;
            try
            {
                vsResult = ShaderCompiler.Compile(shaderPath, EProgramStage.Vertex, compileOpts);
            }
            catch (Exception ex)
            {
                Logger.Error($"Vertex shader compilation threw exception: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            if (!vsResult.Success || vsResult.Code.Length == 0)
            {
                Logger.Error($"Failed to compile vertex shader! Success={vsResult.Success}, CodeLen={vsResult.Code.Length}, Msg={vsResult.Message}, OutputPath={vsResult.OutputPath}");
                return false;
            }
            Logger.Log($"Vertex shader compiled: {vsResult.Code.Length} bytes");

            Logger.Log("Compiling fragment shader...");
            compileOpts.Entry = "MainPS";
            var psResult = ShaderCompiler.Compile(shaderPath, EProgramStage.Fragment, compileOpts);
            if (!psResult.Success || psResult.Code.Length == 0)
            {
                Logger.Error("Failed to compile fragment shader!");
                return false;
            }
            Logger.Log($"Fragment shader compiled: {psResult.Code.Length} bytes");

            // --- Create GPU Programs ---
            _vsProgram = factory.CreateGPUProgram();
            _psProgram = factory.CreateGPUProgram();

            if (!_vsProgram.IsValid || !_psProgram.IsValid)
            {
                Logger.Error("Failed to create GPU programs!");
                return false;
            }

            // Attach SPIR-V bytecodes
            bool vsAttached = factory.AttachProgramByteCode(_vsProgram, EShaderStage.SHADER_STAGE_VERTEX_BIT, vsResult.Code, "MainVS");
            bool psAttached = factory.AttachProgramByteCode(_psProgram, EShaderStage.SHADER_STAGE_FRAGMENT_BIT, psResult.Code, "MainPS");

            if (!vsAttached || !psAttached)
            {
                Logger.Error($"Failed to attach shader bytecodes! VS={vsAttached}, PS={psAttached}");
                return false;
            }
            Logger.Log("Shader programs created and bytecodes attached.");

            // --- Create Pipeline State ---
            _pso = pipelineCache.GetPipelineState();
            if (!_pso.IsValid)
            {
                Logger.Error("Failed to create pipeline state!");
                return false;
            }

            _pso.AddProgram(_vsProgram);
            _pso.AddProgram(_psProgram);
            _pso.SetBindPoint(EPipelineBindPoint.PIPELINE_BIND_POINT_GRAPHICS);
            _pso.SetInputAssemblyState(EPrimitiveTopology.PRIMITIVE_TOPOLOGY_TRIANGLE_LIST);
            _pso.SetRasterizationState(
                EPolygonMode.EPOLYGON_MODE_FILL,
                ECullModeFlagBits.CULL_MODE_BACK_BIT,
                EFrontFace.FRONT_FACE_CLOCKWISE);

            // Use actual swapchain format to avoid validation errors (UNORM vs SRGB)
            var swapChainFormat = factory.GetImageViewFormat(_swapChain.Value.GetImageView(0));
            _pso.SetRenderingFormats(new[] { swapChainFormat }, EFormat.FORMAT_UNDEFINED);
            Logger.Log($"Pipeline rendering format set to: {swapChainFormat}");

            // Color blend — disable blending, just write through
            _pso.SetColorBlendState(blendEnable: false);

            // Dynamic viewport/scissor — set via cmd.SetViewport/SetScissor per frame
            _pso.SetDynamicStateMask((1UL << (int)EDynamicPipelineState.DYNAMIC_STATE_VIEWPORT) |
                                     (1UL << (int)EDynamicPipelineState.DYNAMIC_STATE_SCISSOR));

            // Get compiled pipeline
            _pipeline = pipelineCache.GetGraphicsPipeline(_pso);
            if (!_pipeline.IsValid)
            {
                Logger.Error("Failed to create graphics pipeline!");
                return false;
            }
            Logger.Log("Graphics pipeline created successfully.");

            _pipelineReady = true;
            return true;
        }

        protected override void RenderFrame()
        {
            if (!_device.HasValue || !_swapChain.HasValue || !_cmdPool.HasValue) return;
            if (!_pipelineReady) return;

            // 1. Begin Frame (Acquire Image)
            uint frameIndex = GetCurrentFrameIndex();
            var backBuffer = _swapChain.Value.BeginFrame(frameIndex);
            if (!backBuffer.IsValid) return;

            // Get the image view for the current swapchain image
            var imageView = _swapChain.Value.GetImageView(frameIndex);

            // 2. Record Commands
            var pool = _cmdPool.Value;
            var cmd = pool.GetCommandBuffer(frameIndex);

            cmd.Begin();

            // Transition backbuffer to color attachment
            cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_UNDEFINED,
                EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

            // Begin dynamic rendering with clear to dark blue
            cmd.BeginRendering(imageView,
                EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                EAttachmentLoadOp.ATTACHMENT_LOAD_OP_CLEAR,
                EAttachmentStoreOp.ATTACHMENT_STORE_OP_STORE,
                0.0f, 0.0f, 0.2f, 1.0f,  // dark blue clear color
                0, 0, 1200, 800);          // render area

            cmd.BindPipeline(_pipeline);
            cmd.SetViewport(0, 0, 1200, 800);
            cmd.SetScissor(0, 0, 1200, 800);

            // Draw triangle — 3 vertices hardcoded in shader via SV_VertexID
            cmd.Draw(3, 1, 0, 0);

            cmd.EndRendering();

            // Transition backbuffer to present
            cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
                EImageLayout.IMAGE_LAYOUT_PRESENT_SRC_KHR);

            cmd.End();

            // 3. Submit and Present
            ulong ticket = _device.Value.Submit(cmd, waitSC: _swapChain.Value, signalSC: _swapChain.Value);
            _device.Value.WaitQueueTicket(ticket);

            _swapChain.Value.EndFrame(frameIndex);

            pool.ReleaseCommandBuffer(frameIndex, cmd.RHIHandle);
        }

        protected override void TeardownTest()
        {
            Logger.Log("Tearing down RHIBasicTriangleTest");

            if (_device.HasValue)
            {
                var factory = _device.Value.GetFactory();
                if (_vsProgram.IsValid) factory.ReleaseGPUProgram(_vsProgram);
                if (_psProgram.IsValid) factory.ReleaseGPUProgram(_psProgram);
            }

            if (_pso.IsValid)
            {
                _pso.Release();
            }

            ShaderCompiler.ReleaseDXC();

            base.TeardownTest();
        }
    }
}