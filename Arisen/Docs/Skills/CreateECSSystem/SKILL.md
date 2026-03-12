---
name: create_ecs_system
description: "How to define a new ECS System operating on ComponentPools."
---

# Creating an ECS System

Systems process component data. In Arisen Engine, Systems operate on bulk data arrays pulled from `ComponentPool`.

## Rules:
1. Obtain the `ComponentPool<T>` from the `EntityManager`.
2. Do **NOT** iterate entities to call `.Get()`. This breaks CPU caches.
3. Instead, retrieve `pool.GetRawComponentArray()` and iterate sequentially up to `pool.Count`.
4. Use `ref T` to modify the structures directly in place.

## Example:

```csharp
using ArisenEngine.Core.ECS;
using ArisenEngine.Engine.Components;

namespace ArisenEngine.Engine.Systems
{
    public class PhysicsMovementSystem
    {
        private EntityManager _entityManager;

        public PhysicsMovementSystem(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        public void Update(float deltaTime)
        {
            var transformPool = _entityManager.GetPool<TransformComponent>();
            var velocityPool = _entityManager.GetPool<VelocityComponent>();

            var transforms = transformPool.GetRawComponentArray();
            var velocities = velocityPool.GetRawComponentArray();
            var entities = transformPool.GetRawEntityArray();

            int count = transformPool.Count;

            // Strict contiguous iteration. Ideal for Parallel.For
            for (int i = 0; i < count; i++)
            {
                Entity entity = entities[i];

                // Ensure the entity actually posesses both components
                if (velocityPool.Has(entity))
                {
                    ref TransformComponent transform = ref transforms[i];
                    ref VelocityComponent velocity = ref velocityPool.Get(entity);

                    transform.Position += velocity.LinearVelocity * deltaTime;
                }
            }
        }
    }
}
```
