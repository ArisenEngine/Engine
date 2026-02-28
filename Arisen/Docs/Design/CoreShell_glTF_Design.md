# ArisenEngine: Core-Shell & glTF Design Document

This document outlines the technical vision for ArisenEngine, focusing on the responsibility split between C++ and C# and the strategy for modern asset loading.

## 1. Core-Shell Architecture

ArisenEngine adopts a **Core-Shell** model to achieve the performance of a native engine with the iteration speed of a managed environment.

### 1.1 The C++ Core (The Engine)
The Core is responsible for "The How" — high-performance, low-level operations.
- **RHI (Rendering Hardware Interface)**: Implementation of Vulkan and DX12.
- **AutoBinding Generator**: Uses `CppSharp` to export C++ APIs to C#.
- **Memory & Jobs**: Low-level memory allocation and a high-performance task system.
- **Math Core**: SIMD-optimized math intrinsics.

### 1.2 The C# Shell (The Cockpit)
The Shell is responsible for "The What" — engine logic and orchestration.
- **Pipeline (SRP)**: Scriptable Render Pipeline defining the frame flow.
- **Asset Management**: Parsing complex formats and managing resource lifecycles.
- **Gameplay & Tools**: All high-level features and editor logic.

---

## 2. glTF: The Asset Standard

We have selected **glTF (Graphics Language Transmission Format)** as our primary runtime mesh/scene format.

### 2.1 Why glTF?
- **GPU-Friendly**: Data is structured in "Buffers" and "Accessors" that match RHI buffer/view logic.
- **Extensible**: Supports PBR materials, skeletal animation, and custom metadata.
- **Standardized**: Avoids the "Proprietary Format Hell" of FBX or OBJ.

### 2.2 Implementation Flow (C# Driven)
Instead of hardcoding vertices in C++, the engine will follow this flow:
1. **Load**: C# uses a library (e.g., `SharpGLTF`) to parse the `.gltf` or `.glb` file.
2. **Buffer Creation**: C# calls the `AutoBinding` `RHI_Device_CreateBuffer` for each vertex/index stream.
3. **Upload**: Data is copied into the RHI buffers via `RHI_Buffer_MemoryCopy`.
4. **Mesh Object**: A C# `Mesh` class manages the `RHI_BufferHandle` and ensures correct disposal.

---

## 3. Implementation Checklist

- [ ] **RHI Exports**: Ensure `AutoBinding` exports all necessary buffer/image creation functions.
- [ ] **C# Resource Module**: Create `ArisenEngine.Resources.Mesh` and `ArisenEngine.Resources.Material`.
- [ ] **glTF Loader**: Integrate a C# glTF parser into the `Engine` project.
- [ ] **Test Case**: Create a C# test case that loads a complex glTF model and renders it.

---

## 4. Vision for the Future

By placing the **Renderer Pipeline** and **Asset Loader** in C#, the engine remains incredibly flexible. Developers can rewrite a rendering pass or add a new asset parser without recompiling the entire C++ Core, while still benefiting from the raw power of the RHI layer below.
