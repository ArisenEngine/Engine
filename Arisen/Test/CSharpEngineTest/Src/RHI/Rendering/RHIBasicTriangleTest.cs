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
            if (_device == null || _swapChain == null || !_cmdPool.IsValid) return;

            // 1. Begin Frame (Acquire Image)
            uint frameIndex = GetCurrentFrameIndex();
            var backBuffer = _swapChain.BeginFrame(frameIndex);
            if (!backBuffer.IsValid) return;

            // 2. Record Commands
            var pool = _device.GetCommandBufferPool(_cmdPool);
            var cmdHandle = pool.GetCommandBuffer(frameIndex);
            var cmd = _device.GetCommandBuffer(cmdHandle);

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
            ulong ticket = _device.Submit(cmd, waitSC: _swapChain, signalSC: _swapChain);
            _device.WaitQueueTicket(ticket);
            
            _swapChain.EndFrame(frameIndex);

            pool.ReleaseCommandBuffer(frameIndex, cmdHandle);
        }

        protected override void TeardownTest()
        {
            Logger.Log("Tearing down RHIBasicTriangleTest");
            base.TeardownTest();
        }
    }
}
