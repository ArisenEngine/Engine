using ArisenKernel.Services;

namespace ArisenKernel.Contracts;

public enum RHIBackendDiagnosticMode
{
    None = 0,
    RenderDoc = 1
}

public readonly record struct RHIBackendRestartOptions(
    RHIBackendDiagnosticMode DiagnosticMode)
{
    public static RHIBackendRestartOptions Default { get; } =
        new(RHIBackendDiagnosticMode.None);
}

/// <summary>
/// Selected rendering backend provider for the active workspace/profile.
/// </summary>
[ServiceContract("RHI Backend", "Initializes the selected graphics backend and registers runtime RHI services.")]
public interface IRHIBackend
{
    string Name { get; }

    bool IsInitialized { get; }

    ulong Generation { get; }

    bool Initialize(IServiceRegistry services);

    /// <summary>
    /// Recreates the complete graphics backend after all consumers have released
    /// resources and surfaces from the current generation.
    /// </summary>
    bool Restart(IServiceRegistry services, RHIBackendRestartOptions options);

    void Shutdown();
}
