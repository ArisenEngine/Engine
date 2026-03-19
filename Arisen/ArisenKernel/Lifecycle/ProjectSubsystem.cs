using System.IO;
using System.Text.Json;
using ArisenKernel.Packages;
using ArisenKernel.Diagnostics;

namespace ArisenKernel.Lifecycle;

/// <summary>
/// Manages the active project lifecycle and manifest.
/// </summary>
public class ProjectSubsystem : IEngineSubsystem
{
    public int Priority => 0; // High priority, load manifest first
    public EnginePhase InitPhase => EnginePhase.PreInit;

    public ProjectManifest? ActiveProject { get; private set; }
    public string ProjectPath { get; private set; } = string.Empty;

    public void Initialize()
    {
        KernelLog.Info("[ProjectSubsystem] Initializing...");
        
        // Search for project file in current directory or startup path
        string baseDir = AppContext.BaseDirectory;
        string projectFile = Path.Combine(baseDir, "Project.arisen");
        
        if (File.Exists(projectFile))
        {
            LoadProject(projectFile);
        }
        else
        {
            KernelLog.Info("[ProjectSubsystem] No Project.arisen found in application directory.");
        }
    }

    public void LoadProject(string path)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string json = File.ReadAllText(path);
            ActiveProject = JsonSerializer.Deserialize<ProjectManifest>(json, options);
            ProjectPath = Path.GetDirectoryName(path) ?? string.Empty;
            KernelLog.Info($"[ProjectSubsystem] Loaded Project: {ActiveProject?.Name} from {path}");
        }
        catch (Exception e)
        {
            KernelLog.Info($"[ProjectSubsystem] Failed to load project at {path}: {e.Message}");
        }
    }

    public void Shutdown()
    {
        ActiveProject = null;
    }

    public void Dispose() => Shutdown();
}

