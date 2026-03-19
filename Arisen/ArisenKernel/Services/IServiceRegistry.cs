using System;

namespace ArisenKernel.Services;

/// <summary>
/// The central locator for cross-package communication in the Microkernel architecture.
/// Packages register their public C# interface implementations here, and other packages query them.
/// This prevents packages from holding concrete references to one another.
/// </summary>
public interface IServiceRegistry
{
    /// <summary>
    /// Registers a concrete instance as a provider for interface T.
    /// </summary>
    void RegisterService<T>(T service);

    /// <summary>
    /// Retrieves the registered provider for interface T. Throws if not found.
    /// </summary>
    T GetService<T>();

    /// <summary>
    /// Attempts to retrieve the registered provider for interface T.
    /// </summary>
    bool TryGetService<T>(out T service);
}
