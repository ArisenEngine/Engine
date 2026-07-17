namespace ArisenKernel.Contracts;

public struct RenderOutputFramePacingState
{
    public bool HasSubmittedFrame { get; private set; }
    public bool HasConsumedFrame { get; private set; }
    public uint LastSubmittedFrameIndex { get; private set; }
    public uint LastConsumedFrameIndex { get; private set; }
    public uint OutstandingFrameCount { get; private set; }

    public bool CanSubmit(uint maxOutstandingFrames)
    {
        if (maxOutstandingFrames == 0)
        {
            return false;
        }

        var submissionBudget = HasConsumedFrame && maxOutstandingFrames > 1
            ? maxOutstandingFrames - 1
            : maxOutstandingFrames;
        return OutstandingFrameCount < submissionBudget;
    }

    public void MarkSubmitted(uint frameIndex)
    {
        if (HasSubmittedFrame && !IsNewer(frameIndex, LastSubmittedFrameIndex))
        {
            return;
        }

        LastSubmittedFrameIndex = frameIndex;
        HasSubmittedFrame = true;
        OutstandingFrameCount++;
    }

    public void MarkConsumed(uint frameIndex)
    {
        if (!HasConsumedFrame || IsNewerOrSame(frameIndex, LastConsumedFrameIndex))
        {
            LastConsumedFrameIndex = frameIndex;
            HasConsumedFrame = true;

            if (!HasSubmittedFrame || IsNewerOrSame(frameIndex, LastSubmittedFrameIndex))
            {
                OutstandingFrameCount = 0;
            }
            else
            {
                OutstandingFrameCount = System.Math.Min(
                    OutstandingFrameCount,
                    unchecked(LastSubmittedFrameIndex - frameIndex));
            }
        }
    }

    public void Reset()
    {
        HasSubmittedFrame = false;
        HasConsumedFrame = false;
        LastSubmittedFrameIndex = 0;
        LastConsumedFrameIndex = 0;
        OutstandingFrameCount = 0;
    }

    private static bool IsNewer(uint candidate, uint current)
    {
        return candidate != current && unchecked(candidate - current) < 0x80000000u;
    }

    private static bool IsNewerOrSame(uint candidate, uint current)
    {
        return candidate == current || unchecked(candidate - current) < 0x80000000u;
    }
}
