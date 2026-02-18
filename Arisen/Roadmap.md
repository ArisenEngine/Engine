# Arisen Engine Development Roadmap

## Vision
Build a high-performance, future-proof engine with a **modern C++ RHI**, a **Data-Oriented C# Core**, and a seamless **Automated Binding** layer. The core principle is "Low-level Performance, High-level Productivity".

---

## 📅 Phase 1: RHI Stabilization & Core Infrastructure (P0 - Immediate)
**Goal**: Finalize Vulkan RHI fundamentals and establish profiling based on Tier 1 Fixes.

1.  **RHI Interface Refinement**
    *   **Action**: Segregate `RHIDevice` into `RHIFactory`, `RHISyncPrimitive`, and `RayTracingExtension`.
    *   **Action**: Move `RHICommandBuffer::Replay` logic to `.cpp` to optimize build times.
2.  **PSO Caching & Stability**
    *   **Action**: Implement a **Global PSO Cache**. Decouple `VkPipeline` from `frameIndex`.
    *   **Action**: Solve remaining Vulkan validation errors in the `RayTracing` and `NativeEngine` tests.
3.  **Tracy Profiler Integration**
    *   **Action**: Establish `Core.Diagnostic` for instrumentation wrappers.
    *   **Action**: Instrument RHI CommandBuffer recording and execution.

## 📅 Phase 2: Binding Automation & C# Bootstrap (P1 - Strategic)
**Goal**: Complete the CppSharp generator and ensure thread-safe command recording.

1.  **CppSharp Generator Pipeline**
    *   **Action**: Configure `BindingGenerator` to automate handle exports.
    *   **Action**: Implement the C# "Bootstrap" loader.
2.  **High-Performance Recording**
    *   **Action**: Implement **Lock-free Handle Registry** to remove mutex contention during recording.
    *   **Action**: Transition `RHICommandStream` to a linear **Memory Arena** for zero-allocation recording.
3.  **Verification**: Port `NativeEngineTest` from C++ to C#.

## 📅 Phase 3: Modern Rendering Core (P2 - Performance)
**Goal**: Implement industry-standard high-throughput features.

1.  **Next-Gen Resource Binding**
    *   **Action**: Implement **Descriptor Buffers (`VK_EXT_descriptor_buffer`)** to bypass legacy sets.
    *   **Action**: Full Bindless Table integration.
2.  **C# Render Graph System**
    *   **Action**: DAG-based graph for automatic resource lifetime and barrier management.
    *   **Action**: Implement Transient resource allocation (aliasing).
3.  **GPU-Driven & Multi-Queue**
    *   **Action**: Implement **Indirect Draw** and **Mesh Shaders**.
    *   **Action**: Expose dedicated **Async Compute** and **Transfer** queues.

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

