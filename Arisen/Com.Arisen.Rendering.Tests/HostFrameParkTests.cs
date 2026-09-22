using System;
using System.Collections.Generic;
using ArisenEngine.Platform;
using ArisenKernel.Contracts;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Frame pacing for a host that cannot present: a minimized window reports a zero-extent native
/// surface, so its swapchain can neither acquire nor present, and presentation is otherwise the
/// only pacing an interactive runtime loop has. The park is driven here by a scripted host, so the
/// control flow - wait on the message queue, drain what woke the park, re-check the state, and end
/// the frame loop when the window closes - is pinned without a real window or a real clock.
/// </summary>
public sealed class HostFrameParkTests
{
    [Fact]
    public void AWindowThatCanPresentNeverParks()
    {
        var host = new ScriptedWindowProvider(minimized: false);

        bool survived = HostFramePark.ParkWhileMinimized(host);

        Assert.True(survived);
        Assert.Equal(0, host.WaitCount);
        Assert.Equal(0, host.PumpCount);
    }

    [Fact]
    public void TheParkWaitsAndDrainsUntilTheWindowCanPresentAgain()
    {
        var host = new ScriptedWindowProvider(minimized: true);
        host.EnqueueDrain(minimized: true);
        host.EnqueueDrain(minimized: false);

        bool survived = HostFramePark.ParkWhileMinimized(host);

        Assert.True(survived);
        Assert.Equal(2, host.WaitCount);
        Assert.Equal(2, host.PumpCount);
        // Every wake-up is drained before the state is re-checked, and the park never drains
        // without having waited for the message that makes the state observable.
        Assert.Equal(new[] { "wait", "pump", "wait", "pump" }, host.Log);
    }

    [Fact]
    public void TheParkStopsWaitingOnceTheWindowCanPresent()
    {
        var host = new ScriptedWindowProvider(minimized: true);
        host.EnqueueDrain(minimized: false);

        Assert.True(HostFramePark.ParkWhileMinimized(host));

        Assert.Equal(1, host.WaitCount);
        Assert.Equal(1, host.PumpCount);
    }

    [Fact]
    public void ACloseRequestThatArrivesWhileParkedEndsThePark()
    {
        var host = new ScriptedWindowProvider(minimized: true);
        host.EnqueueDrain(minimized: true, closing: true);

        bool survived = HostFramePark.ParkWhileMinimized(host);

        Assert.False(survived);
        Assert.Equal(1, host.WaitCount);
        Assert.Equal(1, host.PumpCount);
    }

    [Fact]
    public void TheParkWaitsForEveryDrainInsteadOfPolling()
    {
        var host = new ScriptedWindowProvider(minimized: true);
        for (int drain = 0; drain < 32; drain++)
        {
            host.EnqueueDrain(minimized: true);
        }

        host.EnqueueDrain(minimized: false);

        Assert.True(HostFramePark.ParkWhileMinimized(host));
        Assert.Equal(33, host.WaitCount);
        Assert.Equal(33, host.PumpCount);
    }

    [Fact]
    public void TheParkRequiresAWindowProvider()
    {
        Assert.Throws<ArgumentNullException>(() => HostFramePark.ParkWhileMinimized(null!));
    }

    /// <summary>
    /// Host that reports a window state per drain and records the order of the calls the park
    /// makes, with every member the park must not touch reported as unsupported.
    /// </summary>
    private sealed class ScriptedWindowProvider : IWindowProvider
    {
        private readonly Queue<(bool Minimized, bool Closing)> m_DrainResults = new();
        private readonly bool m_InitialMinimized;
        private bool m_Closing;

        internal ScriptedWindowProvider(bool minimized)
        {
            m_InitialMinimized = minimized;
            IsMainWindowMinimized = minimized;
        }

        internal int WaitCount { get; private set; }

        internal int PumpCount { get; private set; }

        internal List<string> Log { get; } = new();

        internal void EnqueueDrain(bool minimized, bool closing = false)
        {
            m_DrainResults.Enqueue((minimized, closing));
        }

        public bool IsMainWindowMinimized { get; private set; }

        public bool IsCloseRequested => m_Closing;

        public void WaitForWindowMessage()
        {
            WaitCount++;
            Log.Add("wait");
        }

        public bool PumpEvents()
        {
            PumpCount++;
            Log.Add("pump");

            (bool minimized, bool closing) = m_DrainResults.Count > 0
                ? m_DrainResults.Dequeue()
                : (m_InitialMinimized, false);
            IsMainWindowMinimized = minimized;
            m_Closing = closing;

            // A closing window reports close requested and stops draining, exactly like the real
            // provider: its close request is what the pump reports back to the frame loop.
            return !m_Closing;
        }

        public void SetMainWindowVisible(bool visible) => throw new NotSupportedException();

        public WindowSurfaceInfo EnsureMainWindow(WindowCreateInfo createInfo) =>
            throw new NotSupportedException();

        public WindowSurfaceInfo GetWindowInfo() => throw new NotSupportedException();

        public IntPtr GetWindowHandle() => throw new NotSupportedException();

        public (int Width, int Height) GetWindowSize() => throw new NotSupportedException();

        public void Close() => throw new NotSupportedException();

        public WindowProcessor CreateWindowProcessor() => throw new NotSupportedException();

        public event EventHandler<(int Width, int Height)>? OnWindowResized
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }

        public event Action<WindowResizeInfo>? WindowResized
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }

        public event Action? CloseRequested
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }
    }
}
