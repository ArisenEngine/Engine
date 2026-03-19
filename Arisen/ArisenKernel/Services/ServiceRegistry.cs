using System;
using System.Collections.Concurrent;

namespace ArisenKernel.Services;

public class ServiceRegistry : IServiceRegistry
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    public void RegisterService<T>(T service)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        
        if (!_services.TryAdd(typeof(T), service))
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is already registered.");
        }
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
}
