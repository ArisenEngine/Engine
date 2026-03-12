---
name: create_ecs_component
description: "How to define a new ECS Component for the Arisen Engine."
---

# Defining an ECS Component

In Arisen Engine, components must adhere to strict Data-Oriented Design principles.

## Rules:
1. The component **MUST** be declared as a `public struct`. (Never use generic `T` references or `class`).
2. The component **MUST** implement the marker interface `ArisenEngine.Core.ECS.IComponent`.
3. The component **MUST NOT** contain any properties (`{ get; set; }`), methods, or game logic. Only raw data fields (e.g., `public float Speed;`).
4. The component **MUST NOT** reference managed objects like `string`, `Array`, or class instances.

## Example:

```csharp
using ArisenEngine.Core.ECS;
using System.Numerics;

namespace ArisenEngine.Engine.Components
{
    /// <summary>
    /// Represents the velocity data of an entity. 
    /// Contiguous in memory, fully blittable.
    /// </summary>
    public struct VelocityComponent : IComponent
    {
        public Vector3 LinearVelocity;
        public Vector3 AngularVelocity;
        public float Mass;
    }
}
```
