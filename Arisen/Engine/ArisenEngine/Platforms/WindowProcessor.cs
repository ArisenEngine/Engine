using ArisenEngine.Rendering;
using CppSharp.Types.Std;

namespace ArisenEngine.Platforms;

internal abstract class WindowProcessor
{
    protected RenderSurface m_RenderSurface;
    internal WindowProcessor(RenderSurface renderSurface)
    {
        m_RenderSurface = renderSurface;
    }
    
    protected IntPtr m_ProcPtr;
    public IntPtr ProcPtr => m_ProcPtr;

    protected IntPtr m_ResizeCallbackPtr;
    public IntPtr ResizeCallbackPtr => m_ResizeCallbackPtr;
    
    protected abstract void OnResizing();

    protected abstract void OnResized();

    protected abstract void OnCreate();

    protected abstract void OnDestroy();

    protected abstract void OnClose();
}