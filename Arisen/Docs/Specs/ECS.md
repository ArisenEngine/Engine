# Entity Component System (ECS) Spec

The Arisen Engine uses a strict Data-Oriented Sparse-Set based ECS.

## Entities
Entities are simply 32-bit integer IDs wrapped in a struct.
```csharp
public readonly struct Entity
{
    public readonly int Id;
}
```

## Component Pools
All components belong to a `ComponentPool<T>`. 
- **Contiguous Memory**: `ComponentPool<T>` stores all instances of `T` in a contiguous array `T[] m_Components`.
- **Sparse Set Mapping**: Lookups use a Sparse Array (`m_Sparse`) for $O(1)$ access from Entity ID -> Dense Index.

## Writing Fast Systems
When writing a System that processes components, you **MUST NOT** iterate entities one by one calling `pool.Get(entity)`. 

Instead, you **MUST** grab the raw component array for bulk operations:
```csharp
var pool = entityManager.GetPool<TransformComponent>();
var components = pool.GetRawComponentArray();
int count = pool.Count;

// THIS is the hot path. Keep it perfectly linear.
for(int i = 0; i < count; i++)
{
    ref TransformComponent transform = ref components[i];
    // compute...
}
```

## Multithreading
Use `Parallel.For` over the raw array intervals. Because components are pure structs, thread safety is guaranteed as long as threads write to mutually exclusive indices of the `m_Components` array.
