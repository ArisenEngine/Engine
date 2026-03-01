namespace ArisenEngine.Rendering;

public abstract class RenderPipeline : IDisposable
{
    internal bool disposed;

    /// <summary>
    /// Entry point for the render pipeline to execute its drawing logic.
    /// </summary>
    protected abstract void Render(RenderContext context, Camera[] cameras);

    protected abstract void OnDisposed();

    public void Dispose()
    {
        OnDisposed();
        disposed = true;
    }

    internal void InternalRender(RenderContext context, Camera[] cameras)
    {
        Render(context, cameras);
    }
}