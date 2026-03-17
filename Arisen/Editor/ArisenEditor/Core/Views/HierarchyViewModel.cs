using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ArisenEditorFramework.Core;
using ArisenEditorFramework.Services;
using ArisenEditorFramework.UI.Menus;
using ArisenEngine.Core.ECS;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

public class SceneNodeViewModel : ReactiveObject
{
    public string Name { get; }
    
    private bool m_IsExpanded = true;
    public bool IsExpanded
    {
        get => m_IsExpanded;
        set => this.RaiseAndSetIfChanged(ref m_IsExpanded, value);
    }

    public ObservableCollection<EntityNodeViewModel> Entities { get; } = new();

    public SceneNodeViewModel(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Unnamed Scene" : name;
    }
}

public class EntityNodeViewModel : ReactiveObject
{
    public Entity Entity { get; }
    
    private string m_Name = "New Entity";
    public string Name
    {
        get => m_Name;
        set => this.RaiseAndSetIfChanged(ref m_Name, value);
    }
    
    public EntityNodeViewModel(Entity entity, string name)
    {
        Entity = entity;
        Name = string.IsNullOrWhiteSpace(name) ? $"Entity {entity.Id}" : name;
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
        var sceneName = ArisenEditor.Core.Services.SceneManagerService.Instance.ActiveScene?.Name ?? "Unnamed Scene";
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

        var pool = entityManager.GetPool<NameComponent>();
        var components = pool.GetRawComponentArray();
        var entities = pool.GetRawEntityArray();
        int count = pool.Count;

        for (int i = 0; i < count; i++)
        {
            ref NameComponent nameComp = ref components[i];
            m_AllEntities.Add(new EntityNodeViewModel(entities[i], nameComp.Name));
        }
        
        ApplyFilter();
    }

    internal void OnUnloaded()
    {
        m_Disposables.Dispose();
    }
}
