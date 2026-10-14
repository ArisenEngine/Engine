namespace ArisenKernel.Lifecycle;

public enum RuntimePlatform
{
    Windows,
    Linux,
    macOS,
    Unknown
}

public enum EngineExecutionMode
{
    Runtime,
    RuntimeAssetCook
}

public sealed record SelectedPackageRequirement(
    string PackageId,
    string VersionExpression,
    string Source);

public class EngineConfig
{
    public string AppName { get; set; } = "ArisenApplication";
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 720;

    public string StartupPath { get; set; } = string.Empty;
    public string DataPath { get; set; } = string.Empty;
    public string ProjectRoot { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the workspace profile this process was launched for. Profile names are labels chosen
    /// by the workspace author, so engine behaviour must key off declared profile properties rather
    /// than off a specific name. Consumers that need a profile identity (for example the deployed
    /// asset catalog, which records the profile it was cooked for) read it from here.
    /// </summary>
    public string Profile { get; set; } = string.Empty;

    public RuntimePlatform Platform { get; set; } = RuntimePlatform.Windows;
    public EngineExecutionMode ExecutionMode { get; set; } = EngineExecutionMode.Runtime;
    public bool EnableSourceAssetDiagnostics { get; set; }

    /// <summary>
    /// Name of the bounded smoke mode that owns this process, or null for an interactive run.
    /// A bounded smoke host and its package scenarios own world activation, so composition
    /// packages must not claim the world streaming source while this is set.
    /// </summary>
    public string? SmokeModeName { get; set; }

    /// <summary>
    /// Startup target selected on the command line. Unspecified when the workspace manifest decides,
    /// which is the default for a workspace that ships one product; a workspace hosting several
    /// products (the game and the sample gallery) selects the target per process instead.
    /// </summary>
    public StartupTargetOptions StartupTarget { get; set; } = StartupTargetOptions.None;

    /// <summary>B13: Topological list of package local paths to load during kernel initialization.</summary>
    public List<string> PackageUrls { get; set; } = new();

    /// <summary>
    /// Direct workspace constraints plus exact build-resolved package identities that must match
    /// the package metadata loaded from <c>PackageUrls</c> before any package entry runs.
    /// </summary>
    public List<SelectedPackageRequirement> PackageRequirements { get; set; } = new();

    // Add additional configuration properties as needed
}
