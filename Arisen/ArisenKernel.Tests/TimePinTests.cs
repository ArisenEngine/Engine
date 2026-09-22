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

    [Fact]
    public void ParkedIntervalIsNotChargedToTheFrameThatResumes()
    {
        try
        {
            Time.Unpin();
            Time.Update();
            double elapsedBeforePark = Time.elapsedTime;
            double wallBeforePark = Time.totalTime;

            // A parked frame loop runs no frames while the wall clock keeps moving; the runtime
            // parks it while its window cannot composite presented frames.
            Assert.True(
                SpinWait.SpinUntil(
                    () => Time.totalTime - wallBeforePark >= 0.25,
                    TimeSpan.FromSeconds(10)),
                "The wall clock did not advance while preparing the frame-clock resync test.");

            Time.ResyncFrameClock();
            Time.Update();

            double wallSincePark = Time.totalTime - wallBeforePark;
            float resumedDelta = Time.deltaTime;

            // The wall clock still reports the whole park, and the frame that resumes owns only
            // the time since the clock was re-based - not the parked interval.
            Assert.True(wallSincePark >= 0.25, $"The wall clock advanced only {wallSincePark:F3}s.");
            Assert.True(
                resumedDelta < 0.1f,
                $"The resumed frame was charged {resumedDelta:F3}s of parked time.");
            Assert.Equal(
                resumedDelta,
                (float)(Time.elapsedTime - elapsedBeforePark),
                precision: 6);
        }
        finally
        {
            Time.Unpin();
        }
    }
}
