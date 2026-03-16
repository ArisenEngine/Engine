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
    private ObservableCollection<EntityNodeViewModel> m_Entities = new();
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

    public ObservableCollection<EntityNodeViewModel> Entities
    {
        get => m_Entities;
        set => this.RaiseAndSetIfChanged(ref m_Entities, value);
    }

    private EntityNodeViewModel? m_SelectedEntity;
    public EntityNodeViewModel? SelectedEntity
    {
        get => m_SelectedEntity;
        set => this.RaiseAndSetIfChanged(ref m_SelectedEntity, value);
    }

    internal HierarchyViewModel()
    {
        // Register default provider (In a real app, this happens in bootstrapper)
        MenuRegistry.Instance.RegisterProvider(new ArisenEditor.Core.Services.HierarchyMenuProvider());
        
        // Populate menus
        RefreshMenus();

        this.WhenAnyValue(x => x.SelectedEntity)
            .Subscribe(_ => RefreshMenus(SelectedEntity));
            
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
                    Entities.Clear();
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
        if (string.IsNullOrWhiteSpace(m_SearchText))
        {
            Entities = new ObservableCollection<EntityNodeViewModel>(m_AllEntities);
        }
        else
        {
            var filtered = m_AllEntities.Where(e => e.Name.Contains(m_SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            Entities = new ObservableCollection<EntityNodeViewModel>(filtered);
        }
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
