
using ArisenKernel.Lifecycle;

namespace ArisenEngine.Platform;

public class PlatformSubsystem : ITickableSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    private IMessageHandler m_MessageHandler;

    public void Initialize()
    {
        if (System.OperatingSystem.IsWindows())
        {
#if !ARISEN_EDITOR
                    // Only standalone runtime games own the OS message loop.
                    // In Editor mode, Avalonia owns the message loop.
                    // m_MessageHandler = new WindowsMessageHandle(); // Note: currently commented out in base code anyway
#endif
        }
    }

    public void Tick(float deltaTime)
    {
        if (m_MessageHandler != null)
        {
            if (!m_MessageHandler.NextFrame())
            {
                EngineKernel.Instance.RequestShutdown();
            }
        }
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
        Shutdown();
    }
}
