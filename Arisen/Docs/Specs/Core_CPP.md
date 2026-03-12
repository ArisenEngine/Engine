# C++ Core Spec

The `Core` project (e.g., `Core.RHI`, `Core.Foundation`) is the bedrock of Arisen Engine. It is written in C++ and is responsible for low-level memory handling, hardware interfacing (Vulkan/DirectX), and extreme performance operations.

## 1. Memory Management & Object Lifecycles
- **No `std::shared_ptr` for Resources**: The engine uses a strict **Handle-based Architecture** for resources (e.g., `RHIResourceHandle`, `RHICommandBuffer`). 
- **Ownership**: Subsystems uniquely own their data. When passing references to the C# layer, pass opaque handles or raw pointers (`void*`), never smart pointers.
- **Allocators**: Prefer custom linear/arena allocators for frame-temporary data over `new`/`malloc`.

## 2. Rendering Hardware Interface (RHI)
- The C++ layer **only provides the interface** and fundamental hardware abstractions (e.g., `RHIDevice`, `RHICommandBuffer`, `RHIPipelineState`).
- **No High-Level Logic**: The C++ RHI layer does **not** implement RenderGraphs, Scene Culling, or complex material sorting. All high-level pipeline logic, including the RenderGraph, is constructed and executed from the **C# Engine layer**.
- **Stateless Commands**: RHI Command Buffers should be recorded as statelessly as possible. The C# layer will submit batched commands to C++.

## 3. Standard Library (STL) Restrictions
- While STL (e.g., `std::vector`, `std::string`) is permissible for initialization or tool-side operations, it **MUST NOT** be used in the hot-path (per-frame loops).
- Any dynamic allocation during the frame is a strict violation. Pre-allocate capacity during the `Init` phase.

## 4. Mathematics and SIMD
- Do **not** build custom complex Math libraries in C++ unless absolutely necessary for an isolated low-level algorithm.
- Arisen Engine relies heavily on C# `System.Numerics` (which is already hardware-accelerated/SIMD-backed by the .NET JIT/AOT compiler) for the majority of game math.
- Pass plain structs (e.g., `Vector3`, `Matrix4x4`) consisting of contiguous floats across the C++/C# boundary.
