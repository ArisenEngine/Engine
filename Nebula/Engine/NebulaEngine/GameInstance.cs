using NebulaEngine.Debugger;
using NebulaEngine.Graphics;

namespace NebulaEngine;

internal sealed class GameInstance : GameBase, IDisposable
{

    private string m_Name;
    internal GameInstance(IntPtr host, string name, int width, int height)
    {
        m_Name = name;
        m_RenderSurface = new RenderSurface(host, name, width, height);
    }

    internal GameInstance(string name, int width, int height)
    {
        m_RenderSurface = new RenderSurface(IntPtr.Zero, name, width, height);
    }
    
    public static implicit operator IntPtr(GameInstance instance) => (instance.m_RenderSurface != null && instance.m_RenderSurface.IsValid()) ? instance.m_RenderSurface.Handle : IntPtr.Zero;
    
    private bool IsValid()
    {
        return m_RenderSurface.IsValid();
    }
    
    public override void Run()
    {
        if (!IsValid())
        {
            throw new Exception($"Game instance: {m_Name} is invalid.");
        }

        if (m_IsRunning)
        {
            throw new Exception($"Game instance: {m_Name} is already running.");
        }

        m_IsRunning = true;
        
        while (m_IsRunning)
        {
            while (m_RenderSurface.MessageHandler.NextFrame())
            {
                // run loop
                Logger.Log($"{m_Name} update");
            }
        }
        
    }
    
    public void Dispose()
    {
        m_IsRunning = false;
        m_RenderSurface?.Dispose();
    }
}