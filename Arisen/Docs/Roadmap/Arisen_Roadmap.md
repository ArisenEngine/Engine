# Arisen Engine Roadmap

This document serves as the master progression tracker for Arisen. The ultimate goal is to build a high-performance, future-proof engine featuring a modern C++ RHI backend, a data-oriented C# core, an automated binding layer, and a robust Avalonia-based Editor.

*Core Philosophy: "Engine Kernel first, Editor Minimum Viable Ecosystem second, then full data-driven synchronization."*

---

## 📅 Phase 1: Robust Foundation & Capabilities (Completed)
**Goal:** Stabilize the foundation systems and abstract RHI hardware capabilities robustly before building complex logic.

> 👉 **[View Phase 1 Detailed Implementation Plan](file:///d:/EngineSource/ArisenEngine/Engine/Arisen/Docs/Roadmap/Phase1_Foundation.md)**

### 1.1 RHI Capability System
- [x] Define `RHICapabilities` C++ structure.
- [x] Query and populate physical device capabilities during Vulkan initialization (e.g., `maxDescriptorSets`, `timestampComputeAndGraphics`).
- [x] Implement query for `VK_KHR_dynamic_rendering` support.
- [x] Expose capability queries via `AutoBinding` to the C# Engine.

### 1.2 Vulkan Extensions & Graceful Fallbacks
- [x] Refactor `VulkanInit` to accept a settings struct instead of hardcoded strings.
- [x] Conditionally load Validation Layers based on build flags or configuration.
- [x] Extract Device Extensions loading into an extensible configuration list.

### 1.3 Diagnostics & Profiling Validation
- [x] Ensure Tracy profiler context propagates perfectly across C++ Vulkan commands.
- [x] Plumb C# `Profiler.BeginSample()` down to Tracy C++ macros.
- [x] **Milestone Validation**: Run `NativeEngineTest` and confirm 0 Vulkan Validation Errors and a clean Tracy timeline.

##- [x] **Phase 2: Engine Kernel & Lifecycle [DONE]**
    - Orchestration of Phased Boot (Init, Tick, Shutdown).
    - Zero-GC Memory Strategy (`FrameArena`).
    - Basic Render Subsystem & SRP Foundation.

- [x] **Phase 2.5: Modern Package & DLC Architecture**
    - Dynamic Package Discovery & loading (`AssemblyLoadContext`).
    - Manifest-based Metadata (`package.json`).
    - Hot-loading potential & DLC path mapping.
    - Extension Points (Registering Subsystems/Pipelines via Packages).

> 👉 **[View Phase 2 Detailed Implementation Plan](file:///d:/EngineSource/ArisenEngine/Engine/Arisen/Docs/Roadmap/Phase2_Kernel.md)**

### 2.1 The Kernel Orchestrator
- [x] Define `EnginePhase` enum (`PreInit`, `Init`, `PostInit`, `Running`, `Shutdown`).
- [x] Implement `IEngineSubsystem` with initialization priorities.
- [x] Build `EngineKernel` to iterate phases and boot subsystems systematically.

### 2.2 Memory Strategy
- [x] Implement `FrameArena` allocator for per-frame C# allocations (zero GC).
- [x] Implement `NativeArray<T>` wrapper for unmanaged arrays passed to C++.
- [x] Refactor C# RHI Command recording to use `FrameArena`.

### 2.3 The "Hello World" Render Loop
- [x] Setup a basic `RenderPipeline` subsystem in C# that requests RHI buffers and issues draws.
- [x] Remove hardcoded test logic from main; make it data-driven via a temporary scene loader.
- [x] **Milestone Validation**: Execute the Engine exe directly and render a triangle (RHI Triangle Test) without any Vulkan validation errors.

---

## 📅 Phase 3: High-Throughput RHI Evolution (Advanced Graphics)
**Goal:** Break the single GraphicQueue limitation and embrace GPU-driven rendering techniques.

> 👉 **[View Phase 3 Detailed Implementation Plan](file:///d:/EngineSource/ArisenEngine/Engine/Arisen/Docs/Roadmap/Phase3_RHI_Evolution.md)**

### 3.1 Multi-Queue RHI Abstraction
- [ ] Abstract `Submit` to specify Queue Types (`Graphics`, `Compute`, `Transfer`).
- [ ] Acquire hardware Queue Families properly during `RHIDevice` init.
- [ ] Expose Queue-specific Submits to `AutoBinding`.

### 3.2 Advanced Queue Synchronization
- [ ] Implement `RHISemaphore` (Timeline Semaphores preferred if supported).
- [ ] Implement `RHIFence` wrapper for Queue-to-CPU stalls.
- [ ] Bind execution barriers to allow `TransferQueue` texture uploads to synchronize with `GraphicsQueue` rendering.

### 3.3 Next-Gen Deskriptors
- [ ] Investigate and wire up `VK_EXT_descriptor_buffer` feature.
- [ ] Fallback: Implement a descriptor set caching mechanism if the buffer extension is unsupported.

---

## 📅 Phase 4: Editor MVE (Minimum Viable Editor)
**Goal:** Verify the full data loop: **UI -> Serialization -> C# Engine -> C++ RHI**.

> 👉 **[View Phase 4 Detailed Implementation Plan](file:///d:/EngineSource/ArisenEngine/Engine/Arisen/Docs/Roadmap/Phase4_Editor_MVE.md)**

### 4.1 Shell & Viewport Integration
- [ ] Setup `ArisenEditorShell` as the host Avalonia Window.
- [ ] Create an Avalonia custom control (`NativeControlHost`) that receives a Native Window Handle (HWND/X11).
- [ ] Bind the RHI Swapchain to render into the Avalonia HWND seamlessly.

### 4.2 Property Synchronization
- [ ] Create a basic Reflection-based Property Gridd in Avalonia.
- [ ] Connect Avalonia UI actions to Engine configuration properties (e.g., Clear Color changing).
- [ ] **Milestone Validation**: Dragging a color slider in the Editor updates the game viewport in less than 16ms with zero stutter.

### 4.3 Serialization V1
- [ ] Introduce a simple JSON or YAML deserializer (like `System.Text.Json`).
- [ ] Automatically load Engine config and standard assets at boot.

### 4.4 Editor Infrastructure & Graphing
- [ ] Integrate **`ArisenEditorFramework`** to serve as the baseline library for editor structure, sharing foundations with Arisen Studio.
- [ ] Introduce **`ArisenDAG`** and **`ArisenNodeCanvas`** to enable visual node-based editing (e.g., for RenderGraph or Behavior Trees).

---

## 📅 Phase 5: The Data-Driven Synchronicity
**Goal:** Simultaneous and rigorous advancement of Engine logic and Editor tooling.

> 👉 **[View Phase 5 Detailed Implementation Plan](file:///d:/EngineSource/ArisenEngine/Engine/Arisen/Docs/Roadmap/Phase5_Data_Driven.md)**

### 5.1 C# Entity Component System (ECS)
- [ ] Build `Archetype` memory chunk allocator.
- [ ] Implement `IComponentData` strict structs.
- [ ] Refactor game update logic into `SystemBase` classes.

### 5.2 Declarative Render Graph
- [ ] Replace imperative RHI calls with a DAG builder (`RenderGraphBuilder`).
- [ ] Implement logic to automatically inject Memory Barriers based on Pass inputs/outputs.
- [ ] Implement Transient resource aliasing pool.

### 5.3 Streaming Content Pipeline
- [ ] Integrate a runtime `glTF` loader in C# (e.g., `SharpGLTF`).
- [ ] Upload parsed binary mesh data using the new `TransferQueue`.
- [ ] Introduce a hot-reloading asset registry for shaders and scripts.

---

*(This roadmap is a living document and should be updated as milestones are conquered or priorities shift.)*
