using System;
using System.Collections.Generic;

namespace ArisenKernel.Services;

public sealed record ServiceRegistrationInfo(
    string ContractName,
    string ImplementationName,
    string? ProviderPackageId);

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
    /// Registers a concrete instance as a provider for a runtime-known contract type.
    /// </summary>
    void RegisterService(Type contractType, object service);

    /// <summary>
    /// Retrieves the registered provider for interface T. Throws if not found.
    /// </summary>
    T GetService<T>();

    /// <summary>
    /// Attempts to retrieve the registered provider for interface T.
    /// </summary>
    bool TryGetService<T>(out T service);

    /// <summary>
    /// Checks whether a service contract is registered by full name, assembly-qualified name, or short type name.
    /// </summary>
    bool IsServiceRegistered(string contractName);

    /// <summary>
    /// Returns registered service metadata for diagnostics/editor tooling.
    /// </summary>
    IReadOnlyCollection<ServiceRegistrationInfo> GetRegisteredServices();
}
