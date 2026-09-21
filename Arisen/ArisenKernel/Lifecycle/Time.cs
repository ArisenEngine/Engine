using System.Diagnostics;

namespace ArisenKernel.Lifecycle;

public static class Time
{
    private static Stopwatch s_Stopwatch = new Stopwatch();
    private static double s_LastFrameTime = 0;
    private static float s_DeltaTime = 0;
    // B11: Use double for accumulated time to prevent float precision drift after ~4.6 hours
    private static double s_ElapsedTime = 0;
    private static double? s_PinnedElapsedTime;

    static Time()
    {
        s_Stopwatch.Start();
    }

    public static float deltaTime => s_DeltaTime;
    /// <summary>Accumulated elapsed time in seconds (double precision to avoid drift).</summary>
    public static double elapsedTime => s_ElapsedTime;
    /// <summary>Total wall-clock time since engine start (double precision).</summary>
    public static double totalTime => s_Stopwatch.Elapsed.TotalSeconds;

    /// <summary>
    /// True while the animation clock is held at an explicit elapsed time by <see cref="Pin"/>.
    /// </summary>
    public static bool IsPinned => s_PinnedElapsedTime.HasValue;

    /// <summary>
    /// Holds the engine clock at an explicit elapsed time until <see cref="Unpin"/> is called.
    /// Deterministic validators use this so a stationary camera frame renders with exactly the
    /// same time-driven inputs as the frame before it: with the clock frozen, a different output
    /// means real non-determinism instead of expected animation progress. While pinned,
    /// <see cref="deltaTime"/> is zero and <see cref="elapsedTime"/> stays at the pinned value.
    /// </summary>
    public static void Pin(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedSeconds),
                "[Time] Pinned elapsed time must be finite and non-negative.");
        }

        s_PinnedElapsedTime = elapsedSeconds;
        s_ElapsedTime = elapsedSeconds;
        s_DeltaTime = 0.0f;
        s_LastFrameTime = s_Stopwatch.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// Releases a clock pin. Idempotent; the clock resumes advancing from the pinned value.
    /// </summary>
    public static void Unpin()
    {
        s_PinnedElapsedTime = null;
    }

    internal static void Update()
    {
        double currentTime = s_Stopwatch.Elapsed.TotalSeconds;
        if (s_PinnedElapsedTime.HasValue)
        {
            s_DeltaTime = 0.0f;
            s_ElapsedTime = s_PinnedElapsedTime.Value;
            s_LastFrameTime = currentTime;
            return;
        }

        s_DeltaTime = (float)(currentTime - s_LastFrameTime);
        s_ElapsedTime += s_DeltaTime;
        s_LastFrameTime = currentTime;
    }
}
