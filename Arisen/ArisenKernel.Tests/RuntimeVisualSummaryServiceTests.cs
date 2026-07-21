using ArisenKernel.Lifecycle;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class RuntimeVisualSummaryServiceTests
{
    [Fact]
    public void NamedCapturesCompleteIndependentlyAndDeriveStablePaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "ArisenVisualSummaryTests");
        string output = Path.Combine(root, "visual.json");
        var service = new RuntimeVisualSummaryService(root, "Development", output);

        Assert.True(service.TryScheduleCapture("before", 3, out string beforePath));
        Assert.True(service.TryScheduleCapture("after", 9, out string afterPath));
        Assert.EndsWith("visual.before.json", beforePath, StringComparison.Ordinal);
        Assert.EndsWith("visual.after.json", afterPath, StringComparison.Ordinal);
        Assert.False(service.TryScheduleCapture("before", 10, out _));

        Assert.True(service.TryBeginCapture(3, out RuntimeVisualSummaryCapture before));
        service.ReportSuccess(before);
        Assert.True(service.TryBeginCapture(9, out RuntimeVisualSummaryCapture after));
        service.ReportSuccess(after);
        service.Seal();

        Assert.True(service.IsComplete);
        Assert.True(service.Succeeded);
        Assert.Equal(2, service.GetCaptureResults().Count);
    }

    [Fact]
    public void AdvancingPastScheduledFrameFailsCaptureDeterministically()
    {
        string root = Path.Combine(Path.GetTempPath(), "ArisenVisualSummaryTests");
        var service = new RuntimeVisualSummaryService(root, "Development");
        Assert.True(service.TryScheduleCapture("before", 3, out _));

        Assert.False(service.TryBeginCapture(4, out _));
        service.Seal();

        Assert.True(service.IsComplete);
        Assert.False(service.Succeeded);
        Assert.Contains("missed requested frame", service.FailureMessage);
    }
}
