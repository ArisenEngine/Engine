using ArisenEngine.Vegetation.GenericRenderPipeline;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class VegetationPassCleanupJournalTests
{
    [Fact]
    public void RetryResumesFailedLegWithoutRepeatingCompletedOwnership()
    {
        var journal = new VegetationPassCleanupJournal();
        journal.BeginOwnership();
        int[] attempts = new int[3];

        void Release()
        {
            journal.Release(0, () => attempts[0]++);
            journal.Release(1, () =>
            {
                attempts[1]++;
                if (attempts[1] == 1)
                {
                    throw new InvalidOperationException("deterministic release failure");
                }
            });
            journal.Release(2, () => attempts[2]++);
        }

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(Release);
        Assert.Equal("deterministic release failure", failure.Message);
        Assert.Equal([1, 1, 0], attempts);

        Release();

        Assert.Equal([1, 2, 1], attempts);
    }

    [Fact]
    public void NewOwnershipCycleRearmsEveryCleanupLeg()
    {
        var journal = new VegetationPassCleanupJournal();
        int firstLegReleases = 0;
        int secondLegReleases = 0;

        void Release()
        {
            journal.Release(0, () => firstLegReleases++);
            journal.Release(1, () => secondLegReleases++);
        }

        journal.BeginOwnership();
        Release();
        Release();
        Assert.Equal(1, firstLegReleases);
        Assert.Equal(1, secondLegReleases);

        journal.BeginOwnership();
        Release();
        Assert.Equal(2, firstLegReleases);
        Assert.Equal(2, secondLegReleases);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void InvalidCleanupLegIsRejectedBeforeRelease(int legIndex)
    {
        var journal = new VegetationPassCleanupJournal();
        bool released = false;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            journal.Release(legIndex, () => released = true));
        Assert.False(released);
    }
}
