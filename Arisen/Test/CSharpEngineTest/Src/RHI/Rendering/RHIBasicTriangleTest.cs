using System;
using Arisen.Native.RHI;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.RHI.Rendering
{
    public class RHIBasicTriangleTest : RHIRenderingTestBase
    {
        public override string GetName() => "BasicTriangleTest";

        protected override bool SetupTest()
        {
            base.SetupTest();
            Logger.Log("Setting up RHIBasicTriangleTest");
            return true;
        }

        protected override void RenderFrame()
        {
            if (_device == null || !_cmdPool.IsValid) return;

            // Simple rendering flow: Allocate Command Buffer, Begin, End, Submit
            var pool = _device.GetCommandBufferPool(_cmdPool);
            var cmdHandle = pool.GetCommandBuffer(GetCurrentFrameIndex());
            var cmd = _device.GetCommandBuffer(cmdHandle);

            cmd.Begin();
            // Since we don't have a full Swapchain / RenderPass setup, we just submit an empty command buffer
            // to verify RHI command submission pipeline is working.
            cmd.End();

            ulong ticket = _device.Submit(cmd);
            _device.WaitQueueTicket(ticket);

            pool.ReleaseCommandBuffer(GetCurrentFrameIndex(), cmdHandle);
            
            // Console.WriteLine($"Rendering frame {_frameIndex} successful");
        }

        protected override void TeardownTest()
        {
            Logger.Log("Tearing down RHIBasicTriangleTest");
            base.TeardownTest();
        }
    }
}
