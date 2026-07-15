namespace ArisenKernel.Contracts;

public struct RenderOutputFramePacingState
{
    public bool HasSubmittedFrame { get; private set; }
    public bool HasConsumedFrame { get; private set; }
    public uint LastSubmittedFrameIndex { get; private set; }
    public uint LastConsumedFrameIndex { get; private set; }

    public bool CanSubmit(uint frameIndex, uint maxOutstandingFrames)
    {
        if (maxOutstandingFrames == 0)
        {
            return false;
        }

        if (!HasSubmittedFrame)
        {
            return true;
        }

        if (!HasConsumedFrame)
        {
            return unchecked(frameIndex - LastSubmittedFrameIndex) < maxOutstandingFrames;
        }

        return GetOutstandingFrameCount(frameIndex) < maxOutstandingFrames;
    }

    public uint GetOutstandingFrameCount(uint frameIndex)
    {
        return !HasConsumedFrame ? 0 : unchecked(frameIndex - LastConsumedFrameIndex);
    }

    public void MarkSubmitted(uint frameIndex)
    {
        if (!HasSubmittedFrame || IsNewerOrSame(frameIndex, LastSubmittedFrameIndex))
        {
            LastSubmittedFrameIndex = frameIndex;
            HasSubmittedFrame = true;
        }
    }

    public void MarkConsumed(uint frameIndex)
    {
        if (!HasConsumedFrame || IsNewerOrSame(frameIndex, LastConsumedFrameIndex))
        {
            LastConsumedFrameIndex = frameIndex;
            HasConsumedFrame = true;
        }
    }

    public void Reset()
    {
        HasSubmittedFrame = false;
        HasConsumedFrame = false;
        LastSubmittedFrameIndex = 0;
        LastConsumedFrameIndex = 0;
    }

    private static bool IsNewerOrSame(uint candidate, uint current)
    {
        return candidate == current || unchecked(candidate - current) < 0x80000000u;
    }
}
