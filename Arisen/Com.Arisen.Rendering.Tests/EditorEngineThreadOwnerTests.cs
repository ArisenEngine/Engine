using ArisenEditor.Core.Lifecycle;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class EditorEngineThreadOwnerTests
{
    [Fact]
    public async Task StopRetainsOwnershipUntilExplicitCleanupCompletes()
    {
        using var entered = new ManualResetEventSlim();
        using var cancellationObserved = new ManualResetEventSlim();
        using var allowCompletion = new ManualResetEventSlim();
        bool cleanupCompleted = false;
        var owner = new EditorEngineThreadOwner(
            token =>
            {
                entered.Set();
                token.WaitHandle.WaitOne();
                cancellationObserved.Set();
                allowCompletion.Wait();
                cleanupCompleted = true;
            },
            "EditorEngineThreadOwnerTests.Stop",
            ThreadPriority.Normal);

        Task? stopTask = null;
        try
        {
            owner.Start();
            WaitForSignal(entered, "The owned thread did not start.");
            stopTask = Task.Run(owner.Stop);
            WaitForSignal(cancellationObserved, "The owned thread did not observe cancellation.");

            Assert.True(owner.HasThreadOwnership);
            Assert.False(stopTask.IsCompleted);
            Assert.False(cleanupCompleted);
        }
        finally
        {
            allowCompletion.Set();
            if (stopTask != null)
            {
                await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            else
            {
                owner.Stop();
            }
        }

        Assert.True(cleanupCompleted);
        Assert.False(owner.HasThreadOwnership);
        Assert.False(owner.IsRunning);
    }

    [Fact]
    public async Task FaultedThreadRemainsOwnedUntilStopObservesFailure()
    {
        var injectedFailure = new InvalidOperationException("Injected Editor engine failure.");
        var owner = new EditorEngineThreadOwner(
            _ => throw injectedFailure,
            "EditorEngineThreadOwnerTests.Failure",
            ThreadPriority.Normal);
        owner.Start();

        InvalidOperationException completionFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await owner.Completion.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(injectedFailure, completionFailure);
        Assert.True(owner.HasThreadOwnership);
        Assert.False(owner.IsRunning);
        InvalidOperationException stopFailure = Assert.Throws<InvalidOperationException>(owner.Stop);
        Assert.Contains("engine-thread completion", stopFailure.Message, StringComparison.Ordinal);
        Assert.Same(injectedFailure, stopFailure.InnerException);
        Assert.False(owner.HasThreadOwnership);
    }

    [Fact]
    public async Task CompletedThreadMustBeReleasedBeforeRestart()
    {
        int runCount = 0;
        var owner = new EditorEngineThreadOwner(
            _ => Interlocked.Increment(ref runCount),
            "EditorEngineThreadOwnerTests.Restart",
            ThreadPriority.Normal);

        owner.Start();
        await owner.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(owner.HasThreadOwnership);
        Assert.Throws<InvalidOperationException>(owner.Start);

        owner.Stop();
        owner.Start();
        await owner.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        owner.Stop();

        Assert.Equal(2, runCount);
        Assert.False(owner.HasThreadOwnership);
    }

    [Fact]
    public void RunnerSourceUsesCompletionSignalWithoutTimedJoin()
    {
        string ownerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Lifecycle/EditorEngineThreadOwner.cs");
        string runnerSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/Core/Lifecycle/EditorEngineRunner.cs");
        string appSource = ReadRepoFile(
            "Arisen/Development/PackageGame/Local/com.arisen.editor/Managed/App.axaml.cs");

        Assert.Contains("state.Completion.Task.GetAwaiter().GetResult();", ownerSource, StringComparison.Ordinal);
        Assert.Contains("state.Thread.Join();", ownerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Join(5000)", ownerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Join(5000)", runnerSource, StringComparison.Ordinal);
        Assert.Contains("public Task Completion", runnerSource, StringComparison.Ordinal);
        Assert.Contains("ObserveEditorEngineCompletionAsync", appSource, StringComparison.Ordinal);
        Assert.Contains("exitArgs.ApplicationExitCode = 1;", appSource, StringComparison.Ordinal);
    }

    private static void WaitForSignal(ManualResetEventSlim signal, string message)
    {
        Assert.True(signal.Wait(TimeSpan.FromSeconds(5)), message);
    }

    private static string ReadRepoFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Arisen")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
