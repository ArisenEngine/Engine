using NebulaEngine;
using ReactiveUI;

namespace NebulaEditor.ViewModels;

public class RenderSurfaceViewModel : ViewModelBase
{
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

    private Engine m_Engine;

    public void SetEngine(Engine engine)
    {
        m_Engine = engine;
    }

    public int FPS => m_Engine != null ? m_Engine.FPS : 0;

}