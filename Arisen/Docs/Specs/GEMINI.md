# Antigravity (Gemini) AI Assistant Guidelines for Arisen Engine

Welcome to the **Arisen Engine** project context. This document (`GEMINI.md`) defines the default specifications, project background, design principles, and, most importantly, the **permanent and global mandatory rules** that you **MUST** strictly adhere to whenever assisting with this codebase.

---

## 1. Project Background & Architecture

Arisen Engine is a next-generation game engine built on a **Package-Centric Microkernel Architecture** ("Everything is a Package"). This maximizes performance, parallel development, customizable engine compositions, and AI-driven automation.

### Engine Structure
- **`Kernel` (Shell)**: The absolute minimum C# core (`ArisenKernel.dll`). It handles package discovery, manifest resolution, subsystem orchestration, and provides the `ServiceRegistry`. It contains **no domain logic** (no ECS, no RHI).
- **`Packages`**: Everything else is a package (e.g., `com.arisen.rhi.vulkan`, `com.arisen.ecs`, `com.arisen.editor.default`). Packages consist of managed DLLs, native DLLs (in `runtimes/`), and assets.
- **Interfaces & Contracts**: Packages **MUST NOT** directly reference each other's concrete types. They communicate strictly through C# interfaces defined in the Kernel (e.g., `IRHIDevice`, `IEntityManager`), resolved via the `ServiceRegistry`.
- **C# is the Public API**: The internal implementation of a package (C++, Rust, pure C#) is an implementation detail. The package's public surface is strictly its C# entry assembly and the interfaces it implements.
- **`BindingGenerator`**: An internal CLI tool that parses C++ headers tagged with `ARISEN_BIND_PACKAGE` to emit per-package C# PInvoke code. **NEVER hand-edit generated binding files.**

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

### 2.3 Package Boundaries & Service Contracts
- **NEVER** add concrete assembly references between domain packages (e.g., Rendering package referencing Vulkan package).
- Packages **MUST** communicate only through interfaces (`ArisenKernel.Contracts.*`) acquired via the `IServiceRegistry`.
- A package's Native DLLs (C++) **MUST** be placed in `runtimes/{rid}/native/` within the package directory. They will be auto-loaded by `PackageLoadContext`.
- If multiple packages share C++ foundation code, the shared C++ DLLs must be put into a shared provider package (e.g., `com.arisen.core.native`) and declared as a dependency in the `package.json`.

### 2.4 Parallelism and Multithreading
- Engine systems are fully multithreaded (Job System via DAG). You **MUST** assume your code will run on multiple threads concurrently.
- **NEVER** mutate shared state outside of your system's designated components without explicit synchronization (e.g., Atomics, Interlocked).
- Avoid `lock` statements in hot paths.
- **NEVER** access or mutate static global variables from within a Job.
- Use Sync Points (Command Buffers) for structural changes (`CreateEntity`, `DestroyEntity`, `AddComponent`).

### 2.5 C# and Native Interop
- When writing bridging code between C# Engine and C++ packages, structures **MUST** be blittable (`[StructLayout(LayoutKind.Sequential)]`).
- **ALWAYS** use `Span<T>`, `ReadOnlySpan<T>`, or `unsafe` pointers when passing large arrays to C++ to avoid GC pinning costs.
- **Batch Processing**: **NEVER** PInvoke functions per-entity in a loop. Fill a `NativeArray` or `Span<T>` in C# and pass a pointer to C++ to process the chunk at once.
- C++ headers exposing bindings must use the `ARISEN_BIND_PACKAGE`, `ARISEN_BIND_MODULE`, and `ARISEN_BIND_NAMESPACE` macros to map to the correct package output.

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

### Subsystems Lifecycle & Packages
- Every package can provide subsystems. They implement `IEngineSubsystem` (and optionally `ITickableSubsystem`).
- Subsystems are declared in the package's `package.json` (`subsystems` array) for auto-registration by the `PackageSubsystem`. Do not manually register them unless necessary.
- Initialized based on `EnginePhase` (`PreInit`, `Init`, `PostInit`) and `Priority` (ascending).
- Shut down in **reverse** order of initialization.
- Subsystems should register their public Interface implementations into the `IServiceRegistry` during `OnLoad()`.

### Asset Pipeline
- Every source asset has a `.meta` file (JSON/YAML) containing a stable `Guid`.
- **NEVER** reference assets by their string path (`"Assets/Tex.png"`). Systems must reference assets by `Guid`.
- Source assets are not parsed at runtime; they are cooked/imported into optimized binary blocks for Zero-Copy Loading and Memory Mapping.

### RenderGraph
- The RenderGraph operates via declarative Passes in C# (e.g., `renderGraph.AddPass(...)`).
- Passes must be strictly independent. **DO NOT** rely on RHI state leaking from previous passes.
- RenderGraph handles all Memory Barriers, Image Layout Transitions, and Resource Lifetime automatically based on declared dependencies.

### AI-First Architecture & Editor Automation
- **Complete Editor Automation:** Every action in the `Editor` must be executable via a headless Command API or ViewModel method, bypassing the Avalonia UI entirely. If a human can do it via a button click, an AI or automation script must be able to do it via code.
- **Semantic Data Representation:** The Engine must have ways to query a "semantic summary" of the ECS state (e.g., exporting a subset of `ComponentPools` into structured JSON) so that external agents can easily read the world state without parsing binary assets.
- **Headless Execution:** The `Engine` and `Core` simulation must remain strictly decoupled from the RenderGraph and Windowing systems. It must be possible to tick the engine at maximum speed in a background process for training AI agents (Reinforcement Learning).

---

**Final Note to Antigravity (Gemini):**
When generating code, analyzing bugs, or suggesting architectures, you must continuously validate your output against the constraints in this document. Any violation of these zero-overhead, DOD, ECS, Interop, or **Package Interface Strictness** rules is considered a critical failure and must be avoided.
