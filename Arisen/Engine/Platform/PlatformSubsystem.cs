using ArisenEngine.Core.Diagnostics;
using ArisenEngine.Core.Lifecycle;
using ArisenKernel.Lifecycle;

namespace ArisenEngine.Platform;

public class PlatformSubsystem : ITickableSubsystem
{
    public int Priority => 10;
    public EnginePhase InitPhase => EnginePhase.Init;

    private IMessageHandler m_MessageHandler;

    public void Initialize()
    {
        using var _ = Profiler.Zone("PlatformSubsystem.Initialize");
        var env = EngineKernel.Instance.GetSubsystem<EnvironmentSubsystem>();
        switch (env?.Platform)
        {
            case RuntimePlatform.Windows:
#if !ARISEN_EDITOR
                    // Only standalone runtime games own the OS message loop.
                    // In Editor mode, Avalonia owns the message loop.
                    // m_MessageHandler = new WindowsMessageHandle(); // Note: currently commented out in base code anyway
#endif
                break;
        }
    }

    public void Tick(float deltaTime)
    {
        using var _ = Profiler.Zone("PlatformSubsystem.Tick");
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
