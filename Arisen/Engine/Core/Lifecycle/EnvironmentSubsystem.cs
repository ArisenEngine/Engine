using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Core.Lifecycle;

public class EnvironmentSubsystem : IEngineSubsystem
{
    public int Priority => 0; // Initialize first
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public string StartupPath { get; private set; } = string.Empty;
    public string DataPath { get; private set; } = string.Empty;
    public string ProjectRoot { get; private set; } = string.Empty;
    public string ProjectName { get; private set; } = string.Empty;
    public RuntimePlatform Platform { get; private set; } = RuntimePlatform.Windows;

    public void SetProject(string root, string name)
    {
        ProjectRoot = root;
        ProjectName = name;
        Logger.Log($"[EnvironmentSubsystem] Project set to: {ProjectName} at {ProjectRoot}");
    }

    public void Initialize()
    {
        var config = EngineKernel.Instance.Config;
        if (config != null)
        {
            StartupPath = config.StartupPath;
            DataPath = config.DataPath;
            ProjectRoot = config.ProjectRoot;
            ProjectName = config.ProjectName;
            Platform = config.Platform;
        }

        Logger.Log($"[EnvironmentSubsystem] Initialized. Platform: {Platform}, StartupPath: {StartupPath}");
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}
