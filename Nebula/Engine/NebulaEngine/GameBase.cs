using NebulaEngine.Graphics;

namespace NebulaEngine;

internal abstract class GameBase
{
    protected RenderSurface m_RenderSurface;

    protected bool m_IsRunning = false;

    public abstract void Run();
}