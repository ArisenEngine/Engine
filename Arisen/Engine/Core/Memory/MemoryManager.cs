namespace ArisenEngine.Core.Memory;

public static class MemoryManager
{
    private static FrameArena? s_FrameArena;

    public static FrameArena FrameArena => s_FrameArena ?? throw new InvalidOperationException("MemoryManager not initialized");

    public static void Initialize(uint frameArenaSizeMB = 64)
    {
        s_FrameArena = new FrameArena(frameArenaSizeMB);
    }

    public static void Shutdown()
    {
        s_FrameArena?.Dispose();
        s_FrameArena = null;
    }
}
