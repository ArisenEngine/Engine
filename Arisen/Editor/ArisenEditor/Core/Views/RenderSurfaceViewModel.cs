using ArisenEngine;
using ReactiveUI;

namespace ArisenEditor.ViewModels;

public class RenderSurfaceViewModel : ReactiveObject
{
    private bool m_IsSceneView = false;
    public bool IsGameView => !m_IsSceneView;
    
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