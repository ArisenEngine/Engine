# Profiler Spec

Performance is a first-class citizen in Arisen Engine. All performance-sensitive regions of code, both in C++ and C#, **MUST** be instrumented with the Engine's Profiler. 

The Profiler is designed to have **zero overhead** when `ARISEN_PROFILER_ENABLED` is `0`, meaning you should be aggressive about adding markers.

## 1. C++ Profiling (`Core.Diagnostic`)

In the native C++ layer (`engine/core`), we use Tracy macros defined in `Profiler.h`.

- **Zone Scoping**: Use `ARISEN_PROFILE_ZONE("ZoneName")` at the beginning of any heavy function, loop, or significant block. This automatically scopes the duration to the block's lifetime.
- **Frame Marking**: Use `ARISEN_PROFILE_FRAME("FrameName")` to mark the boundary of a repeating process (typically handled centrally by the kernel).
- **Values**: Use `ARISEN_PROFILE_VALUE("MetricName", value)` to plot numerical data over time (e.g., active memory allocations, entities processed).

**Example:**
```cpp
#include "Profiler/Profiler.h"

void MyHeavyFunction()
{
    ARISEN_PROFILE_ZONE("MyHeavyFunction_Execution");
    // ... expensive work ...
}
```

## 2. C# Profiling (`ArisenEngine.Core.Diagnostics`)

In the managed C# layer, we wrap the native API calls using `IDisposable` structs to ensure `using` statements correctly push and pop zones without allocating on the managed heap.

- **Zone Scoping**: Use `using (Profiler.Zone("ZoneName")) { ... }` around blocks. Because it returns a readonly struct, it does not trigger the Garbage Collector.
- **Plotting**: Use `Profiler.PlotValue("MetricName", value)`.

**Example:**
```csharp
using ArisenEngine.Core.Diagnostics;

public void UpdateEntities()
{
    using (Profiler.Zone("UpdateEntities_System"))
    {
        // ... game logic ...
        Profiler.PlotValue("ActiveEntities", _pool.Count);
    }
}
```

## When to Profile?
1. Any ECS `System` update loop.
2. Any Asset Pipeline loading/baking method.
3. RHI Command generation and submission.
4. Heavy editor-side UI computations (Search/Filtering).
