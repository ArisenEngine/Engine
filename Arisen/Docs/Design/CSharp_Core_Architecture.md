# C# Core Architecture

ArisenEngine places all primary logic processing into `.NET`/C#. This dramatically improves syntax legibility, decreases compilation times, and leverages the powerful .NET package ecosystem. However, doing so requires strict architectural discipline to prevent performance losses due to Garbage Collection (GC) pressure and poor CPU branching.

## 1. Engine LifeCycle & Kernel
The entire engine lifespan is explicitly managed by the `EngineKernel`. This replaces traditional monolithic `while(true)` loops with rigid state phases. 
- **Initialization Priorities**: Components like `AssetPipeline`, `ScriptingHost`, and `RenderPipeline` exist as `IEngineSubsystem` instances. The Kernel boots them strictly in order.
- **Phased Scheduling**:
  1. `PreInit`: Resolving DLLs and configuring global loggers (`Serilog`/Native PInvoke maps).
  2. `Init`: Bringing the Hardware and memory up.
  3. `PostInit`: Loading scenes, UI, and external script domains.
  4. `Running`: The Main Job Loop (Update, Culling, Rendering).
  5. `Shutdown`: Graceful closure to prevent memory leaks and dangling handlers.

## 2. Memory Conventions
To solve `.NET` GC pause bottlenecks, Arisen aggressively moves transient resources out of managed `class` heaps.

- **FrameArena (`ArisenEngine.Core.Memory`)**: A linear memory buffer that blindly allocates contiguous `byte` slabs during a frame, immediately resetting at the Frame's end without notifying the GC.
- **NativeArray (`Span<T>`)**: Unmanaged memory abstractions allowing memory segments to seamlessly bounce between C# pointers and the native C++ Backend.
- **No Class Instances Every Frame**: Systems and Components must be `structs` (`unmanaged` logic).

## 3. Entity Component System (ECS)
The core simulation pipeline is transitioning to Data-Oriented Design to guarantee cache hits across memory.
- **Components are Simple Types**: `IComponentData` structs (`Position`, `Velocity`, `Renderable`). They do not contain functions.
- **Archetype Memory Chunks**: Components are packed sequentially based on their `Archetype`. If 600 entities share identical traits, they are jammed into identical `Chunk` sizes.
- **Systems are Job-Driven**: `SystemBase` instances query entire Chunks at once and distribute the mathematics broadly across multi-threaded workers (JobSystem).

## 4. The Render Graph
We have replaced immediate command pipeline `Submit()` calls with a declarative **RenderGraph**.

Instead of manually trying to parse the dependencies of the depth buffers against future post-processing stages, systems instead declare:
```csharp
graph.AddRenderPass("LightingPass", ctx => {
    ctx.ReadTexture(depthBuffer);
    ctx.SetRenderTarget(colorTarget);
    // Submit objects...
});
```
The Engine parses the entire graph, identifies the topology via a Directed Acyclic Graph (DAG), and:
1. Instantiates transient resources (Aliasing memory safely).
2. Computes exactly when to trigger specific Vulkan Image Memory Barriers (e.g. `UNDEFINED` -> `COLOR_ATTACHMENT_OPTIMAL`).
3. Dispatches dependencies across `Transfer/Compute/Graphics` Queues transparently.

## 5. JobSystem Integration
The C# Core will bypass typical `.NET` thread pools by abstracting explicit workers customized for Engine loads. Render Commands and logic calculations implement the `IJobParallelFor` to split indices directly into thread contexts. 

*No system in Arisen runs arbitrarily. Everything is orchestrated to be measurable, cache-friendly, and parallelizable.*
