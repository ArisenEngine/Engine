using System.Diagnostics;

namespace ArisenEngine.Core;

public static class Time
{
    private static Stopwatch s_Stopwatch = new Stopwatch();
    private static float s_LastFrameTime = 0;
    private static float s_DeltaTime = 0;
    private static float s_ElapsedTime = 0;

    static Time()
    {
        s_Stopwatch.Start();
    }

    public static float deltaTime => s_DeltaTime;
    public static float elapsedTime => s_ElapsedTime;
    public static float totalTime => (float)s_Stopwatch.Elapsed.TotalSeconds;

    internal static void Update()
    {
        float currentTime = (float)s_Stopwatch.Elapsed.TotalSeconds;
        s_DeltaTime = currentTime - s_LastFrameTime;
        s_ElapsedTime += s_DeltaTime;
        s_LastFrameTime = currentTime;
    }
}
