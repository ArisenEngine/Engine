using System.Threading;
using ArisenEngine.Rendering;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

public sealed class RenderDocCaptureArtifactProbeTests
{
    [Fact]
    public void WaitForPublicationReturnsMatchingNonEmptyInventoryArtifact()
    {
        using var directory = TemporaryDirectory.Create();
        using var observationSignal = new AutoResetEvent(false);
        string pathTemplate = Path.Combine(directory.Path, "request-");
        string capturePath = $"{pathTemplate}_capture.rdc";
        File.WriteAllBytes(capturePath, [0x52, 0x44, 0x43, 0x01]);

        RenderDocCaptureArtifactProbeResult result =
            RenderDocCaptureArtifactProbe.WaitForPublication(
                captureCountBeforeStart: 0,
                pathTemplate,
                readCaptureCount: () => 1,
                TryReadCapturePath,
                observationSignal,
                CancellationToken.None);

        Assert.Equal(RenderDocCaptureArtifactProbeStatus.Ready, result.Status);
        Assert.Equal(0U, result.CaptureIndex);
        Assert.Equal(Path.GetFullPath(capturePath), result.CandidatePath);
        Assert.Equal(4, result.Length);
        return;

        bool TryReadCapturePath(
            uint captureIndex,
            out string path,
            out string diagnostic)
        {
            Assert.Equal(0U, captureIndex);
            path = capturePath;
            diagnostic = string.Empty;
            return true;
        }
    }

    [Fact]
    public async Task WaitForPublicationRetriesOnlyAfterExplicitObservationSignal()
    {
        using var directory = TemporaryDirectory.Create();
        using var observationSignal = new AutoResetEvent(false);
        var firstObservation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        string pathTemplate = Path.Combine(directory.Path, "request-");
        string capturePath = $"{pathTemplate}_capture.rdc";
        File.WriteAllBytes(capturePath, [0x52, 0x44, 0x43, 0x01]);
        int captureCount = 0;
        int observationCount = 0;

        Task<RenderDocCaptureArtifactProbeResult> probe = Task.Factory.StartNew(
            () => RenderDocCaptureArtifactProbe.WaitForPublication(
                captureCountBeforeStart: 0,
                pathTemplate,
                ReadCaptureCount,
                TryReadCapturePath,
                observationSignal,
                CancellationToken.None),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await firstObservation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, Volatile.Read(ref observationCount));
        Volatile.Write(ref captureCount, 1);
        observationSignal.Set();

        RenderDocCaptureArtifactProbeResult result =
            await probe.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RenderDocCaptureArtifactProbeStatus.Ready, result.Status);
        Assert.Equal(2, Volatile.Read(ref observationCount));

        uint ReadCaptureCount()
        {
            Interlocked.Increment(ref observationCount);
            firstObservation.TrySetResult();
            return checked((uint)Volatile.Read(ref captureCount));
        }

        bool TryReadCapturePath(
            uint captureIndex,
            out string path,
            out string diagnostic)
        {
            Assert.Equal(0U, captureIndex);
            path = capturePath;
            diagnostic = string.Empty;
            return true;
        }
    }

    [Fact]
    public void WaitForPublicationRejectsEmptyInventoryArtifact()
    {
        using var directory = TemporaryDirectory.Create();
        using var observationSignal = new AutoResetEvent(false);
        string pathTemplate = Path.Combine(directory.Path, "request-");
        string capturePath = $"{pathTemplate}_capture.rdc";
        using (File.Create(capturePath))
        {
        }

        RenderDocCaptureArtifactProbeResult result =
            RenderDocCaptureArtifactProbe.WaitForPublication(
                captureCountBeforeStart: 0,
                pathTemplate,
                readCaptureCount: () => 1,
                TryReadCapturePath,
                observationSignal,
                CancellationToken.None);

        Assert.Equal(RenderDocCaptureArtifactProbeStatus.Failed, result.Status);
        Assert.Contains("artifact is empty", result.Diagnostic);
        return;

        bool TryReadCapturePath(
            uint captureIndex,
            out string path,
            out string diagnostic)
        {
            Assert.Equal(0U, captureIndex);
            path = capturePath;
            diagnostic = string.Empty;
            return true;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"arisen-renderdoc-probe-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
