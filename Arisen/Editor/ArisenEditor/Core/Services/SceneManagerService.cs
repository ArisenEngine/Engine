using System;
using System.IO;
using ArisenEngine.Core.ECS;
using ArisenEngine.Models;
using ArisenEngine.Core.Serialization;
using ReactiveUI;
using ArisenEngine.Core.Diagnostics;

namespace ArisenEditor.Core.Services;

/// <summary>
/// Manages the currently open/active Scene in the Editor, allowing global access
/// to the Scene's EntityManager for ViewModels like Hierarchy and Inspector.
/// </summary>
public class SceneManagerService : ReactiveObject
{
    private static readonly Lazy<SceneManagerService> _instance = new(() => new SceneManagerService());
    public static SceneManagerService Instance => _instance.Value;

    private Scene? _activeScene;
    private string? _activeScenePath;
    private Guid _activeSceneGuid = Guid.Empty;

    public event Action? HierarchyChanged;
    
    // Fine-grained ECS Editor Events
    public event Action<Entity, string>? EntityNameChanged;
    public event Action<Entity>? EntityCreated;
    public event Action<Entity>? EntityDeleted;
    public event Action<Entity, Entity>? EntityParentChanged;
    public event Action<Entity, Type>? EntityComponentChanged;

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public void NotifyHierarchyChanged()
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => HierarchyChanged?.Invoke());
    }

    public void NotifyEntityNameChanged(Entity entity, string newName)
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntityNameChanged?.Invoke(entity, newName));
    }

    public void NotifyEntityCreated(Entity entity)
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntityCreated?.Invoke(entity));
    }

    public void NotifyEntityDeleted(Entity entity)
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntityDeleted?.Invoke(entity));
    }

    public void NotifyEntityParentChanged(Entity entity, Entity newParent)
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntityParentChanged?.Invoke(entity, newParent));
    }

    public void NotifyEntityComponentChanged(Entity entity, Type componentType)
    {
        IsDirty = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntityComponentChanged?.Invoke(entity, componentType));
    }

    /// <summary>
    /// The currently loaded Scene.
    /// </summary>
    public Scene? ActiveScene
    {
        get => _activeScene;
        private set => this.RaiseAndSetIfChanged(ref _activeScene, value);
    }
    
    public string? ActiveScenePath => _activeScenePath;

    public Guid ActiveSceneGuid => _activeSceneGuid;

    private SceneManagerService() { }

    /// <summary>
    /// Opens an existing scene from the given absolute file path.
    /// </summary>
    public bool LoadScene(string path)
    {
        if (!File.Exists(path))
        {
            EditorLog.Error($"[SceneManager] Cannot load scene. File not found: {path}");
            return false;
        }

        try
        {
            var loadedScene = new Scene { Name = Path.GetFileNameWithoutExtension(path) };
            SceneSerializer.LoadScene(path, loadedScene.Registry);
            
            _activeScenePath = path;
            ActiveScene = loadedScene;
            IsDirty = false;
            
            // Resolve Guid and save to UserSettings
            _activeSceneGuid = AssetDatabaseService.Instance.GetGuidFromPath(path);
            if (_activeSceneGuid != Guid.Empty)
            {
                EditorProjectService.Instance.UserSettings.LastOpenedSceneGuid = _activeSceneGuid;
                EditorProjectService.Instance.SaveUserSettings();
            }

            EditorLog.Log($"[SceneManager] Loaded scene '{loadedScene.Name}' from {path}");
            return true;
        }
        catch (Exception ex)
        {
            EditorLog.Error($"[SceneManager] Failed to load scene {path}. Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Opens an existing scene from the given Guid.
    /// </summary>
    public bool LoadScene(Guid guid)
    {
        string? path = AssetDatabaseService.Instance.GetPathFromGuid(guid);
        if (string.IsNullOrEmpty(path))
        {
            EditorLog.Error($"[SceneManager] Cannot load scene. Guid {guid} not found in AssetDatabase.");
            return false;
        }

        return LoadScene(path);
    }

    /// <summary>
    /// Saves the active scene to its original path.
    /// </summary>
    public bool SaveCurrentScene()
    {
        if (ActiveScene == null || string.IsNullOrEmpty(_activeScenePath))
        {
            EditorLog.Error("[SceneManager] No active scene or path to save.");
            return false;
        }

        try
        {
            SceneSerializer.SaveScene(_activeScenePath, ActiveScene.Registry);
            IsDirty = false;
            EditorLog.Log($"[SceneManager] Saved scene to {_activeScenePath}");
            return true;
        }
        catch (Exception ex)
        {
            EditorLog.Error($"[SceneManager] Failed to save scene to {_activeScenePath}. Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Creates a new, empty Scene in memory. It must be saved to disk manually to persist.
    /// </summary>
    public Scene CreateNewScene(string name = "New Scene")
    {
        var newScene = new Scene { Name = name };
        ActiveScene = newScene;
        _activeScenePath = null;
        _activeSceneGuid = Guid.Empty;
        IsDirty = false;
        return newScene;
    }
}
