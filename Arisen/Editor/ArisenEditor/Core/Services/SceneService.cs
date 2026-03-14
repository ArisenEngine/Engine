using System;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Lifecycle;
using ReactiveUI;
using System.Numerics;

namespace ArisenEditor.Core.Services;

/// <summary>
/// Manages the active world/scene within the Editor.
/// It provides access to the EntityManager and handles scene-level operations.
/// </summary>
public interface ISceneService
{
    EntityManager? CurrentEntityManager { get; }
    void InitializeNewScene();
}

public class SceneService : ReactiveObject, ISceneService
{
    private EntityManager? _currentEntityManager;
    
    /// <summary>
    /// The active EntityManager for the current scene.
    /// UI components like Hierarchy and Inspector bind to this.
    /// </summary>
    public EntityManager? CurrentEntityManager
    {
        get => _currentEntityManager;
        private set => this.RaiseAndSetIfChanged(ref _currentEntityManager, value);
    }

    public void InitializeNewScene()
    {
        CurrentEntityManager = new EntityManager();
        
        // Add initial entities to populate the hierarchy for verification.
        // In a real flow, this would be loaded from a .scene file by SceneSerializer.
        var ent1 = CurrentEntityManager.CreateEntity();
        CurrentEntityManager.AddComponent(ent1, new NameComponent { Name = "Main Camera" });

        var ent2 = CurrentEntityManager.CreateEntity();
        CurrentEntityManager.AddComponent(ent2, new NameComponent { Name = "Directional Light" });

        var ent3 = CurrentEntityManager.CreateEntity();
        CurrentEntityManager.AddComponent(ent3, new NameComponent { Name = "Environment Root" });
        
        // Create a default Editor Camera Entity
        var editorCameraEntity = CurrentEntityManager.CreateEntity();
        CurrentEntityManager.AddComponent<NameComponent>(editorCameraEntity).Name = "Editor Camera";
        CurrentEntityManager.AddComponent<TransformComponent>(editorCameraEntity).Position = new Vector3(0, 0, -10);
        CurrentEntityManager.AddComponent<CameraComponent>(editorCameraEntity);
        
        // Note: For now, we are manually adding NameComponents. 
        // In the future, every entity created in Editor should probably get one by default.
    }
}
