using System;
using System.IO;
using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Serialization;

namespace ArisenEngine.Core.Lifecycle;

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
        Logger.Log("[ProjectSubsystem] Initializing...");
        
        // Search for project file in current directory or startup path
        string baseDir = AppContext.BaseDirectory;
        string projectFile = Path.Combine(baseDir, "Project.arisen");
        
        if (File.Exists(projectFile))
        {
            LoadProject(projectFile);
        }
        else
        {
            Logger.Warning("[ProjectSubsystem] No Project.arisen found in application directory.");
        }
    }

    public void LoadProject(string path)
    {
        try
        {
            ActiveProject = SerializationUtil.Deserialize<ProjectManifest>(path);
            ProjectPath = Path.GetDirectoryName(path) ?? string.Empty;
            Logger.Log($"[ProjectSubsystem] Loaded Project: {ActiveProject?.Name} from {path}");
        }
        catch (Exception e)
        {
            Logger.Error($"[ProjectSubsystem] Failed to load project at {path}: {e.Message}");
        }
    }

    public void Shutdown()
    {
        ActiveProject = null;
    }

    public void Dispose() => Shutdown();
}
