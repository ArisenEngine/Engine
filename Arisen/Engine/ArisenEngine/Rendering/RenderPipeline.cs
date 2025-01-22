namespace ArisenEngine.Rendering;

public abstract class RenderPipeline : IDisposable
{
    internal bool disposed;
    /// <summary>
    /// TODO: add cameras as parameters
    /// </summary>
    protected abstract void Render();
    protected abstract void OnDisposed();
    
    public void Dispose()
    {
        OnDisposed();
        disposed = true;
    }

    internal void InternalRender()
    {
        Render();
    }
}