using System;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Core.Diagnostics;
using System.IO;

namespace ArisenEditor.Core.Services;

/// <summary>
/// Service for managing the active project's settings and manifest within the editor.
/// </summary>
public class EditorProjectService
{
    private static EditorProjectService? _instance;
    public static EditorProjectService Instance => _instance ??= new EditorProjectService();

    public ProjectManifest? ActiveProject => EngineKernel.Instance.GetSubsystem<ProjectSubsystem>()?.ActiveProject;

    private EditorProjectService() { }

    public void SaveProject()
    {
        var manifest = ActiveProject;
        if (manifest == null) return;

        string projectFile = Path.Combine(ArisenEngine.Core.Lifecycle.ArisenApplication.s_ProjectRoot, "Project.arisen");
        try
        {
            SerializationUtil.Serialize(manifest, projectFile);
            Logger.Log($"[EditorProjectService] Project manifest saved to {projectFile}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[EditorProjectService] Failed to save project manifest: {ex.Message}");
        }
    }

    public void SetProjectName(string name)
    {
        if (ActiveProject != null)
        {
            ActiveProject.Name = name;
            SaveProject();
        }
    }
}
