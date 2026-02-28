# Phase 2: Engine Kernel & Lifecycle

**Objective**: Prove the C# Engine Architecture can orchestrate a full game loop smoothly using a Job-based design.

## 2.1 The Kernel Orchestrator
To move away from monolithic loops, we need a phased Bootloader.

**Implementation Steps:**
1. **Define `EnginePhase` and Subsystems:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Core\Lifecycle\EngineKernel.cs` (or `EngineInstance.cs`).
   - Enums: `PreInit`, `Init`, `PostInit`, `Running`, `Shutdown`.
   - Interface: `IEngineSubsystem` with `Initialize()`, `Shutdown()`, and `int Priority`.
2. **Implement Boot Sequence:**
   - Register subsystems like `RenderPipelineManager`, `InputSystem`, etc.
   - Iterate and initialize them in priority order.

## 2.2 Memory Strategy
High-throughput C# requires eliminating GC.

**Implementation Steps:**
1. **FrameArena:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Core\Memory\FrameArena.cs`.
   - Expose `Alloc<T>(int count)` that writes to a pre-allocated unmanaged buffer. Reset this at the end of every `Running` loop.
2. **NativeArray Wrapper:**
   - Create `NativeArray<T> struct` wrapping unmanaged pointers for sharing data securely with C++ RHI functions.
3. **Refactor C# RHI Command Recording:**
   - Use `FrameArena` for command batches instead of standard C# arrays to prevent Gen0 GC collection.

## 2.3 The "Hello World" Render Loop
Drive raw C++ RHI classes purely via C# execution.

**Implementation Steps:**
1. **Basic C# RenderPipeline:**
   - **Path**: `d:\EngineSource\ArisenEngine\Engine\Arisen\Engine\Rendering\RenderPipeline.cs`.
   - Implement `Render()` function: acquires Swapchain image, begins command recording, binds a basic PSO, calls `Draw`, and submits to the RHI.
2. **Execution Test:**
   - Modify the main C# entry point (`Bootstrap.cs` or `Program.cs`) to loop the render pipeline without crashing.
   - Monitor memory usage to secure zero leaks upon Application Shutdown.
