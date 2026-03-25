using System.Diagnostics;

namespace ArisenKernel.Lifecycle;

public static class Time
{
    private static Stopwatch s_Stopwatch = new Stopwatch();
    private static double s_LastFrameTime = 0;
    private static float s_DeltaTime = 0;
    // B11: Use double for accumulated time to prevent float precision drift after ~4.6 hours
    private static double s_ElapsedTime = 0;

    static Time()
    {
        s_Stopwatch.Start();
    }

    public static float deltaTime => s_DeltaTime;
    /// <summary>Accumulated elapsed time in seconds (double precision to avoid drift).</summary>
    public static double elapsedTime => s_ElapsedTime;
    /// <summary>Total wall-clock time since engine start (double precision).</summary>
    public static double totalTime => s_Stopwatch.Elapsed.TotalSeconds;

    internal static void Update()
    {
        double currentTime = s_Stopwatch.Elapsed.TotalSeconds;
        s_DeltaTime = (float)(currentTime - s_LastFrameTime);
        s_ElapsedTime += s_DeltaTime;
        s_LastFrameTime = currentTime;
    }
}
