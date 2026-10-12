namespace ArisenKernel.Lifecycle;

internal readonly record struct RuntimeAssetOptions(bool EnableSourceAssetDiagnostics)
{
    public const string SourceDiagnosticsArgument = "--diagnostic-source-assets";

    public static RuntimeAssetOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new RuntimeAssetOptions(args.Any(argument => string.Equals(
            argument,
            SourceDiagnosticsArgument,
            StringComparison.OrdinalIgnoreCase)));
    }

    public void Validate(
        string profile,
        bool deployedLaunch,
        bool isEditorProfile,
        bool isDeployedProfile)
    {
        if (!EnableSourceAssetDiagnostics)
        {
            return;
        }

        if (deployedLaunch)
        {
            throw new InvalidOperationException(
                $"{SourceDiagnosticsArgument} is unavailable for deployed launches.");
        }

        if (isDeployedProfile)
        {
            throw new InvalidOperationException(
                $"{SourceDiagnosticsArgument} is unavailable for deployed profile '{profile}'. " +
                "A deployed profile boots from its cooked catalog and never reads source assets.");
        }

        if (isEditorProfile)
        {
            throw new InvalidOperationException(
                $"{SourceDiagnosticsArgument} is unnecessary for editor profile '{profile}'; " +
                "editor source access is compile-owned.");
        }
    }
}
