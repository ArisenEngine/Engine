using Xunit;
using ArisenEditorFramework.Lifecycle;

namespace EditorTest;

public class BootstrapperTests
{
    private class TestStep : IBootStep
    {
        public string Name { get; set; } = "Test Step";
        public string Description { get; set; } = "A test step";
        public bool WasExecuted { get; private set; }
        public bool ShouldFail { get; set; }
        public string FailMessage { get; set; } = "Step failed";
        
        public Task ExecuteAsync(BootContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasExecuted = true;
            if (ShouldFail)
            {
                context.Success = false;
                context.ErrorMessage = FailMessage;
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RunAsync_WithNoSteps_ReturnsSuccessContext()
    {
        var bootstrapper = new Bootstrapper();
        var context = await bootstrapper.RunAsync("test.arisenproj");
        Assert.True(context.Success);
    }

    [Fact]
    public async Task RunAsync_ExecutesAllSteps()
    {
        var bootstrapper = new Bootstrapper();
        var step1 = new TestStep { Name = "Step 1" };
        var step2 = new TestStep { Name = "Step 2" };
        bootstrapper.AddStep(step1);
        bootstrapper.AddStep(step2);

        await bootstrapper.RunAsync("test.arisenproj");

        Assert.True(step1.WasExecuted);
        Assert.True(step2.WasExecuted);
    }

    [Fact]
    public async Task RunAsync_StopsOnFailure()
    {
        var bootstrapper = new Bootstrapper();
        var step1 = new TestStep { Name = "Fail Step", ShouldFail = true, FailMessage = "boom" };
        var step2 = new TestStep { Name = "Never Run" };
        bootstrapper.AddStep(step1);
        bootstrapper.AddStep(step2);

        var context = await bootstrapper.RunAsync("test.arisenproj");

        Assert.False(context.Success);
        Assert.Equal("boom", context.ErrorMessage);
        Assert.True(step1.WasExecuted);
        Assert.False(step2.WasExecuted);
    }

    [Fact]
    public async Task RunAsync_SupportsCancellation()
    {
        var bootstrapper = new Bootstrapper();
        var step = new TestStep();
        bootstrapper.AddStep(step);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = await bootstrapper.RunAsync("test.arisenproj", cts.Token);

        Assert.False(context.Success);
        Assert.Contains("cancelled", context.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(step.WasExecuted);
    }

    [Fact]
    public async Task RunAsync_ReportsProgress()
    {
        var bootstrapper = new Bootstrapper();
        bootstrapper.AddStep(new TestStep { Name = "Step A", Description = "Desc A" });
        bootstrapper.AddStep(new TestStep { Name = "Step B", Description = "Desc B" });

        var progressEvents = new List<(string name, string desc, double progress)>();
        bootstrapper.ProgressChanged += (name, desc, progress) => progressEvents.Add((name, desc, progress));

        await bootstrapper.RunAsync("test.arisenproj");

        Assert.True(progressEvents.Count >= 2);
        Assert.Equal("Step A", progressEvents[0].name);
        Assert.Equal("Completed", progressEvents.Last().name);
    }

    [Fact]
    public async Task RunAsync_SetsProjectPath()
    {
        var bootstrapper = new Bootstrapper();
        var step = new TestStep();
        bootstrapper.AddStep(step);

        var context = await bootstrapper.RunAsync("my/project.arisenproj");

        Assert.Equal("my/project.arisenproj", context.ProjectPath);
    }
}
