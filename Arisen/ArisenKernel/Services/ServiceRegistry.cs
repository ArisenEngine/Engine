using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace ArisenKernel.Services;

public class ServiceRegistry : IServiceRegistry
{
    internal const string KernelProviderId = "ArisenKernel";

    private readonly ConcurrentDictionary<Type, object> _services = new();
    private readonly ConcurrentDictionary<Type, ServiceRegistrationInfo> _registrationInfo = new();
    private readonly AsyncLocal<string?> _currentProviderPackageId = new();

    public IDisposable BeginPackageRegistration(string packageId)
    {
        string? previous = _currentProviderPackageId.Value;
        _currentProviderPackageId.Value = packageId;
        return new RegistrationScope(this, previous);
    }

    public void RegisterService<T>(T service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        RegisterService(typeof(T), service);
    }

    public void RegisterService(Type contractType, object service)
    {
        if (contractType == null) throw new ArgumentNullException(nameof(contractType));
        if (service == null) throw new ArgumentNullException(nameof(service));
        if (!contractType.IsInstanceOfType(service))
        {
            throw new ArgumentException($"Service instance type '{service.GetType().FullName}' is not assignable to contract '{contractType.FullName}'.", nameof(service));
        }
        
        if (!_services.TryAdd(contractType, service))
        {
            throw new InvalidOperationException($"Service of type {contractType.Name} is already registered.");
        }

        _registrationInfo[contractType] = new ServiceRegistrationInfo(
            contractType.FullName ?? contractType.Name,
            service.GetType().FullName ?? service.GetType().Name,
            _currentProviderPackageId.Value);
    }

    public T GetService<T>()
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }
        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    public bool TryGetService<T>(out T service)
    {
        if (_services.TryGetValue(typeof(T), out var obj))
        {
            service = (T)obj;
            return true;
        }
        
        service = default!;
        return false;
    }

    public bool IsServiceRegistered(string contractName)
    {
        if (string.IsNullOrWhiteSpace(contractName)) return false;

        foreach (var serviceType in _services.Keys)
        {
            if (ServiceContractMatches(serviceType, contractName)) return true;
        }

        return false;
    }

    public IReadOnlyCollection<ServiceRegistrationInfo> GetRegisteredServices()
    {
        return _registrationInfo.Values.ToArray();
    }

    public int UnregisterServicesProvidedByPackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return 0;

        int removedCount = 0;
        foreach (var registration in _registrationInfo.ToArray())
        {
            if (!string.Equals(registration.Value.ProviderPackageId, packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_services.TryRemove(registration.Key, out _))
            {
                removedCount++;
            }

            _registrationInfo.TryRemove(registration.Key, out _);
        }

        return removedCount;
    }

    /// <summary>
    /// Clears all registered services. Used by EngineKernel.Reset().
    /// </summary>
    public void Clear()
    {
        _services.Clear();
        _registrationInfo.Clear();
        _currentProviderPackageId.Value = null;
    }

    private static bool ServiceContractMatches(Type serviceType, string contractName)
    {
        return string.Equals(serviceType.FullName, contractName, StringComparison.Ordinal)
            || string.Equals(serviceType.AssemblyQualifiedName, contractName, StringComparison.Ordinal)
            || string.Equals(serviceType.Name, contractName, StringComparison.Ordinal);
    }

    private sealed class RegistrationScope : IDisposable
    {
        private readonly ServiceRegistry _registry;
        private readonly string? _previousProviderPackageId;
        private bool _disposed;

        public RegistrationScope(ServiceRegistry registry, string? previousProviderPackageId)
        {
            _registry = registry;
            _previousProviderPackageId = previousProviderPackageId;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _registry._currentProviderPackageId.Value = _previousProviderPackageId;
            _disposed = true;
        }
    }
}
