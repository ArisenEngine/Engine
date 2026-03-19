namespace ArisenKernel.Lifecycle;

public enum RuntimePlatform
{
    Windows,
    Linux,
    macOS,
    Unknown
}

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

    // Add additional configuration properties as needed
}
