namespace ArisenKernel.Contracts;

/// <summary>
/// Defines the main entry point loop that takes over the application thread after bootup.
/// </summary>
[ServiceContract("Application Host", "Defines the main entry point loop that takes over the application thread.")]
public interface IApplicationHost
{
    /// <summary>
    /// Gets whether subsystem phases must initialize before this host takes over the main thread.
    /// Package-oriented tooling and test hosts can return false when package mounting alone provides
    /// every service they need.
    /// </summary>
    bool RequiresEngineInitialization => true;

    /// <summary>
    /// Executes the main application loop, blocking the host's main thread.
    /// </summary>
    void Run(string[] args);
}
