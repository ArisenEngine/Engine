using Arisen.Native.RHI;
using ArisenEngine.Core.RHI;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEngine.Rendering;

public class ForwardRenderPipeline : RenderPipeline
{
    private RHICommandBufferPool? m_CommandPool;

    protected override void Render(RenderContext context, Camera[] cameras)
    {
        using var _ = Profiler.Zone("ForwardRenderPipeline.Render");

        var device = context.Device;
        var swapChain = context.SwapChain;
        uint frameIndex = context.FrameIndex;

        // 1. Ensure Command Pool exists
        if (m_CommandPool == null)
        {
            var factory = device.GetFactory();
            m_CommandPool = factory.CreateCommandBufferPool(RHIQueueType.Graphics);
        }

        // 2. Begin Frame (Acquire Image)
        var backBuffer = swapChain.BeginFrame(frameIndex);
        if (!backBuffer.IsValid) return;

        // 3. Record Commands
        var cmd = m_CommandPool.Value.GetCommandBuffer(frameIndex);
        cmd.Begin();

        // Transition backbuffer to color attachment
        cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_UNDEFINED,
            EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL);

        // TODO: Future: Add Clear and Draw logic here

        // Transition backbuffer to present
        cmd.TransitionImageLayout(backBuffer, EImageLayout.IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL,
            EImageLayout.IMAGE_LAYOUT_PRESENT_SRC_KHR);

        cmd.End();

        // 4. Submit and Present
        ulong ticket = device.Submit(cmd, waitSC: swapChain, signalSC: swapChain);
        device.WaitQueueTicket(ticket);

        swapChain.EndFrame(frameIndex);

        m_CommandPool.Value.ReleaseCommandBuffer(frameIndex, cmd.RHIHandle);
    }

    protected override void OnDisposed()
    {
        if (m_CommandPool != null)
        {
            // Note: RHI handles are usually managed, but pools might need explicit release if not GC'd properly
            m_CommandPool = null;
        }
    }
}
