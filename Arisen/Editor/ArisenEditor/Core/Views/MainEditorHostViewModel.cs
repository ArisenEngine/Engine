using ArisenEditor.Core.Factory;
using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class MainEditorHostViewModel : ReactiveObject
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
    
    internal MainEditorHostViewModel(ArisenEditorFramework.Core.IPanelFactory? panelFactory = null)
    {
        m_LayoutManager = new ArisenEditorFramework.Docking.LayoutManager();
        if (panelFactory != null)
        {
            m_LayoutManager.PanelFactory = panelFactory;
        }
        
        m_LayoutManager.Initialize();
        Layout = m_LayoutManager.Layout;
        
        m_LayoutManager.LayoutRefresh += (newLayout) => { Layout = newLayout; };
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