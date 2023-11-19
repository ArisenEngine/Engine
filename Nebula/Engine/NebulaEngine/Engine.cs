using NebulaEngine.Debugger;
using NebulaEngine.Platforms;

namespace NebulaEngine;

public class Engine : IDisposable
{
    private EngineContext m_Context;

    public IntPtr MessageHandle = IntPtr.Zero;

    public int FPS = 0;
    public Engine()
    {
        
    }

    public void Run(EngineContext context)
    {
        try
        {
            m_Context = context;
            while (m_Context.NextFrame())
            {
                Logger.Log("Update");
            }
        }
        catch (Exception e)
        {
            Logger.Error(e);
            throw;
        }
    }

    public void Dispose()
    {
        m_Context.Dispose();
    }
    
    
}