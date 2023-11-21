using NebulaEngine.Rendering;

namespace NebulaEngine.Platforms;

internal abstract class WindowProcessor
{
    protected RenderSurface m_RenderSurface;
    internal WindowProcessor(RenderSurface renderSurface)
    {
        m_RenderSurface = renderSurface;
    }
    
    protected IntPtr m_ProcPtr;
    public IntPtr ProcPtr => m_ProcPtr;
    
    protected abstract void OnResizing();

    protected abstract void OnResized();

    protected abstract void OnCreate();

    protected abstract void OnDestroy();

}