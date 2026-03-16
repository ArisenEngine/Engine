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
    }
}
