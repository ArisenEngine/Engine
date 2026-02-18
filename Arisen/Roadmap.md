# Arisen Engine Development Roadmap

## Vision
Build a high-performance, future-proof engine with a **modern C++ RHI**, a **Data-Oriented C# Core**, and a seamless **Automated Binding** layer. The core principle is "Low-level Performance, High-level Productivity".

---

## 📅 Phase 1: RHI Stabilization & core Infrastructure
**Goal**: Finalize Vulkan RHI fundamentals and establish profiling.

1.  **RHI Interface Refinement**
    *   **Action**: Segregate `RHIDevice` into logical components (`RHIFactory`, `RHISyncPrimitive`, `RayTracingExtension`).
    *   **Action**: Move `RHICommandBuffer::Replay` logic to `.cpp` to optimize build times.
2.  **PSO Caching & Stability**
    *   **Action**: Implement a global PSO Cache. Decouple `VkPipeline` from `frameIndex`.
    *   **Action**: Solve remaining Vulkan validation errors in the `RayTracing` and `NativeEngine` tests.
3.  **Tracy Profiler Integration**
    *   **Action**: Move instrumentation wrappers to `Core.Diagnostic`.
    *   **Action**: Instrument RHI CommandBuffer recording and execution for deep performance visibility.

## � Phase 2: Binding Automation & C# Bootstrap
**Goal**: Complete the CppSharp generator and eliminate manual `extern "C"` boilerplate.

1.  **CppSharp Generator Pipeline**
    *   **Action**: Configure `BindingGenerator` to parse `Core.RHI` headers.
    *   **Action**: Automate the export of RHI handles and interfaces.
2.  **C# RHI Wrapper**
    *   **Action**: Generate the `ArisenEngine.RHI` assembly.
    *   **Action**: Implement the C# "Bootstrap" loader to bind the RHI DLL at runtime.
3.  **Verification**: Port `NativeEngineTest` from C++ to C#.

## 📅 Phase 3: Modern Rendering Core (The "Powerhouse")
**Goal**: Implement GPU-driven features and the C# Render Graph.

1.  **Full Bindless Support**
    *   **Action**: Standardize resource access on global heaps.
    *   **Action**: Move indices management to `RHIBindlessDescriptorTable`.
2.  **C# Render Graph System**
    *   **Action**: Design the DAG-based graph for automatic resource lifetime and barrier management.
    *   **Action**: Implement Transient resource allocation (aliasing).
3.  **GPU-Driven Foundations**
    *   **Action**: Implement Indirect Draw and Mesh Shader paths in the Vulkan backend.

## � Phase 4: High-Level Engine Framework (C#)
**Goal**: Build the user-facing engine systems.

1.  **Asset Pipeline (C#)**
    *   Multi-threaded asset loading (glTF, Textures).
    *   Integration with persistent resource cache.
2.  **Scene & ECS**
    *   Implementation of the scene hierarchy and renderer in C#.
    *   Data-Oriented component system for high-performance iteration.

## 📅 Phase 5: Editor & Tooling (AvaloniaUI)
**Goal**: Iterate on the Editor and project management tools.

1.  **Editor Shell**
    *   Modernize the AvaloniaUI shell.
    *   Vulkan viewport integration into Avalonia.
2.  **Project System**
    *   Project creation, resource management, and C# script hot-reloading.

---

## 📝 Success Metrics
*   **Compile once, run anywhere**: Adding a C++ RHI method is immediately available in C#.
*   **Zero Validation Errors**: Clean Vulkan runs on all test cases.
*   **Performance Trace**: Every RHI command is traceable in Tracy from recording to GPU execution.

