using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Arisen.Native.Diagnostics;

namespace ArisenEngine.Core.Diagnostics;

/// <summary>
/// High-level wrapper for the Arisen Engine Profiler.
/// </summary>
public static class Profiler
{
    /// <summary>
    /// Starts a profiling zone. Should be used with the 'using' statement.
    /// </summary>
    /// <param name="name">Name of the zone.</param>
    /// <returns>A disposable zone context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ProfilerZone Zone(string name)
    {
#if ARISEN_PROFILER_ENABLED
        return new ProfilerZone(ProfilerAPI.Profiler_BeginZone(name));
#else
        return default;
#endif
    }

    /// <summary>
    /// Marks a frame.
    /// </summary>
    [Conditional("ARISEN_PROFILER_ENABLED")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FrameMark()
    {
        ProfilerAPI.Profiler_FrameMark();
    }

    /// <summary>
    /// Marks a named frame.
    /// </summary>
    /// <param name="name">Name of the frame.</param>
    [Conditional("ARISEN_PROFILER_ENABLED")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FrameMarkNamed(string name)
    {
        ProfilerAPI.Profiler_FrameMarkNamed(name);
    }

    /// <summary>
    /// Plots a numerical value.
    /// </summary>
    /// <param name="name">Name of the plot.</param>
    /// <param name="value">Value to plot.</param>
    [Conditional("ARISEN_PROFILER_ENABLED")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PlotValue(string name, double value)
    {
        ProfilerAPI.Profiler_PlotValue(name, value);
    }

    /// <summary>
    /// Sets the name of the current thread for the profiler.
    /// </summary>
    /// <param name="name">Name of the thread.</param>
    [Conditional("ARISEN_PROFILER_ENABLED")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetThreadName(string name)
    {
        ProfilerAPI.Profiler_SetThreadName(name);
    }
}

/// <summary>
/// A disposable handle for a profiling zone.
/// </summary>
public readonly struct ProfilerZone : IDisposable
{
#if ARISEN_PROFILER_ENABLED
    private readonly ProfilerZoneContext _ctx;

    internal ProfilerZone(ProfilerZoneContext ctx)
    {
        _ctx = ctx;
    }
#endif

    /// <summary>
    /// Ends the profiling zone.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
#if ARISEN_PROFILER_ENABLED
        if (_ctx.Active != 0)
        {
            ProfilerAPI.Profiler_EndZone(_ctx);
        }
#endif
    }
}
