---
name: create_engine_subsystem
description: "Template for a new engine-level subsystem with priority and phase registration."
---

# Creating an Engine Subsystem

Engine subsystems are modular plugins that the `EngineKernel` manages.

## Steps
1. Create a class implementing `IEngineSubsystem` (or `ITickableSubsystem` if it needs per-frame updates).
2. Define its `Priority` and `InitPhase`.
3. Register it with `EngineKernel.Instance` before the engine boots (typically in `ArisenApplication.InitializeEngine`).

## Example

```csharp
using ArisenEngine.Core.Lifecycle;

namespace MyModule
{
    public class MyPhysicsSubsystem : ITickableSubsystem
    {
        public int Priority => 100; // Lower numbers boot first
        public EnginePhase InitPhase => EnginePhase.Init;

        public void Initialize()
        {
            // Set up physics world
        }

        public void Tick(float deltaTime)
        {
            // Step simulation
        }

        public void Shutdown()
        {
            // Cleanup
        }

        public void Dispose() { Shutdown(); }
    }
}
```
