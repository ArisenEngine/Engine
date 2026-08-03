using ArisenKernel.Contracts;
using ArisenKernel.Lifecycle;
using Xunit;

namespace ArisenKernel.Tests;

public sealed class RenderSurfaceRegistryTests
{
    [Fact]
    public void StaleRegistrationCannotReadResizeOrReleaseReplacement()
    {
        var registry = new RenderSurfaceRegistry();
        var firstSurface = new TestRenderSurface(new IntPtr(0x101));
        RenderSurfaceRegistration first = registry.Register(
            firstSurface.Handle,
            "SceneView:first",
            SurfaceType.SceneView,
            firstSurface);

        Assert.True(registry.TryGet(first, out SurfaceInfo firstInfo));
        Assert.Same(firstSurface, firstInfo.Surface);
        Assert.True(registry.Unregister(first));
        Assert.Equal(1, firstSurface.DisposeCount);

        var replacementSurface = new TestRenderSurface(firstSurface.Handle);
        RenderSurfaceRegistration replacement = registry.Register(
            replacementSurface.Handle,
            "SceneView:replacement",
            SurfaceType.SceneView,
            replacementSurface);

        Assert.True(replacement.Generation > first.Generation);
        Assert.False(registry.TryGet(first, out _));
        Assert.False(registry.Unregister(first));
        Assert.Equal(0, replacementSurface.DisposeCount);
        Assert.True(registry.TryGet(replacement, out SurfaceInfo replacementInfo));
        Assert.Same(replacementSurface, replacementInfo.Surface);

        registry.Dispose();
        registry.Dispose();
        Assert.Equal(1, replacementSurface.DisposeCount);
    }

    [Fact]
    public void DuplicateHostDoesNotTransferSurfaceOwnership()
    {
        var registry = new RenderSurfaceRegistry();
        var registeredSurface = new TestRenderSurface(new IntPtr(0x201));
        var rejectedSurface = new TestRenderSurface(registeredSurface.Handle);
        registry.Register(
            registeredSurface.Handle,
            "GameView",
            SurfaceType.GameView,
            registeredSurface);

        Assert.Throws<InvalidOperationException>(() => registry.Register(
            rejectedSurface.Handle,
            "GameView:duplicate",
            SurfaceType.GameView,
            rejectedSurface));

        Assert.Equal(0, rejectedSurface.DisposeCount);
        registry.Dispose();
        Assert.Equal(1, registeredSurface.DisposeCount);
    }

    [Fact]
    public void VirtualSurfaceHostDoesNotRequireNativeWindowHandle()
    {
        using var registry = new RenderSurfaceRegistry();
        var virtualSurface = new TestRenderSurface(IntPtr.Zero);

        RenderSurfaceRegistration registration = registry.Register(
            new IntPtr(0x251),
            "SceneView",
            SurfaceType.SceneView,
            virtualSurface);

        Assert.True(registration.IsValid);
        Assert.True(registry.TryGet(registration, out SurfaceInfo surfaceInfo));
        Assert.Equal(IntPtr.Zero, surfaceInfo.Surface.Handle);
        Assert.Equal(new IntPtr(0x251), surfaceInfo.Parent);
    }

    [Fact]
    public void DrainAttemptsEverySurfaceAndReportsAllFailures()
    {
        var registry = new RenderSurfaceRegistry();
        var failingSurface = new TestRenderSurface(new IntPtr(0x301), throwOnDispose: true);
        var healthySurface = new TestRenderSurface(new IntPtr(0x302));
        registry.Register(
            failingSurface.Handle,
            "SceneView",
            SurfaceType.SceneView,
            failingSurface);
        registry.Register(
            healthySurface.Handle,
            "GameView",
            SurfaceType.GameView,
            healthySurface);

        AggregateException failure = Assert.Throws<AggregateException>(() => registry.Drain());

        Assert.Single(failure.InnerExceptions);
        Assert.Contains("SceneView", failure.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, registry.Count);
        Assert.Equal(1, failingSurface.DisposeCount);
        Assert.Equal(1, healthySurface.DisposeCount);
        Assert.Equal(0, registry.Drain());
        registry.Dispose();
        Assert.Equal(1, failingSurface.DisposeCount);
        Assert.Equal(1, healthySurface.DisposeCount);
    }

    [Fact]
    public void KernelResetDisposesOldOwnerAndCreatesFreshRegistry()
    {
        using var kernel = new EngineKernel();
        RenderSurfaceRegistry oldRegistry = kernel.RenderSurfaces;
        var surface = new TestRenderSurface(new IntPtr(0x401));
        oldRegistry.Register(
            surface.Handle,
            "RuntimeMainWindow",
            SurfaceType.Window,
            surface);

        kernel.Reset();

        Assert.True(oldRegistry.IsDisposed);
        Assert.Equal(1, surface.DisposeCount);
        Assert.NotSame(oldRegistry, kernel.RenderSurfaces);
        Assert.False(kernel.RenderSurfaces.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => oldRegistry.Register(
            new IntPtr(0x402),
            "stale",
            SurfaceType.Window,
            new TestRenderSurface(new IntPtr(0x402))));
    }

    [Fact]
    public void ChildProcessDoesNotInheritManagedRegistryPointer()
    {
        const string variableName = "ARISEN_SURFACE_REGISTRY_ADDR";
        string? previousValue = Environment.GetEnvironmentVariable(variableName);
        try
        {
            Environment.SetEnvironmentVariable(variableName, null);
            using var registry = new RenderSurfaceRegistry();
            var surface = new TestRenderSurface(new IntPtr(0x501));
            registry.Register(
                surface.Handle,
                "SceneView",
                SurfaceType.SceneView,
                surface);

            var startInfo = new System.Diagnostics.ProcessStartInfo(
                "cmd.exe",
                $"/d /c if defined {variableName} (exit /b 17) else (exit /b 0)")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using System.Diagnostics.Process child =
                System.Diagnostics.Process.Start(startInfo) ??
                throw new InvalidOperationException("Could not start child process for environment validation.");
            child.WaitForExit();

            Assert.Equal(0, child.ExitCode);
            Assert.Null(Environment.GetEnvironmentVariable(variableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, previousValue);
        }
    }

    private sealed class TestRenderSurface : IRenderSurface
    {
        private readonly bool m_ThrowOnDispose;

        public TestRenderSurface(IntPtr handle, bool throwOnDispose = false)
        {
            Handle = handle;
            m_ThrowOnDispose = throwOnDispose;
        }

        public int DisposeCount { get; private set; }
        public string Name => "TestSurface";
        public IntPtr Handle { get; }
        public uint SurfaceId => 1;
        public uint Width => 1;
        public uint Height => 1;

        public void DisposeSurface()
        {
            DisposeCount++;
            if (m_ThrowOnDispose)
            {
                throw new InvalidOperationException("Injected surface-disposal failure.");
            }
        }

        public void Dispose() => DisposeSurface();
        public void Resize(uint width, uint height) { }
        public IntPtr GetHandle() => Handle;
        public IntPtr GetSharedHandle(uint frameIndex) => IntPtr.Zero;
        public ulong GetSharedMemorySize(uint frameIndex) => 0;
        public IntPtr GetRenderFinishedSemaphoreHandle(uint frameIndex) => IntPtr.Zero;
        public IntPtr CreateConsumedSemaphoreHandle(uint frameIndex) => IntPtr.Zero;
        public void CompleteConsumedSemaphoreHandle(IntPtr handle) { }
        public void ReleaseConsumedSemaphoreHandle(IntPtr handle) { }
        public ulong GetLastRenderTicket() => 0;
        public uint GetLastRenderFrameIndex() => 0;
        public Task WaitForRenderTicketAsync(ulong ticket) => Task.CompletedTask;
        public RenderOutputInfo GetOutputInfo() => default;
        public void ReportConsumedFrameIndex(uint frameIndex) { }
        public uint GetLastConsumedFrameIndex() => 0;
        public void OnCreate() { }
        public void OnResizing() { }
        public void OnResized() { }
        public void OnDestroy() { }
    }
}
