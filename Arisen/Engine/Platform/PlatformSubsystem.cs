using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Platform;

public class PlatformSubsystem : ITickableSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    private IMessageHandler m_MessageHandler;

    public void Initialize()
    {
        switch (ArisenApplication.s_Platform)
        {
            case RuntimePlatform.Windows:
                // m_MessageHandler = new WindowsMessageHandle();
                break;
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
