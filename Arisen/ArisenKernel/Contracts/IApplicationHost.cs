namespace ArisenKernel.Contracts;

/// <summary>
/// Implemented by packages that require taking over the main thread (e.g., Avalonia UI or specialized windowing).
/// </summary>
public interface IApplicationHost
{
    /// <summary>
    /// Executes the main application loop, blocking the host's main thread.
    /// </summary>
    void Run(string[] args);
}
