using Arisen.Testing;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class DeterministicFaultInjectorTests
{
    [Fact]
    public void ArmedStageThrowsExactlyOnceAndRecordsTrigger()
    {
        var injector = new DeterministicFaultInjector<TestFaultStage>();
        injector.Arm(
            TestFaultStage.PackageLoad,
            () => new TestFaultException("package load failed"));

        TestFaultException failure = Assert.Throws<TestFaultException>(() =>
            injector.ThrowIfArmed(TestFaultStage.PackageLoad));
        injector.ThrowIfArmed(TestFaultStage.PackageLoad);

        Assert.Equal("package load failed", failure.Message);
        DeterministicFaultSnapshot<TestFaultStage> snapshot = injector.Snapshot();
        Assert.Empty(snapshot.PendingStages);
        Assert.Equal([TestFaultStage.PackageLoad], snapshot.TriggeredStages);
        injector.EnsureFullyConsumed();
    }

    [Fact]
    public void ConcurrentTriggersConsumeOneArmedStageExactlyOnce()
    {
        var injector = new DeterministicFaultInjector<TestFaultStage>();
        injector.Arm(
            TestFaultStage.TaskDrain,
            () => new TestFaultException("task drain failed"));
        int failureCount = 0;

        Parallel.For(0, 128, _ =>
        {
            try
            {
                injector.ThrowIfArmed(TestFaultStage.TaskDrain);
            }
            catch (TestFaultException)
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        Assert.Equal(1, failureCount);
        Assert.Equal([TestFaultStage.TaskDrain], injector.Snapshot().TriggeredStages);
        injector.EnsureFullyConsumed();
    }

    [Fact]
    public void PendingStageFailsConsumptionCheckAndCannotBeOverwritten()
    {
        var injector = new DeterministicFaultInjector<TestFaultStage>();
        injector.Arm(
            TestFaultStage.AssetPublication,
            () => new TestFaultException("asset publication failed"));

        InvalidOperationException duplicate = Assert.Throws<InvalidOperationException>(() =>
            injector.Arm(
                TestFaultStage.AssetPublication,
                () => new TestFaultException("replacement")));
        InvalidOperationException pending = Assert.Throws<InvalidOperationException>(
            injector.EnsureFullyConsumed);

        Assert.Contains("already pending", duplicate.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestFaultStage.AssetPublication), pending.Message, StringComparison.Ordinal);
        Assert.Equal([TestFaultStage.AssetPublication], injector.Snapshot().PendingStages);
    }

    [Fact]
    public void ResetAllowsAStageToBeArmedForANewFixtureCycle()
    {
        var injector = new DeterministicFaultInjector<TestFaultStage>();
        injector.Arm(
            TestFaultStage.RhiSubmit,
            () => new TestFaultException("first cycle"));
        Assert.Throws<TestFaultException>(() =>
            injector.ThrowIfArmed(TestFaultStage.RhiSubmit));

        injector.Reset();
        injector.Arm(
            TestFaultStage.RhiSubmit,
            () => new TestFaultException("second cycle"));
        TestFaultException failure = Assert.Throws<TestFaultException>(() =>
            injector.ThrowIfArmed(TestFaultStage.RhiSubmit));

        Assert.Equal("second cycle", failure.Message);
        injector.EnsureFullyConsumed();
    }

    private enum TestFaultStage
    {
        PackageLoad,
        AssetPublication,
        TaskDrain,
        RhiSubmit
    }

    private sealed class TestFaultException(string message) : Exception(message);
}
