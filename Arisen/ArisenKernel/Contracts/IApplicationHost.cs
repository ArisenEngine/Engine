namespace ArisenKernel.Contracts;

/// <summary>
/// Defines the main entry point loop that takes over the application thread after bootup.
/// </summary>
[ServiceContract("Application Host", "Defines the main entry point loop that takes over the application thread.")]
public interface IApplicationHost
{
    /// <summary>
    /// Executes the main application loop, blocking the host's main thread.
    /// </summary>
    void Run(string[] args);
}
