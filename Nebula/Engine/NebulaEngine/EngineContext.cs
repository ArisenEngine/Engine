using NebulaEngine.Platforms;

namespace NebulaEngine;

public class EngineContext : IDisposable
{
    private IMessageLoop m_MessageLoop;
    public EngineContext(IntPtr handle)
    {
        switch (GameApplication.platform)
        {
            case RuntimePlatform.Windows:
                m_MessageLoop = new WindowsMessageLoop(handle);
                break;
            default:
                throw new Exception($"unsupported platform type : {GameApplication.platform}");
        }
    }

    public void Dispose()
    {
        if (m_MessageLoop != null)
        {
            m_MessageLoop.Dispose();
        }
    }

    public bool NextFrame()
    {
        if (m_MessageLoop != null)
        {
            return m_MessageLoop.NextFrame();
        }

        return false;
    }
}