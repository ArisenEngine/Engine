# Arisen Engine Development Roadmap

## Vision
Build a high-performance, future-proof engine with a **modern C++ RHI** and a **Data-Oriented C# Core**. The goal is to maximize development efficiency (C#) without sacrificing potential performance (Data-Oriented Design), while eliminating manual binding maintenance.

---

## 📅 Phase 1: RHI Modernization (C++ Foundation)
**Goal**: Address architectural gaps in the RHI to support next-gen rendering features.
*Based on `rhi_review.md` findings.*

1.  **Refactor Pipeline State Objects (PSO)**
    *   **Action**: Remove per-frame `VkPipeline` duplication in `RHIVkGPUPipeline`. Implement a proper PSO cache.
    *   **Benefit**: Faster startup, lower memory usage, smoother streaming.
2.  **Enable GPU-Driven Rendering**
    *   **Action**: Expose `DrawIndexedIndirectCount` and `DrawMeshTasksIndirectCount` in `RHICommandBuffer`.
    *   **Benefit**: Prerequisite for GPU Culling (Nanite-like) and efficient instance rendering.
3.  **Implement Transient Resources**
    *   **Action**: Add specialized allocators for single-frame resources (Transient Buffer/Images) with memory aliasing support.
    *   **Benefit**: Reduced VRAM pressure for complex render graphs.
4.  **Async Compute & Data Transfer**
    *   **Action**:
        *   Instantiate **Transfer Queues** (DMA) in `RHIVkDevice`.
        *   Implement **Queue Ownership Transfer Barriers** to safely move resources between Graphics, Compute, and Transfer queues.
        *   Expose `RHIQueueType` in the public `Submit` API (currently implicit or hardcoded to Graphics in some paths).
    *   **Benefit**: Overlap Geometry/Rasterization with Compute post-processing and Asset streaming.

## 🔗 Phase 2: Binding Automation (Eliminate "NativeEngine")
**Goal**: Stop manual writing of C-exports (`extern "C"`). Automate C++ $\to$ C# bridging.

1.  **Upgrade `BindingGenerator`**
    *   **Action**: Create a new `RHILibrary.cs` configuration in the `BindingGenerator` project.
    *   **Target**: Point it directly at `Arisen/Core/Core.RHI` headers.
    *   **Configuration**:
        *   Map `Arisen::Core::RHI::*` namespace to `ArisenEngine.RHI` (C#).
        *   Handle `Containers::Vector` and `std::shared_ptr` mapping (CppSharp supports this).
3.  **Dynamic Loading Strategy (The "Bootstrap" Pattern)**
    *   **Challenge**: The RHI is loaded dynamically (`RHILoader`), so we can't just `DllImport` static symbols.
    *   **Solution**:
        *   **Bootstrap**: Keep *one* manual export: `CreateRHIInstance(info)`.
        *   **C# Implementation**: Use `NativeLibrary.Load("RHI.Vulkan.dll")` to get the bootstrap function pointer.
        *   **CppSharp Role**: Generate bindings for `Core.RHI` (Interfaces). CppSharp wraps the `IntPtr` returned by the bootstrap function.
        *   **Result**: Function calls hit the C++ vtable directly. No manual `extern "C"` exports needed for member functions.

4.  **Performance Analysis (VTable Overhead)**
    *   **Concern**: Does calling C++ virtual functions from C# add overhead?
    *   **Reality**:
        *   **Current State**: `RHICommandBuffer` methods are *already* virtual in C++. The current C-export adds an *extra* layer: P/Invoke $\to$ `extern "C"` wrapper $\to$ `virtual` call.
        *   **New State**: P/Invoke $\to$ `virtual` call (via VTable offset).
        *   **Conclusion**: The automated approach removes the intermediate C-wrapper function call, potentially **improving** performance. The VTable lookup cost is identical in both scenarios because the RHI design itself is polymorphic.

## 🚀 Phase 3: C# Core & Framework
**Goal**: Establish the "Data-Oriented" entry point and game loop in C#.

1.  **Core Systems (C#)**
    *   **Windowing**: Port Window creation/event loop to C# (calling `Core.window` C++ or OS directly).
    *   **Memory Management**: Implement `Unsafe` struct wrappers for Data-Oriented access.
    *   **ECS Foundation**: (Optional) specific DOTS-like framework or lightweight Struct-of-Arrays implementation.
2.  **Asset Pipeline (C#)**
    *   Use C# for IO and asset processing (Model loading, Texture conversion).
    *   Feed data into RHI Resources via mapped memory.

## 🎨 Phase 4: Render Graph & High-Level Rendering (C#)
**Goal**: Implement the rendering logic where iteration speed matters most.

1.  **Render Graph (C#)**
    *   **Design**: A pure C# class that logically describes passes and dependencies.
    *   **Execution**:
        *   Builds a dependency DAG.
        *   Calculates Barriers automatically.
        *   Allocates Transient Resources (calling Phase 1 RHI features).
        *   Records commands via the automated RHI bindings.
2.  **Scene Renderer**
    *   Implement standard passes: G-Buffer, Lighting, Post-Process using the Graph.

---

## 📝 Success Metrics
*   **Zero Manual bindings**: Adding a function to C++ RHI automatically appears in C# after running the generator.
*   **Performance**: Overhead of C# $\to$ C++ RHI calls is negligible (using unmanaged constraints/inlining).
*   **DX**: "Test" code looks like clean C#, not verbose C-style API calls.
