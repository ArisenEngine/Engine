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

    public void Validate(string profile, bool deployedLaunch)
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

        if (string.Equals(profile, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{SourceDiagnosticsArgument} is unavailable for the Production profile.");
        }

        if (string.Equals(profile, "Editor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{SourceDiagnosticsArgument} is unnecessary for the Editor profile; " +
                "editor source access is compile-owned.");
        }
    }
}
