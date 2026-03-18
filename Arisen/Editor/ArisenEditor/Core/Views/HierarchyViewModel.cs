using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ArisenEditor.Core.Commands;
using ArisenEditorFramework.Commands;
using ArisenEditorFramework.Core;
using ArisenEditorFramework.Services;
using ArisenEditorFramework.UI.Menus;
using ArisenEngine.Core.ECS;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

public class SceneNodeViewModel : ReactiveObject
{
    private string m_Name;
    public string Name
    {
        get => m_Name;
        set => this.RaiseAndSetIfChanged(ref m_Name, value);
    }
    
    private bool m_IsExpanded = true;
    public bool IsExpanded
    {
        get => m_IsExpanded;
        set => this.RaiseAndSetIfChanged(ref m_IsExpanded, value);
    }

    public ObservableCollection<EntityNodeViewModel> Entities { get; } = new();

    public SceneNodeViewModel(string name)
    {
        m_Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Scene" : name;
    }
}

public class EntityNodeViewModel : ReactiveObject
{
    public Entity Entity { get; }
    
    private string m_Name = "New Entity";
    public string Name
    {
        get => m_Name;
        set 
        {
            if (m_Name != value)
            {
                var oldName = m_Name;
                this.RaiseAndSetIfChanged(ref m_Name, value);
                CommandHistory.Instance.Execute(new RenameEntityCommand(Entity, oldName, value));
            }
        }
    }
    
    private bool m_IsRenaming;
    public bool IsRenaming
    {
        get => m_IsRenaming;
        set => this.RaiseAndSetIfChanged(ref m_IsRenaming, value);
    }
    
    private bool m_IsExpanded = true;
    public bool IsExpanded
    {
        get => m_IsExpanded;
        set => this.RaiseAndSetIfChanged(ref m_IsExpanded, value);
    }

    public ObservableCollection<EntityNodeViewModel> Children { get; } = new();
    
    public EntityNodeViewModel(Entity entity, string name)
    {
        Entity = entity;
        // Set the backing field directly to avoid triggering the Name setter,
        // which fires a RenameEntityCommand and causes an infinite RefreshHierarchy loop.
        m_Name = string.IsNullOrWhiteSpace(name) ? $"Entity {entity.Id}" : name;
    }
}

internal class HierarchyViewModel : EditorPanelBase
{
    private ObservableCollection<EntityNodeViewModel> m_AllEntities = new();
    private ObservableCollection<SceneNodeViewModel> m_RootNodes = new();
    private readonly CompositeDisposable m_Disposables = new();

    private string m_SearchText = string.Empty;
    public string SearchText
    {
        get => m_SearchText;
        set 
        {
            this.RaiseAndSetIfChanged(ref m_SearchText, value);
            ApplyFilter();
        }
    }

    public ObservableCollection<MenuAction> CreateActions { get; } = new();
    public ObservableCollection<MenuAction> ContextActions { get; } = new();

    public EntityManager? ActiveEntityManager { get; set; }
    public ArisenEditor.Core.Services.SelectionService SelectionService { get; set; } = null!;

    public override string Title => "Hierarchy";
    public override string Id => "Hierarchy";

    public override object Content => new Views.HierarchyView { DataContext = this };

    public ObservableCollection<SceneNodeViewModel> RootNodes
    {
        get => m_RootNodes;
        set => this.RaiseAndSetIfChanged(ref m_RootNodes, value);
    }

    private ReactiveObject? m_SelectedItem;
    public ReactiveObject? SelectedItem
    {
        get => m_SelectedItem;
        set => this.RaiseAndSetIfChanged(ref m_SelectedItem, value);
    }

    internal HierarchyViewModel()
    {
        // Register default provider (In a real app, this happens in bootstrapper)
        MenuRegistry.Instance.RegisterProvider(new ArisenEditor.Core.Services.HierarchyMenuProvider());
        
        // Populate menus
        RefreshMenus();

        this.WhenAnyValue(x => x.SelectedItem)
            .Subscribe(_ => RefreshMenus(SelectedItem));
            
        // Subscribe to SceneManagerService to auto-refresh when the active scene changes
        ArisenEditor.Core.Services.SceneManagerService.Instance
            .WhenAnyValue(x => x.ActiveScene)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(scene => 
            {
                if (scene != null)
                {
                    RefreshHierarchy(scene.Registry);
                }
                else
                {
                    m_AllEntities.Clear();
                    RootNodes.Clear();
                    ActiveEntityManager = null;
                }
            })
            .DisposeWith(m_Disposables);

        ArisenEditor.Core.Services.SceneManagerService.Instance.HierarchyChanged += () =>
        {
            if (ActiveEntityManager != null)
            {
                RefreshHierarchy(ActiveEntityManager);
            }
        };

        ArisenEditor.Core.Services.SceneManagerService.Instance.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(ArisenEditor.Core.Services.SceneManagerService.IsDirty) ||
                args.PropertyName == nameof(ArisenEditor.Core.Services.SceneManagerService.ActiveScene))
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    if (RootNodes.Count > 0)
                    {
                        var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
                        var sceneName = svc.ActiveScene?.Name ?? "Unnamed Scene";
                        if (svc.IsDirty) sceneName += "*";
                        RootNodes[0].Name = sceneName;
                    }
                });
            }
        };
    }

    public void RefreshMenus(object? context = null)
    {
        CreateActions.Clear();
        foreach (var item in MenuRegistry.Instance.BuildMenu("Hierarchy.CreateMenu", context))
            CreateActions.Add(item);

        ContextActions.Clear();
        foreach (var item in MenuRegistry.Instance.BuildMenu("Hierarchy.ContextMenu", context))
            ContextActions.Add(item);
    }

    private void ApplyFilter()
    {
        var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
        var sceneName = svc.ActiveScene?.Name ?? "Unnamed Scene";
        if (svc.IsDirty) sceneName += "*";
        
        var sceneNode = new SceneNodeViewModel(sceneName);

        if (string.IsNullOrWhiteSpace(m_SearchText))
        {
            foreach (var e in m_AllEntities)
                sceneNode.Entities.Add(e);
        }
        else
        {
            foreach (var e in m_AllEntities.Where(en => en.Name.Contains(m_SearchText, StringComparison.OrdinalIgnoreCase)))
                sceneNode.Entities.Add(e);
            sceneNode.IsExpanded = true;
        }

        RootNodes = new ObservableCollection<SceneNodeViewModel> { sceneNode };
    }

    public void RefreshHierarchy(EntityManager entityManager)
    {
        ActiveEntityManager = entityManager;
        m_AllEntities.Clear();
        
        if (!entityManager.HasPool<NameComponent>())
        {
            ApplyFilter();
            return;
        }

        var namePool = entityManager.GetPool<NameComponent>();
        var components = namePool.GetRawComponentArray();
        var entities = namePool.GetRawEntityArray();
        int count = namePool.Count;

        var entityMap = new System.Collections.Generic.Dictionary<Entity, EntityNodeViewModel>();

        for (int i = 0; i < count; i++)
        {
            ref NameComponent nameComp = ref components[i];
            var node = new EntityNodeViewModel(entities[i], nameComp.Name);
            m_AllEntities.Add(node);
            entityMap[entities[i]] = node;
        }
        
        // Build hierarchy
        var rootEntities = new System.Collections.Generic.List<EntityNodeViewModel>();
        
        foreach (var node in m_AllEntities)
        {
            if (entityManager.HasComponent<ParentComponent>(node.Entity))
            {
                var parentComp = entityManager.GetComponent<ParentComponent>(node.Entity);
                if (parentComp.Parent != Entity.Null && entityMap.TryGetValue(parentComp.Parent, out var parentNode))
                {
                    parentNode.Children.Add(node);
                    continue;
                }
            }
            rootEntities.Add(node);
        }

        m_AllEntities = new ObservableCollection<EntityNodeViewModel>(rootEntities);
        
        ApplyFilter();
    }

    public void MoveEntity(EntityNodeViewModel source, EntityNodeViewModel? targetParent)
    {
        var em = ActiveEntityManager;
        if (em == null) return;
        
        var srcEntity = source.Entity;
        var newParentEntity = targetParent?.Entity ?? Entity.Null;

        if (srcEntity == newParentEntity) return;

        // Check if newParentEntity is a child of srcEntity to prevent cycles
        var current = newParentEntity;
        while (current != Entity.Null && em.HasComponent<ParentComponent>(current))
        {
            if (current == srcEntity) return; // Cycle detected
            current = em.GetComponent<ParentComponent>(current).Parent;
        }

        // Check if already at the target parent
        if (em.HasComponent<ParentComponent>(srcEntity))
        {
            var oldParent = em.GetComponent<ParentComponent>(srcEntity).Parent;
            if (oldParent == newParentEntity) return;
        }

        CommandHistory.Instance.Execute(new MoveEntityCommand(srcEntity, newParentEntity, em));
        RefreshHierarchy(em);
    }

    internal void OnUnloaded()
    {
        m_Disposables.Dispose();
    }
}
