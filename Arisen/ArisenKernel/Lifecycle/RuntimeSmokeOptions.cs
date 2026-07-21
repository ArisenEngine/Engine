namespace ArisenKernel.Lifecycle;

public enum RuntimeSmokeMode
{
    Boot,
    Scene,
    HotReload,
    WorldStreaming
}

internal readonly record struct RuntimeSmokeOptions(
    bool Enabled,
    RuntimeSmokeMode Mode,
    uint RequestedFrameCount,
    bool CaptureVisualSummary,
    string? VisualSummaryOutputPath,
    string? SmokeSummaryOutputPath)
{
    public static RuntimeSmokeOptions Disabled { get; } = new(
        false,
        RuntimeSmokeMode.Boot,
        1,
        false,
        null,
        null);

    public uint EffectiveFrameCount => Math.Max(RequestedFrameCount, GetMinimumFrameCount(Mode));

    public string ModeName => Mode switch
    {
        RuntimeSmokeMode.Boot => "boot",
        RuntimeSmokeMode.Scene => "scene",
        RuntimeSmokeMode.HotReload => "hot-reload",
        RuntimeSmokeMode.WorldStreaming => "world-streaming",
        _ => Mode.ToString()
    };

    public static RuntimeSmokeOptions Parse(string[] args)
    {
        bool enabled = false;
        RuntimeSmokeMode mode = RuntimeSmokeMode.Boot;
        bool modeSpecified = false;
        bool captureVisualSummary = false;
        string? visualSummaryOutputPath = null;
        string? smokeSummaryOutputPath = null;
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
                modeSpecified = true;
                enabled = true;
                i++;
            }
            else if (string.Equals(args[i], "--smoke-scene", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.Scene;
                modeSpecified = true;
                enabled = true;
            }
            else if (string.Equals(args[i], "--smoke-hot-reload", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.HotReload;
                modeSpecified = true;
                enabled = true;
            }
            else if (string.Equals(args[i], "--smoke-world-streaming", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.WorldStreaming;
                modeSpecified = true;
                enabled = true;
            }
            else if (string.Equals(args[i], "--visual-summary", StringComparison.OrdinalIgnoreCase))
            {
                captureVisualSummary = true;
                enabled = true;
                if (!modeSpecified)
                {
                    mode = RuntimeSmokeMode.Scene;
                }
            }
            else if (string.Equals(
                         args[i],
                         "--smoke-summary-output",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    throw new ArgumentException(
                        "--smoke-summary-output requires a non-empty path.",
                        nameof(args));
                }

                smokeSummaryOutputPath = Path.GetFullPath(args[++i]);
                enabled = true;
            }
            else if (string.Equals(
                         args[i],
                         "--visual-summary-output",
                         StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
                {
                    throw new ArgumentException(
                        "--visual-summary-output requires a non-empty path.",
                        nameof(args));
                }

                visualSummaryOutputPath = Path.GetFullPath(args[++i]);
                captureVisualSummary = true;
                enabled = true;
                if (!modeSpecified)
                {
                    mode = RuntimeSmokeMode.Scene;
                }
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

        if (captureVisualSummary && mode is not (
                RuntimeSmokeMode.Scene or RuntimeSmokeMode.WorldStreaming))
        {
            throw new ArgumentException(
                "--visual-summary requires scene or world-streaming smoke mode.",
                nameof(args));
        }

        if (smokeSummaryOutputPath != null && mode != RuntimeSmokeMode.WorldStreaming)
        {
            throw new ArgumentException(
                "--smoke-summary-output requires world-streaming smoke mode.",
                nameof(args));
        }

        return enabled
            ? new RuntimeSmokeOptions(
                true,
                mode,
                frames,
                captureVisualSummary,
                visualSummaryOutputPath,
                smokeSummaryOutputPath)
            : Disabled;
    }

    private static uint GetMinimumFrameCount(RuntimeSmokeMode mode) => mode switch
    {
        RuntimeSmokeMode.Boot => 1,
        RuntimeSmokeMode.Scene => 2,
        RuntimeSmokeMode.HotReload => 4,
        RuntimeSmokeMode.WorldStreaming => 1024,
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

        if (string.Equals(value, "world-streaming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "worldstreaming", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeSmokeMode.WorldStreaming;
        }

        throw new ArgumentException(
            $"Unknown smoke mode '{value}'. Expected one of: boot, scene, hot-reload, world-streaming.",
            nameof(value));
    }
}
