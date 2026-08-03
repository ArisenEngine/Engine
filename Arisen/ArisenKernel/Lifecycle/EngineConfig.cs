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
    public RuntimePlatform Platform { get; set; } = RuntimePlatform.Windows;
    public EngineExecutionMode ExecutionMode { get; set; } = EngineExecutionMode.Runtime;
    public bool EnableSourceAssetDiagnostics { get; set; }

    /// <summary>B13: Topological list of package local paths to load during kernel initialization.</summary>
    public List<string> PackageUrls { get; set; } = new();

    /// <summary>
    /// Direct workspace constraints plus exact build-resolved package identities that must match
    /// the package metadata loaded from <c>PackageUrls</c> before any package entry runs.
    /// </summary>
    public List<SelectedPackageRequirement> PackageRequirements { get; set; } = new();

    // Add additional configuration properties as needed
}
