using System;
using System.IO;
using Xunit;

namespace Com.Arisen.Rendering.Tests;

/// <summary>
/// Frame pacing for a host that cannot present. A minimized window has a zero-extent native
/// surface, so its swapchain can neither acquire nor present an image and every frame an
/// interactive runtime would produce in that state is invisible: presentation is otherwise the
/// loop's only pacing, which is why a minimized runtime used to burn a core at thousands of frames
/// per second. The decision is that the host owns the pacing - the platform subsystem parks the
/// engine thread on the window message queue while the main window is minimized, and the frame
/// clock is re-based when the loop resumes - so the wiring is pinned here across the kernel
/// contract, the platform package, and the kernel loop that stays host-agnostic.
/// </summary>
public sealed class HostFramePacingContractTests
{
    [Fact]
    public void TheHostContractExposesTheStateAndTheWaitTheParkIsBuiltFrom()
    {
        string windowProviderContract = ReadSource(
            "Arisen/ArisenKernel/Contracts/IWindowProvider.cs");
        string win32Native = ReadSource(
            "Arisen/Development/PackageGame/Local/com.arisen.platform.desktop/Desktop/Win32Native.cs");
        string desktopPackage = ReadSource(
            "Arisen/Development/PackageGame/Local/com.arisen.platform.desktop/DesktopPackage.cs");
        string messageHandle = ReadSource(
            "Arisen/Development/PackageGame/Local/com.arisen.platform.desktop/Desktop/WindowsMessageHandle.cs");

        // The contract carries the window state that gates presentation and the wait primitive the
        // park is built from; neither is a runtime, a frame budget, or an interval.
        Assert.Contains("bool IsMainWindowMinimized { get; }", windowProviderContract, StringComparison.Ordinal);
        Assert.Contains("void WaitForWindowMessage();", windowProviderContract, StringComparison.Ordinal);

        // The provider answers the state from the operating system and parks on the message queue
        // of the thread that owns the window.
        Assert.Contains(
            "[DllImport(\"user32.dll\", EntryPoint = \"IsIconic\"",
            win32Native,
            StringComparison.Ordinal);
        Assert.Contains(
            "[DllImport(\"user32.dll\", EntryPoint = \"WaitMessage\"",
            win32Native,
            StringComparison.Ordinal);
        Assert.Contains("Win32Native.IsIconic(handle)", desktopPackage, StringComparison.Ordinal);
        Assert.Contains("m_MessageHandler?.WaitForMessage();", desktopPackage, StringComparison.Ordinal);
        Assert.Contains("Win32Native.WaitMessage();", messageHandle, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMinimizedHostParksItsFrameLoopInsteadOfSpinning()
    {
        string park = ReadSource(
            "Arisen/Development/PackageGame/Local/com.arisen.platform.desktop/HostFramePark.cs");
        string platformSubsystem = ReadSource(
            "Arisen/Development/PackageGame/Local/com.arisen.platform.desktop/PlatformSubsystem.cs");

        // The park loops on the minimized state and waits for a message per wake-up; the drain
        // runs between the wait and the re-check so a restore and a close request are both
        // observed before the frame continues.
        Assert.Contains("while (windowProvider.IsMainWindowMinimized)", park, StringComparison.Ordinal);
        Assert.Contains("windowProvider.WaitForWindowMessage();", park, StringComparison.Ordinal);
        Assert.Contains("windowProvider.PumpEvents()", park, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", park, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", park, StringComparison.Ordinal);
        Assert.DoesNotContain("SpinWait", park, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Yield", park, StringComparison.Ordinal);

        // The frame loop parks behind the minimized state - never behind visibility, which the
        // hidden startup window of an interactive runtime would trip - and only after the input
        // snapshot of the frame that observes the minimize was refreshed.
        Assert.Contains(
            "if (m_WindowProvider == null || !m_WindowProvider.IsMainWindowMinimized)",
            platformSubsystem,
            StringComparison.Ordinal);
        Assert.DoesNotContain("IsWindowVisible", platformSubsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", platformSubsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Yield", platformSubsystem, StringComparison.Ordinal);
        Assert.DoesNotContain("SpinWait", platformSubsystem, StringComparison.Ordinal);

        int pumpIndex = platformSubsystem.IndexOf("m_InputProvider?.Pump();", StringComparison.Ordinal);
        int parkIndex = platformSubsystem.IndexOf(
            "HostFramePark.ParkWhileMinimized(m_WindowProvider)",
            StringComparison.Ordinal);
        int resyncIndex = platformSubsystem.IndexOf("Time.ResyncFrameClock();", StringComparison.Ordinal);
        Assert.True(pumpIndex >= 0, "The frame loop must refresh the input snapshot before parking.");
        Assert.True(parkIndex > pumpIndex, "The park must run after the frame's input snapshot.");
        Assert.True(
            resyncIndex > parkIndex,
            "The frame clock must be re-based after the park, before the frame loop resumes.");
    }

    [Fact]
    public void TheKernelLoopStaysHostAgnosticAndIsNotPacedByTime()
    {
        string engineKernel = ReadSource("Arisen/ArisenKernel/Lifecycle/EngineKernel.cs");

        int runStart = engineKernel.IndexOf("public int Run()", StringComparison.Ordinal);
        int runEnd = engineKernel.IndexOf("public int RunForFrames(", StringComparison.Ordinal);
        Assert.True(
            runStart >= 0 && runEnd > runStart,
            "The interactive frame loop must stay the host-independent loop it is.");

        // Pacing belongs to the host: the interactive loop neither knows the window state nor
        // sleeps, yields or spins to approximate a frame budget. It ticks the frame clock and the
        // subsystems, and the host's park is what withholds frames the host cannot show.
        string interactiveLoop = engineKernel[runStart..runEnd];
        Assert.DoesNotContain("IsMainWindowMinimized", interactiveLoop, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Sleep", interactiveLoop, StringComparison.Ordinal);
        Assert.DoesNotContain("Thread.Yield", interactiveLoop, StringComparison.Ordinal);
        Assert.DoesNotContain("SpinWait", interactiveLoop, StringComparison.Ordinal);
        Assert.Contains("Time.Update();", interactiveLoop, StringComparison.Ordinal);
        Assert.Contains("TickCore(Time.deltaTime);", interactiveLoop, StringComparison.Ordinal);
    }

    [Fact]
    public void TheParkedIntervalIsNotEngineTime()
    {
        string time = ReadSource("Arisen/ArisenKernel/Lifecycle/Time.cs");

        Assert.Contains("public static void ResyncFrameClock()", time, StringComparison.Ordinal);
        Assert.Contains(
            "s_LastFrameTime = s_Stopwatch.Elapsed.TotalSeconds;",
            time,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheRuntimeGateDrivesTheParkThroughARealWindow()
    {
        string gate = ReadSource("Arisen/Scripts/Windows/validate_host_pacing.ps1");
        string runtimeValidation = ReadSource("Arisen/Scripts/Windows/validate_runtime.bat");
        string runtimeSummary = ReadSource("Arisen/Scripts/Windows/write_runtime_validation_summary.ps1");
        string stabilityStress = ReadSource("Arisen/Scripts/Windows/validate_stability_stress.ps1");

        // The gate drives the window the platform package created through the minimize the park
        // answers, and reads the log the running process owns, because a parked loop is only
        // observable from outside the parked process.
        Assert.Contains(
            "[ArisenHostPacing.NativeMethods]::ShowWindowAsync($windowHandle, 6)",
            gate,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ArisenHostPacing.NativeMethods]::ShowWindowAsync($windowHandle, 9)",
            gate,
            StringComparison.Ordinal);
        Assert.Contains("Get-MainWindowHandle", gate, StringComparison.Ordinal);
        Assert.Contains("[System.IO.FileShare]::ReadWrite", gate, StringComparison.Ordinal);

        // The parked interval is asserted from observable state, not from elapsed time: the
        // minimized runtime advances no frame, keeps the frame it parked on, and burns a fraction
        // of one core.
        Assert.Contains("$framesWhileParked -eq 0", gate, StringComparison.Ordinal);
        Assert.Contains("$resumeFrame -eq $parkFrame", gate, StringComparison.Ordinal);
        Assert.Contains("ParkedCpuBudgetRatio", gate, StringComparison.Ordinal);

        // The zero-frame parked interval is only evidence of pacing if the loop was presenting
        // frames before the minimize, so the gate requires the visible window to present first.
        Assert.Contains("$visibleFrames -ge $MinimumVisibleFrames", gate, StringComparison.Ordinal);

        // The runtime validation profile gate runs the parking gate where an interactive window
        // exists, and it can be opted out when the desktop cannot be left undisturbed.
        Assert.Contains("validate_host_pacing.ps1", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("--skip-host-pacing", runtimeValidation, StringComparison.Ordinal);
        Assert.Contains("CURRENT_HOST_PACING_REQUESTED", runtimeValidation, StringComparison.Ordinal);

        // The run is evidence, not a log line: the runtime summary publishes the run count and the
        // artifact path, and the release promotion gate requires both.
        Assert.Contains("hostPacingRuns = [int]$env:HOST_PACING_RUNS", runtimeSummary, StringComparison.Ordinal);
        Assert.Contains("hostPacingArtifactPaths = $hostPacingArtifactPaths", runtimeSummary, StringComparison.Ordinal);
        Assert.Contains("[int]$summary.hostPacingRuns -eq 1", stabilityStress, StringComparison.Ordinal);
        Assert.Contains("[int]$summary.schemaVersion -eq 9", stabilityStress, StringComparison.Ordinal);
    }

    private static string ReadSource(string relativePath)
    {
        return File.ReadAllText(Path.Combine(CppSourceContractScanner.FindRepoRoot(), relativePath));
    }
}
