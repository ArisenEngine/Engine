using ArisenEngine.Core.Lifecycle;

namespace ArisenEngine.Rendering;

public class RenderSubsystem : ITickableSubsystem
{
    // Rendering should typically happen last in the frame
    public int Priority => 100; 
    public EnginePhase InitPhase => EnginePhase.Init;

    public void Initialize()
    {
    }

    public void Tick(float deltaTime)
    {
        RenderPipelineManager.DoRenderLoop(Graphics.currentRenderPipelineAsset);
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
        Shutdown();
    }
}
