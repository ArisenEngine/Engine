using System;
using ArisenEngine.Core.Lifecycle;
using ArisenEngine.Core.Serialization;
using ArisenEngine.Core.Diagnostics;
using System.IO;
using ArisenEditor.Core.Models;

namespace ArisenEditor.Core.Services;

/// <summary>
/// Service for managing the active project's settings and manifest within the editor.
/// </summary>
public class EditorProjectService
{
    private static readonly Lazy<EditorProjectService> _instance = new(() => new EditorProjectService());
    public static EditorProjectService Instance => _instance.Value;

    public ProjectManifest? ActiveProject => EngineKernel.Instance.GetSubsystem<ProjectSubsystem>()?.ActiveProject;

    public EditorUserSettings UserSettings { get; private set; } = new();

    private EditorProjectService() 
    {
    }

    public void SaveProject()
    {
        var manifest = ActiveProject;
        if (manifest == null) return;

        var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
        string projectFile = Path.Combine(env?.ProjectRoot ?? string.Empty, "Project.arisen");
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

    public void LoadUserSettings()
    {
        var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
        string libraryPath = Path.Combine(env?.ProjectRoot ?? string.Empty, "Library");
        string settingsPath = Path.Combine(libraryPath, "EditorUserSettings.arisen_settings");

        if (File.Exists(settingsPath))
        {
            try
            {
                UserSettings = SerializationUtil.Deserialize<EditorUserSettings>(settingsPath);
            }
            catch (Exception ex)
            {
                Logger.Error($"[EditorProjectService] Failed to load user settings: {ex.Message}");
                UserSettings = new EditorUserSettings();
            }
        }
        else
        {
            UserSettings = new EditorUserSettings();
        }
    }

    public void SaveUserSettings()
    {
        var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
        string libraryPath = Path.Combine(env?.ProjectRoot ?? string.Empty, "Library");
        
        if (!Directory.Exists(libraryPath))
        {
            Directory.CreateDirectory(libraryPath);
        }

        string settingsPath = Path.Combine(libraryPath, "EditorUserSettings.arisen_settings");
        
        try
        {
            SerializationUtil.Serialize(UserSettings, settingsPath);
        }
        catch (Exception ex)
        {
            Logger.Error($"[EditorProjectService] Failed to save user settings: {ex.Message}");
        }
    }
}
