using ArisenEditor.Core.Factory;
using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class MainDockViewModel : ReactiveObject
{
    private readonly ArisenEditorFramework.Docking.LayoutManager m_LayoutManager;
    private IRootDock? m_Layout;
    
    public IRootDock? Layout
    {
        get => m_Layout;
        set { this.RaiseAndSetIfChanged(ref m_Layout, value); }
    }

    public string ProjectName => ArisenEditor.Core.EditorProjectContext.Instance.CurrentProject.Name;
    public string ProjectPath => ArisenEditor.Core.EditorProjectContext.Instance.CurrentProject.ProjectPath;

    private string m_SearchText = string.Empty;
    public string SearchText
    {
        get => m_SearchText;
        set => this.RaiseAndSetIfChanged(ref m_SearchText, value);
    }

    private string m_StatusText = "Engine Ready";
    public string StatusText
    {
        get => m_StatusText;
        set => this.RaiseAndSetIfChanged(ref m_StatusText, value);
    }
    
    public IReactiveCommand PlayCommand { get; }
    public IReactiveCommand PauseCommand { get; }
    public IReactiveCommand StopCommand { get; }
    public IReactiveCommand BuildCommand { get; }
    public IReactiveCommand SaveSceneCommand { get; }
    
    public MenuItemBarViewModel MenuViewModel { get; }
    public Dock.Model.Core.IFactory Factory => m_LayoutManager.Factory;
    
    internal MainDockViewModel(ArisenEditorFramework.Core.IPanelFactory? panelFactory = null)
    {
        MenuViewModel = new MenuItemBarViewModel();
        m_LayoutManager = new ArisenEditorFramework.Docking.LayoutManager();
        if (panelFactory != null)
        {
            m_LayoutManager.PanelFactory = panelFactory;
        }
        
        m_LayoutManager.Initialize();
        Layout = m_LayoutManager.Layout;
        
        m_LayoutManager.LayoutRefresh += (newLayout) => { Layout = newLayout; };

        PlayCommand = ReactiveCommand.Create(() => StatusText = "Running...");
        PauseCommand = ReactiveCommand.Create(() => StatusText = "Paused");
        StopCommand = ReactiveCommand.Create(() => StatusText = "Engine Ready");
        BuildCommand = ReactiveCommand.Create(() => StatusText = "Building...");
        
        SaveSceneCommand = ReactiveCommand.Create(() =>
        {
            var svc = ArisenEditor.Core.Services.SceneManagerService.Instance;
            if (svc.IsDirty && svc.ActiveScene != null)
            {
                svc.SaveCurrentScene();
            }
        });
    }
    
    public void CloseLayout()
    {
        if (Layout is IDock dock)
        {
            if (dock.Close.CanExecute(null))
            {
                dock.Close.Execute(null);
            }
        }
    }
}