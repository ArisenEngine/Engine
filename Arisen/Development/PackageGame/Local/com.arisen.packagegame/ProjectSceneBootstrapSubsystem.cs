using ArisenEngine.Core.Assets;
using ArisenEngine.Core.ECS;
using ArisenEngine.ECS.Lifecycle;
using ArisenEngine.Resources.Serialization;
using ArisenKernel.Diagnostics;
using ArisenKernel.Lifecycle;

namespace PackageGame;

public sealed class ProjectSceneBootstrapSubsystem : IEngineSubsystem
{
    public int Priority => 50;
    public EnginePhase InitPhase => EnginePhase.PostInit;

    public void Initialize()
    {
        var kernel = EngineKernel.Instance;
        var sceneSubsystem = kernel.GetSubsystem<SceneSubsystem>()
            ?? throw new InvalidOperationException("Project scene bootstrap requires SceneSubsystem.");
        var project = kernel.Services.GetService<ProjectSubsystem>().ActiveProject
            ?? throw new InvalidOperationException("Project scene bootstrap requires a loaded workspace manifest.");
        var startupScene = project.StartupScene;
        if (startupScene is not { IsValid: true })
        {
            throw new InvalidOperationException(
                "Workspace manifest must define StartupScene with a valid Guid and PackageId.");
        }

        sceneSubsystem.RegisterSystem(new MeshSystem());

        var sceneService = kernel.Services.GetService<IRuntimeSceneService>();
        var sceneRef = new AssetRef<SceneSourceAsset>(
            startupScene.Guid,
            "Scene",
            startupScene.PackageId);
        var result = sceneService.LoadScene(sceneRef);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Diagnostic);
        }

        KernelLog.InfoFormat(
            "[ProjectSceneBootstrap] Activated startup scene '{0}' ({1}) with {2} entities.",
            result.SceneName,
            startupScene.Guid,
            result.EntityCount);
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
    }
}
