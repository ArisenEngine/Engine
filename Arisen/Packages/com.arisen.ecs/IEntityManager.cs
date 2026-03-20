namespace ArisenEngine.Core.ECS;

/// <summary>
/// Contract for the Entity Manager. 
/// Packages use this to query the world state or manipulate entities 
/// without directly depending on the ECS implementation.
/// </summary>
public interface IEntityManager
{
    /// <summary>
    /// Creates a new Entity, optionally reusing a freed ID.
    /// </summary>
    Entity CreateEntity();

    /// <summary>
    /// Destroys the entity and removes all associated components across all pools.
    /// </summary>
    void DestroyEntity(Entity entity);

    /// <summary>
    /// Adds or updates a component onto the given entity.
    /// </summary>
    void AddComponent<T>(Entity entity, in T component = default) where T : struct, IComponent;

    /// <summary>
    /// Checks if the entity has the given component type.
    /// </summary>
    bool HasComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>
    /// Removes the component from the entity.
    /// </summary>
    void RemoveComponent<T>(Entity entity) where T : struct, IComponent;

    // Note: ref returning methods (GetComponent<T>) are currently omitted from the interface 
    // to strictly preserve the interface abstraction, as C# interface generic ref returns 
    // can complicate COM/ABI bounds or require specific struct layouts. 
    // High-performance reads/writes should be done via ComponentPool array spans directly.
}
