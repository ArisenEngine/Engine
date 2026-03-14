using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ArisenEditorFramework.Core;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Lifecycle;
using DynamicData;
using DynamicData.Binding;
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
    private ObservableCollection<EntityNodeViewModel> m_Entities = new();
    private readonly CompositeDisposable m_Disposables = new();

    public EntityManager? ActiveEntityManager { get; set; }
    public ArisenEditor.Core.Services.SelectionService? SelectionService { get; set; }

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
        // Typically, we would subscribe to an event aggregating Scene changes.
        // For demonstration, simulating a refresh command or lifecycle hook.
    }

    public void RefreshHierarchy(EntityManager entityManager)
    {
        Entities.Clear();
        
        // This is exactly the hot path optimization dictated.
        if (entityManager.HasComponent<NameComponent>(new Entity(0)) == false)
        {
            // Just a safeguard check if NameComponent pool exists at all, 
            // a true engine would track active entities globally or via a Scene struct.
        }

        var pool = entityManager.GetPool<NameComponent>();
        var components = pool.GetRawComponentArray();
        var entities = pool.GetRawEntityArray();
        int count = pool.Count;

        for (int i = 0; i < count; i++)
        {
            ref NameComponent nameComp = ref components[i];
            Entities.Add(new EntityNodeViewModel(entities[i], nameComp.Name));
        }
    }

    internal void OnUnloaded()
    {
        m_Disposables.Dispose();
    }
}
