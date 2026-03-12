---
name: add_profiler_marker
description: "How to add performance profiling markers in C++ and C#."
---

# Adding Profiler Markers

The Arisen Engine is heavily metric-driven. When you create new performance-sensitive systems, you must wrap them in Profiler Zones.

## C++ Implementation
Include `#include "Core.Diagnostic/Profiler/Profiler.h"` and use the macros. They evaluate to nothing in non-profiled builds, so do not hesitate to use them.

```cpp
void PhysicsSolver::StepSimulation(float deltaTime)
{
    ARISEN_PROFILE_ZONE("PhysicsSolver::StepSimulation");
    
    // ... logic ...
}
```

## C# Implementation
Use the `IDisposable` struct `ProfilerZone` via a `using` statement. Do **NOT** try to manually start/stop the profiler unless strictly necessary.

```csharp
using ArisenEngine.Core.Diagnostics;

public class PhysicsSystem
{
    public void Update()
    {
        using (Profiler.Zone("PhysicsSystem.Update"))
        {
            // ... loops and logic ...
        }
    }
}
```
