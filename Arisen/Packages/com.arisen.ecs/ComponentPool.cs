using ArisenKernel.Contracts;
using ArisenEngine.Core.ECS;
using ArisenEngine.Core.Automation;
using ArisenEngine.Rendering;
using System.Numerics;
using System;
using System.Runtime.CompilerServices;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// Non-generic interface to allow the EntityManager to store generic pools in a single collection.
/// </summary>
public interface IComponentPool
{
    bool Has(Entity entity);
    void Remove(Entity entity);
    void Clear();
    Type GetComponentType();
    object GetBoxed(Entity entity);
    void SetBoxed(Entity entity, object component);
    IntPtr GetAddress(Entity entity);
}

/// <summary>
/// A Sparse Set implementation that stores all components of type T in a contiguous memory array.
/// This maximizes CPU Cache hits and enables high-performance multi-threading.
/// </summary>
public class ComponentPool<T> : IComponentPool where T : struct, IComponent
{
    private int[] m_Sparse; // Entity ID -> Dense Index
    private Entity[] m_Dense; // Dense Index -> Entity
    private T[] m_Components; // Dense Index -> Component Data
    private int m_Count;

    public int Count => m_Count;
    public Type GetComponentType() => typeof(T);
    public object GetBoxed(Entity entity) => Get(entity);
    public void SetBoxed(Entity entity, object component) => Add(entity, (T)component);

    public unsafe IntPtr GetAddress(Entity entity)
    {
        fixed (T* ptr = &m_Components[m_Sparse[entity.Id]])
        {
            return (IntPtr)ptr;
        }
    }

    public ComponentPool(int capacity = 128)
    {
        m_Sparse = new int[capacity];
        Array.Fill(m_Sparse, -1);
        m_Dense = new Entity[capacity];
        m_Components = new T[capacity];
        m_Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(Entity entity)
    {
        return entity.Id >= 0 && entity.Id < m_Sparse.Length && m_Sparse[entity.Id] != -1;
    }

    public ref T Add(Entity entity, in T component = default)
    {
        EnsureSparseCapacity(entity.Id);

        if (Has(entity))
        {
            // Already exists, just update it
            m_Components[m_Sparse[entity.Id]] = component;
            return ref m_Components[m_Sparse[entity.Id]];
        }

        if (m_Count >= m_Dense.Length)
        {
            EnsureDenseCapacity(m_Count * 2);
        }

        int denseIndex = m_Count;
        m_Sparse[entity.Id] = denseIndex;
        m_Dense[denseIndex] = entity;
        m_Components[denseIndex] = component;
        m_Count++;

        return ref m_Components[denseIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(Entity entity)
    {
        if (!Has(entity))
            throw new Exception($"Entity {entity.Id} does not have component {typeof(T).Name}");

        return ref m_Components[m_Sparse[entity.Id]];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(Entity entity) => ref Get(entity);

    public void Remove(Entity entity)
    {
        if (!Has(entity)) return;

        int denseIndex = m_Sparse[entity.Id];
        int lastDenseIndex = m_Count - 1;

        // If we are not removing the last element, swap the last element into the removed slot
        // to keep the dense array contiguous.
        if (denseIndex < lastDenseIndex)
        {
            Entity lastEntity = m_Dense[lastDenseIndex];
            m_Dense[denseIndex] = lastEntity;
            m_Components[denseIndex] = m_Components[lastDenseIndex];
            m_Sparse[lastEntity.Id] = denseIndex;
        }

        m_Sparse[entity.Id] = -1;
        m_Count--;
    }

    public void Clear()
    {
        Array.Fill(m_Sparse, -1);
        m_Count = 0;
    }

    /// <summary>
    /// Returns the raw contiguous array of components. Do not access elements beyond Count.
    /// Ideal for Parallel.For loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] GetRawComponentArray() => m_Components;

    /// <summary>
    /// Returns the raw array of entities matching the components array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Entity[] GetRawEntityArray() => m_Dense;

    private void EnsureSparseCapacity(int entityId)
    {
        if (entityId >= m_Sparse.Length)
        {
            int newSize = System.Math.Max(m_Sparse.Length * 2, entityId + 1);
            int[] newSparse = new int[newSize];
            Array.Fill(newSparse, -1);
            Array.Copy(m_Sparse, newSparse, m_Sparse.Length);
            m_Sparse = newSparse;
        }
    }

    private void EnsureDenseCapacity(int size)
    {
        Array.Resize(ref m_Dense, size);
        Array.Resize(ref m_Components, size);
    }
}


