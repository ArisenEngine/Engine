namespace ArisenKernel.Lifecycle;

public enum RuntimeSmokeMode
{
    Boot,
    Scene,
    HotReload,
    WorldStreaming,
    TerrainStreaming
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

    /// <summary>
    /// The wall-clock bound of a bounded smoke run. The streaming scenarios cross and soak a regional
    /// world: one pose settles by loading whole world cells, each of which carries several terrain
    /// tiles, and the soak repeats that load/unload cycle on the pinned core. The path therefore needs
    /// more frames and more time than a single-cell world needed. The bound stays explicit and
    /// bounded, so a scenario that stops making progress still fails loudly instead of running on.
    /// </summary>
    public TimeSpan EffectiveDuration => GetMaximumDuration(Mode);

    public bool UsesPackageScenario => Mode is
        RuntimeSmokeMode.WorldStreaming or RuntimeSmokeMode.TerrainStreaming;

    public string ModeName => Mode switch
    {
        RuntimeSmokeMode.Boot => "boot",
        RuntimeSmokeMode.Scene => "scene",
        RuntimeSmokeMode.HotReload => "hot-reload",
        RuntimeSmokeMode.WorldStreaming => "world-streaming",
        RuntimeSmokeMode.TerrainStreaming => "terrain-streaming",
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
            else if (string.Equals(args[i], "--smoke-terrain-streaming", StringComparison.OrdinalIgnoreCase))
            {
                mode = RuntimeSmokeMode.TerrainStreaming;
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
                RuntimeSmokeMode.Scene or
                RuntimeSmokeMode.WorldStreaming or
                RuntimeSmokeMode.TerrainStreaming))
        {
            throw new ArgumentException(
                "--visual-summary requires scene, world-streaming, or terrain-streaming smoke mode.",
                nameof(args));
        }

        if (smokeSummaryOutputPath != null && mode is not (
                RuntimeSmokeMode.WorldStreaming or RuntimeSmokeMode.TerrainStreaming))
        {
            throw new ArgumentException(
                "--smoke-summary-output requires world-streaming or terrain-streaming smoke mode.",
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
        RuntimeSmokeMode.WorldStreaming => 2048,
        RuntimeSmokeMode.TerrainStreaming => 2048,
        _ => 1
    };

    private static TimeSpan GetMaximumDuration(RuntimeSmokeMode mode) => mode switch
    {
        RuntimeSmokeMode.WorldStreaming or RuntimeSmokeMode.TerrainStreaming =>
            TimeSpan.FromSeconds(120),
        _ => TimeSpan.FromSeconds(45)
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

        if (string.Equals(value, "terrain-streaming", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "terrainstreaming", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeSmokeMode.TerrainStreaming;
        }

        throw new ArgumentException(
            $"Unknown smoke mode '{value}'. Expected one of: boot, scene, hot-reload, " +
            "world-streaming, terrain-streaming.",
            nameof(value));
    }
}
