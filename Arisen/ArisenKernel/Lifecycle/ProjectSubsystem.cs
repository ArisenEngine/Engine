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
    public string ProjectDir { get; private set; } = string.Empty;

    public void Initialize()
    {
        KernelLog.Info("[ProjectSubsystem] Early initialization phase complete.");
    }

    /// <summary>
    /// Loads the project context from a standardized workspace directory containing a manifest.json.
    /// </summary>
    public void LoadFromWorkspace(string workspacePath)
    {
        try
        {
            string absPath = Path.GetFullPath(workspacePath);
            string manifestPath = Path.Combine(absPath, "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                KernelLog.Warning($"[ProjectSubsystem] No manifest.json found at {absPath}.");
                return;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string json = File.ReadAllText(manifestPath);
            ActiveProject = JsonSerializer.Deserialize<ProjectManifest>(json, options);
            ProjectDir = absPath;
            KernelLog.Info($"[ProjectSubsystem] Unified Project Context Established: {ActiveProject?.Name} at {ProjectDir}");
        }
        catch (Exception e)
        {
            KernelLog.Error($"[ProjectSubsystem] Failed to establish project context: {e.Message}");
        }
    }

    public void Shutdown()
    {
        ActiveProject = null;
        ProjectDir = string.Empty;
    }

    public void Dispose() => Shutdown();
}

