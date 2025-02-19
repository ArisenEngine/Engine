using ArisenEditor.Core.Factory;
using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class MainEditorHostViewModel : ReactiveObject
{
    private readonly DockFactory m_DockFactory = new DockFactory();
    private IRootDock? m_Layout;
    
    public IRootDock? Layout
    {
        get => m_Layout;
        set { this.RaiseAndSetIfChanged(ref m_Layout, value); }
    }
    
    internal MainEditorHostViewModel()
    {
        Layout = m_DockFactory.CreateLayout();
        if (Layout is { } root)
        {
            m_DockFactory.InitLayout(root);
        }
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