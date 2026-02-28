# Arisen Engine Roadmap

This document outlines the strategic phases for developing Arisen. The ultimate goal is to build a high-performance, future-proof engine featuring a modern C++ RHI backend, a data-oriented C# core, an automated binding layer, and a robust Avalonia-based Editor.

*Our core philosophy for development: "Engine Kernel first, Editor Minimum Viable Ecosystem second, then full data-driven synchronization."*

---

## 📅 Phase 1: Robust Foundation & Capabilities (Immediate Objective)
**Goal:** Stabilize the foundation systems and abstract RHI hardware capabilities robustly before building complex logic.

- **RHI Capability System:** Implement a feature querying structure (`RHICapabilities`) to dynamically detect device support (e.g., Dynamic Rendering, Descriptor Indexing, max attachments).
- **Vulkan Extensions & Fallbacks:** Move away from hardcoded initialization. Integrate feature toggles (via configurations) for Validation Layers and explicit Device Extensions parsing.
- **Diagnostics Validation:** Ensure Tracy profiler integration spans fully across native commands and C# lifecycle markers. Achieve zero Vulkan Validation Errors in tests.

## 📅 Phase 2: Engine Kernel & Lifecycle (Mid-Term Run)
**Goal:** Prove the C# Engine Architecture can orchestrate a full game loop smoothly using a Job-based design.

- **Lifecycle Orchestration (`EngineKernel`):** Complete the implementation of the phased startup/shutdown subsystem. Integrate Subsystem initialization priorities.
- **Memory & Allocation Strategy:** Implement zero-allocation C# recording for frames using custom memory variants (e.g., `FrameArena`, `NativePool`, `NativeArray`).
- **Basic Render Pipeline:** Achieve a successful Triangle/Model render driven exclusively by C# orchestrating the C++ RHI bindings without Editor interference.

## 📅 Phase 3: High-Throughput RHI Evolution (Advanced Graphics)
**Goal:** Break the single GraphicQueue limitation and embrace GPU-driven rendering techniques.

- **Multi-Queue RHI Abstraction:** Introduce dedicated `TransferQueue` (for background asynchronous asset uploading) and `ComputeQueue` (for compute shader culling and simulation).
- **Queue Synchronization:** Develop comprehensive `RHISemaphore` and `RHIFence` abstractions capable of scaling with asynchronous queues.
- **Next-Gen Deskriptors:** Migrate to Descriptor Buffers (`VK_EXT_descriptor_buffer`) and a Bindless pipeline.

## 📅 Phase 4: Editor MVE (Minimum Viable Editor)
**Goal:** Verify the full data loop: **UI -> Serialization -> C# Engine -> C++ RHI**.

- **Viewport Integration:** Embed the C++ RHI rendering output directly into an Avalonia window control inside `ArisenEditor.Desktop`.
- **Property Shell:** Build a simple property explorer plugin inside `ArisenEditorShell`. Verify that changing a C# configuration value in Avalonia updates the engine render immediately.
- **Serialization V1:** Move past the "temporary" serialization system to a stable configuration format (e.g., JSON or robust internal format) to load basic entities.

## 📅 Phase 5: The Data-Driven Synchronicity
**Goal:** Simultaneous and rigorous advancement of Engine logic and Editor tooling.

- **Entity Component System (ECS):** Replace monolithic logic with a chunked memory Archetype system for performance and multi-threaded scaling.
- **Render Graph Integration:** Instead of manual barriers and transient memory bindings, implement a DAG Render Graph system that resolves hardware dependencies automatically based on Pass layout declarations.
- **Resource Pipeline:** Multi-threaded asset importing (e.g., parsing glTF files dynamically from the Editor and streaming them via the Transfer Queue).

*(This roadmap is a living document and will evolve as requirements pivot.)*
