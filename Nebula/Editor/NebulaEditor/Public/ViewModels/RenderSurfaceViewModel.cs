using NebulaEngine;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public class RenderSurfaceViewModel : ViewModelBase
{
    private bool m_IsSceneView = false;
    public bool IsGameView => !m_IsSceneView;
    
    private bool m_StatsToggleChecked;

    public bool StatsToggleChecked
    {
        get
        {
            return m_StatsToggleChecked;
        }
        set
        {
            this.RaiseAndSetIfChanged(ref m_StatsToggleChecked, value);
        }
    }

    public RenderSurfaceViewModel(bool isSceneView)
    {
        m_IsSceneView = isSceneView;
    }
    
    // FOR designer preview
    public RenderSurfaceViewModel()
    {
        m_IsSceneView = false;
    }

}