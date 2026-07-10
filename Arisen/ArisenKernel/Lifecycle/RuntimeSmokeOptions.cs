namespace ArisenKernel.Lifecycle;

public enum RuntimeSmokeMode
{
    Boot,
    Scene,
    HotReload
}

internal readonly record struct RuntimeSmokeOptions(
    bool Enabled,
    RuntimeSmokeMode Mode,
    uint RequestedFrameCount)
{
    public static RuntimeSmokeOptions Disabled { get; } = new(false, RuntimeSmokeMode.Boot, 1);

    public uint EffectiveFrameCount => Math.Max(RequestedFrameCount, GetMinimumFrameCount(Mode));

    public string ModeName => Mode switch
    {
        RuntimeSmokeMode.Boot => "boot",
        RuntimeSmokeMode.Scene => "scene",
        RuntimeSmokeMode.HotReload => "hot-reload",
        _ => Mode.ToString()
    };

    public static RuntimeSmokeOptions Parse(string[] args)
    {
        bool enabled = false;
        RuntimeSmokeMode mode = RuntimeSmokeMode.Boot;
        uint frames = 1;

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--smoke", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
            }
            else if (string.Equals(args[i], "--smoke-mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                mode = ParseMode(args[i + 1]);
                enabled = true;
                i++;
            }
            else if (string.Equals(args[i], "--smoke-scene", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.Scene;
                enabled = true;
            }
            else if (string.Equals(args[i], "--smoke-hot-reload", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.HotReload;
                enabled = true;
            }
            else if (string.Equals(args[i], "--frames", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length
                     && uint.TryParse(args[i + 1], out var parsedFrames))
            {
                frames = Math.Max(1, parsedFrames);
                enabled = true;
                i++;
            }
        }

        return enabled ? new RuntimeSmokeOptions(true, mode, frames) : Disabled;
    }

    private static uint GetMinimumFrameCount(RuntimeSmokeMode mode) => mode switch
    {
        RuntimeSmokeMode.Boot => 1,
        RuntimeSmokeMode.Scene => 2,
        RuntimeSmokeMode.HotReload => 4,
        _ => 1
    };

    private static RuntimeSmokeMode ParseMode(string value)
    {
        if (string.Equals(value, "boot", StringComparison.OrdinalIgnoreCase)) return RuntimeSmokeMode.Boot;
        if (string.Equals(value, "scene", StringComparison.OrdinalIgnoreCase)) return RuntimeSmokeMode.Scene;
        if (string.Equals(value, "hot-reload", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "hotreload", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeSmokeMode.HotReload;
        }

        throw new ArgumentException(
            $"Unknown smoke mode '{value}'. Expected one of: boot, scene, hot-reload.",
            nameof(value));
    }
}
