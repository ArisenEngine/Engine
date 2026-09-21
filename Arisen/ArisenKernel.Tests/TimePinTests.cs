using ArisenKernel.Lifecycle;
using Xunit;

namespace ArisenKernel.Tests;

[Collection(KernelGlobalStateCollection.Name)]
public sealed class TimePinTests
{
    [Fact]
    public void PinnedClockIgnoresAdvancingWallClock()
    {
        try
        {
            Time.Unpin();
            Time.Update();
            double pinned = Time.elapsedTime;
            double wallStart = Time.totalTime;
            Assert.True(
                SpinWait.SpinUntil(
                    () => Time.totalTime - wallStart >= 0.002,
                    TimeSpan.FromSeconds(5)),
                "The wall clock did not advance while preparing the time-pin test.");

            Time.Pin(pinned);
            for (int index = 0; index < 4; index++)
            {
                Time.Update();
                Assert.True(Time.IsPinned);
                Assert.Equal(pinned, Time.elapsedTime);
                Assert.Equal(0.0f, Time.deltaTime);
            }

            Time.Unpin();
            Assert.False(Time.IsPinned);
            Time.Update();
            Assert.True(Time.elapsedTime >= pinned);
        }
        finally
        {
            Time.Unpin();
        }
    }

    [Fact]
    public void PinRejectsNonFiniteOrNegativeElapsedTime()
    {
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Time.Pin(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Time.Pin(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Time.Pin(-0.5));
            Assert.False(Time.IsPinned);
        }
        finally
        {
            Time.Unpin();
        }
    }
}
