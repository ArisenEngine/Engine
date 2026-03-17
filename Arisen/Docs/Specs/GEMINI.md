# Antigravity (Gemini) AI Assistant Guidelines for Arisen Engine

Welcome to the **Arisen Engine** project context. This document (`GEMINI.md`) defines the default specifications, project background, design principles, and, most importantly, the **permanent and global mandatory rules** that you **MUST** strictly adhere to whenever assisting with this codebase.

---

## 1. Project Background & Architecture

Arisen Engine is a next-generation game engine separated into distinct layers to maximize performance, customize-ability, and safety.

### Layer Separation
- **`Core` (C++)**: Built for absolute maximum performance, hardware interfacing (Vulkan/DirectX), low-level memory handling, and the Rendering Hardware Interface (RHI). It provides interfaces and fundamental abstractions but leaves high-level logic to C#.
- **`Engine` (C#)**: Contains the primary Entity Component System (ECS), game simulation logic, mathematics (using `System.Numerics`), and high-level scene management (like the RenderGraph). **C# drives the logic; C++ executes the heavy lifting.**
- **`Editor` (C#)**: The Avalonia-based C# Editor. It references the `Engine` project and provides the user interface for scene authoring using the strict MVVM pattern.
- **`BindingGenerator` & `AutoBinding`**: A CLI tool that parses C++ headers to emit C# PInvoke code into the `AutoBinding` directory. **NEVER hand-edit files in `AutoBinding`.**
- **Other Modules**: `Assets` (asset pipeline), `Projects`, `Scripts` (Build scripts), `Test`.

---

## 2. Global Mandatory Rules & Taboos (From `Rules.md`)

These rules are **STRICT** and **NON-NEGOTIABLE**.

### 2.1 Zero-Overhead and Data-Oriented Design (DOD)
- **NEVER** allocate managed classes (`new MyClass()`) in any per-frame hot path (like `Update`, `Tick`, or Render loops).
- **ALWAYS** prefer `struct` (value types) over `class` (reference types) for data containers.
- **NEVER** use `virtual` methods or object-oriented inheritance for hot-path game logic. Use composition and ECS instead.

### 2.2 Entity Component System (ECS) Restrictions
- All components **MUST** implement the `IComponent` interface and **MUST** be defined as `struct`.
- Components **MUST NOT** contain any logic or methods, only raw data (fields).
- Systems **MUST** process components in bulk by iterating over `ComponentPool<T>.GetRawComponentArray()` or using Jobs. **NEVER** iterate entities one by one calling `pool.Get(entity)`.
- **NEVER** store reference types (`class`, `string`, `object`) inside an `IComponent`. Use indices, IDs, or Native strings.

### 2.3 Parallelism and Multithreading
- Engine systems are fully multithreaded (Job System via DAG). You **MUST** assume your code will run on multiple threads concurrently.
- **NEVER** mutate shared state outside of your system's designated components without explicit synchronization (e.g., Atomics, Interlocked).
- Avoid `lock` statements in hot paths.
- **NEVER** access or mutate static global variables from within a Job.
- Use Sync Points (Command Buffers) for structural changes (`CreateEntity`, `DestroyEntity`, `AddComponent`).

### 2.4 C# and Native Interop
- When writing bridging code between C# Engine and C++ Core, structures **MUST** be blittable (`[StructLayout(LayoutKind.Sequential)]`).
- **ALWAYS** use `Span<T>`, `ReadOnlySpan<T>`, or `unsafe` pointers when passing large arrays to C++ to avoid GC pinning costs.
- **Batch Processing**: **NEVER** PInvoke functions per-entity in a loop. Fill a `NativeArray` or `Span<T>` in C# and pass a pointer to C++ to process the chunk at once.

### 2.5 Editor and UI
- Stick strictly to the **MVVM** pattern in Avalonia. Views (`.axaml`) do not have business logic in code-behind.
- ViewModels should notify property changes (`INotifyPropertyChanged` via `ReactiveObject` or `EditorPanelBase`).
- Editor code **MUST NOT** run C++ engine logic directly on the UI thread if it blocks for more than 16ms.

### 2.6 Logging
- When developing or fixing bugs, **ALWAYS** separate Editor logs (`ArisenEditor.Core.Services.EditorLog`) from Player logs (`ArisenEngine.Core.Diagnostics.Logger`). **Do not mix them up.**

---

## 3. Module Design Principles & References

### Memory Management
- **`FrameArena`**: For 1-frame transient data, use `FrameArena.Instance.Alloc<T>(count)`. **NEVER** store references to `FrameArena` memory across frame boundaries.
- **`NativeArray<T>`**: For long-lived unmanaged data. **MUST** be disposed manually or via `using` blocks to prevent memory leaks.

### Performance Profiling
- All performance-sensitive regions **MUST** be instrumented.
- **C++**: Use `ARISEN_PROFILE_ZONE("ZoneName")` from `Profiler/Profiler.h`.
- **C#**: Use `using (Profiler.Zone("ZoneName")) { ... }` from `ArisenEngine.Core.Diagnostics`. This returns a readonly struct and avoids GC allocation.

### Subsystems Lifecycle
- Subsystems implement `IEngineSubsystem` (and optionally `ITickableSubsystem`).
- Initialized based on `EnginePhase` (`PreInit`, `Init`, `PostInit`) and `Priority` (ascending).
- Shut down in **reverse** order of initialization.
- Access via `EngineKernel.Instance.GetSubsystem<T>()`.

### Asset Pipeline
- Every source asset has a `.meta` file (JSON/YAML) containing a stable `Guid`.
- **NEVER** reference assets by their string path (`"Assets/Tex.png"`). Systems must reference assets by `Guid`.
- Source assets are not parsed at runtime; they are cooked/imported into optimized binary blocks for Zero-Copy Loading and Memory Mapping.

### RenderGraph
- The RenderGraph operates via declarative Passes in C# (e.g., `renderGraph.AddPass(...)`).
- Passes must be strictly independent. **DO NOT** rely on RHI state leaking from previous passes.
- RenderGraph handles all Memory Barriers, Image Layout Transitions, and Resource Lifetime automatically based on declared dependencies.

---

**Final Note to Antigravity (Gemini):**
When generating code, analyzing bugs, or suggesting architectures, you must continuously validate your output against the constraints in this document. Any violation of these zero-overhead, DOD, ECS, or Interop rules is considered a critical failure and must be avoided.
