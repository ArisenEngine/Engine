using ArisenKernel.Services;

namespace ArisenKernel.Contracts;

/// <summary>
/// Selected rendering backend provider for the active workspace/profile.
/// </summary>
[ServiceContract("RHI Backend", "Initializes the selected graphics backend and registers runtime RHI services.")]
public interface IRHIBackend
{
    string Name { get; }

    bool IsInitialized { get; }

    bool Initialize(IServiceRegistry services);

    void Shutdown();
}
