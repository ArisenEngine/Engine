using System;
using System.Collections.Generic;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// The central Registry for the ECS. It issues new Entity IDs and routes component payloads
/// to their respective contiguous ComponentPools.
/// </summary>
public class EntityManager
{
    private int m_NextEntityId = 0;
    private readonly List<int> m_FreeIds = new();
    private readonly Dictionary<Type, IComponentPool> m_ComponentPools = new();

    /// <summary>
    /// Creates a new Entity, optionally reusing an ID from a destroyed Entity.
    /// </summary>
    public Entity CreateEntity()
    {
        if (m_FreeIds.Count > 0)
        {
            int id = m_FreeIds[^1];
            m_FreeIds.RemoveAt(m_FreeIds.Count - 1);
            return new Entity(id);
        }

        return new Entity(m_NextEntityId++);
    }

    /// <summary>
    /// Destroys the entity and removes all associated components across all pools.
    /// </summary>
    public void DestroyEntity(Entity entity)
    {
        foreach (var pool in m_ComponentPools.Values)
        {
            pool.Remove(entity);
        }
        
        m_FreeIds.Add(entity.Id);
    }

    /// <summary>
    /// Adds or updates a component onto the given entity.
    /// </summary>
    public ref T AddComponent<T>(Entity entity, in T component = default) where T : struct, IComponent
    {
        var pool = GetOrCreatePool<T>();
        return ref pool.Add(entity, component);
    }

    /// <summary>
    /// Retrieves a contiguous reference to a component for raw mutation.
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        return ref GetOrCreatePool<T>().Get(entity);
    }

    /// <summary>
    /// Checks if the entity has the given component type.
    /// </summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (m_ComponentPools.TryGetValue(typeof(T), out var pool))
        {
            return pool.Has(entity);
        }
        return false;
    }

    /// <summary>
    /// Removes the component from the entity.
    /// </summary>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (m_ComponentPools.TryGetValue(typeof(T), out var pool))
        {
            pool.Remove(entity);
        }
    }

    /// <summary>
    /// Fetches the raw ComponentPool Sparse Set logic directly for ultra-fast Parallel iteration blocks.
    /// </summary>
    public ComponentPool<T> GetPool<T>() where T : struct, IComponent
    {
        return GetOrCreatePool<T>();
    }

    private ComponentPool<T> GetOrCreatePool<T>() where T : struct, IComponent
    {
        var type = typeof(T);
        if (!m_ComponentPools.TryGetValue(type, out var pool))
        {
            pool = new ComponentPool<T>();
            m_ComponentPools[type] = pool;
        }

        return (ComponentPool<T>)pool;
    }
}
