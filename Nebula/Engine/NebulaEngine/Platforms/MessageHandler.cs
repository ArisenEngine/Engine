using NebulaEngine.Graphics;

namespace NebulaEngine.Platforms;

internal abstract class MessageHandler
{
    protected IntPtr m_CallbackPtr = IntPtr.Zero;
    protected IRenderSurface m_RenderSurface;

    public MessageHandler(IRenderSurface renderSurface)
    {
        m_RenderSurface = renderSurface;
    }
    public IntPtr CallbackPtr => m_CallbackPtr;

    protected abstract void OnResizing();

    protected abstract void OnResized();

    protected abstract void OnCreate();

    protected abstract void OnDestroy();
    
    public abstract bool NextFrame();
}