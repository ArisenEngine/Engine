using System;

namespace ArisenKernel.Contracts;

public enum RenderOutputPresentationSkipReason
{
    None,
    WarmingUp,
    DuplicateTicket,
    MissingSharedHandle,
    MissingMemory,
    InvalidSize,
    MissingSemaphore
}

public readonly struct RenderOutputPresentationDecision
{
    public RenderOutputPresentationDecision(
        bool shouldPresent,
        bool shouldReleaseSignalSemaphore,
        RenderOutputPresentationSkipReason skipReason)
    {
        ShouldPresent = shouldPresent;
        ShouldReleaseSignalSemaphore = shouldReleaseSignalSemaphore;
        SkipReason = skipReason;
    }

    public bool ShouldPresent { get; }
    public bool ShouldReleaseSignalSemaphore { get; }
    public RenderOutputPresentationSkipReason SkipReason { get; }
}

public struct RenderOutputPresentationState
{
    public ulong LastPresentedTicket { get; private set; }
    public uint LastImportedWidth { get; private set; }
    public uint LastImportedHeight { get; private set; }
    public uint LastImportedResizeGeneration { get; private set; }

    public RenderOutputPresentationDecision Evaluate(in RenderOutputInfo info, bool requiresSemaphores)
    {
        var reason = GetSkipReason(info, requiresSemaphores);
        return new RenderOutputPresentationDecision(
            reason == RenderOutputPresentationSkipReason.None,
            reason != RenderOutputPresentationSkipReason.None && info.SignalSemaphoreHandle != IntPtr.Zero,
            reason);
    }

    public bool ShouldResetImportedImageCache(in RenderOutputInfo info)
    {
        return info.ResizeGeneration != LastImportedResizeGeneration ||
            info.Width != LastImportedWidth ||
            info.Height != LastImportedHeight;
    }

    public void MarkImportedImageCacheCurrent(in RenderOutputInfo info)
    {
        LastImportedResizeGeneration = info.ResizeGeneration;
        LastImportedWidth = info.Width;
        LastImportedHeight = info.Height;
    }

    public void MarkPresented(in RenderOutputInfo info)
    {
        LastPresentedTicket = info.Ticket;
    }

    public void Reset()
    {
        LastPresentedTicket = 0;
        LastImportedWidth = 0;
        LastImportedHeight = 0;
        LastImportedResizeGeneration = 0;
    }

    private RenderOutputPresentationSkipReason GetSkipReason(in RenderOutputInfo info, bool requiresSemaphores)
    {
        if (info.Ticket == 0)
        {
            return RenderOutputPresentationSkipReason.WarmingUp;
        }

        if (info.Ticket == LastPresentedTicket)
        {
            return RenderOutputPresentationSkipReason.DuplicateTicket;
        }

        if (info.SharedHandle == IntPtr.Zero)
        {
            return RenderOutputPresentationSkipReason.MissingSharedHandle;
        }

        if (info.MemorySize == 0)
        {
            return RenderOutputPresentationSkipReason.MissingMemory;
        }

        if (info.Width == 0 || info.Height == 0)
        {
            return RenderOutputPresentationSkipReason.InvalidSize;
        }

        if (requiresSemaphores &&
            (info.WaitSemaphoreHandle == IntPtr.Zero || info.SignalSemaphoreHandle == IntPtr.Zero))
        {
            return RenderOutputPresentationSkipReason.MissingSemaphore;
        }

        return RenderOutputPresentationSkipReason.None;
    }
}
