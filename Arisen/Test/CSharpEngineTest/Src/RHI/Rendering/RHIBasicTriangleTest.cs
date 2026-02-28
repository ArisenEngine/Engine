using System;
using Arisen.Native.RHI;
using ArisenEngine.Core.Diagnostics;
using CSharpEngineTest.Framework;

namespace CSharpEngineTest.RHI.Rendering
{
    public class RHIBasicTriangleTest : RHIRenderingTestBase
    {
        public override string GetName() => "RHIBasicTriangleTest";

        protected override bool SetupTest()
        {
            if (!base.SetupTest()) return false;
            
            Logger.Log("Setting up RHIBasicTriangleTest");
            
            // Note: Full pipeline/renderpass setup would go here.
            // For now, we'll implement the frame loop leveraging the new wrappers.
            return true;
        }

        protected override void RenderFrame()
        {
            if (!_device.HasValue || !_swapChain.HasValue || !_cmdPool.HasValue) return;

            // 1. Begin Frame (Acquire Image)
            uint frameIndex = GetCurrentFrameIndex();
            var backBuffer = _swapChain.Value.BeginFrame(frameIndex);
            if (!backBuffer.IsValid) return;

            // 2. Record Commands
            var pool = _cmdPool.Value;
            var cmd = pool.GetCommandBuffer(frameIndex);

            cmd.Begin();
            
            // Transition backbuffer to color attachment
            cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_UNDEFINED, EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

            // TODO: cmd.BeginRenderPass(...);
            // TODO: cmd.BindPipeline(...);
            // TODO: cmd.Draw(3, 1, 0, 0);
            // TODO: cmd.EndRenderPass();

            // Transition backbuffer to present
            cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL, EImageLayout.IMAGE_LAYOUT_PRESENT_SRC_KHR);
            
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
            base.TeardownTest();
        }
    }
}
