# ArisenEngine

ArisenEngine is an open-source, self-developed rendering engine designed for learning and research. It adopts a modular architecture and supports multi-platform builds and modern graphics APIs (specifically Vulkan). The goal is to create a modern, high-performance graphics engine that allows developers to explore cutting-edge graphics programming and engine architecture.

---

![License](https://img.shields.io/github/license/ArisenEngine/Engine)
![Contributors](https://img.shields.io/github/contributors/ArisenEngine/Engine)
[![Contributors](https://contrib.rocks/image?repo=ArisenEngine/Engine)](https://github.com/ArisenEngine/Engine/graphs/contributors)

---

## 🧰 Requirements

| Component     | Version Requirement      |
| ------------- | ------------------------ |
| CMake         | ≥ 3.29                   |
| Vulkan SDK    | Installed (Latest recommended) |
| Visual Studio | 2022 (C++23 support)     |
| Windows SDK   | 10+                      |
| .NET SDK      | 9.0+                     |
| Python        | 3.0+                     |

---

## 🏗️ Architecture Design

### Core Architecture Overview

ArisenEngine employs a layered modular architecture, primarily divided into the following core levels:

```
┌─────────────────────────────────────────────────────────────┐
│                    Editor Layer (C#)                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   ArisenEditor  │  │   Project Mgmt  │  │   UI/UX      │ │
│  │   (Avalonia)    │  │   & Asset Tools │  │   Framework  │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                 Engine Layer (C#)                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   ShaderLab     │  │   Serialization │  │   Models     │ │
│  │   Parser        │  │   System        │  │   & ECS      │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                Platform Layer (C++)                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Shader        │  │   Window        │  │   File I/O   │ │
│  │   Compiler      │  │   Management    │  │   & Utils    │ │
│  │   (DXC)         │  │   (Win32)       │  │              │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                   RHI Layer (C++)                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Vulkan        │  │   DirectX 12    │  │   Common     │ │
│  │   Backend       │  │   (Planned)     │  │   RHI        │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│                 Infrastructure (C++)                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │   Logging       │  │   Memory        │  │   Profiling  │ │
│  │   (spdlog)      │  │   Management    │  │   (Tracy)    │ │
│  └─────────────────┘  └─────────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Core Components Details

#### 1. ShaderLab System 🎨
- **Function**: Parses ShaderLab format shader files, extracting metadata and HLSL code.
- **Features**:
  - Complete Pass parsing (Name, Tags, Render States).
  - HLSL code extraction and preprocessor directive handling.
  - Render State parsing (Blend, ZWrite, ZTest, Cull, Stencil, etc.).
  - Pragma directive parsing (Entry points, Target models, Multi-compile variants).
  - Code fidelity checking based on source text slicing.
- **Output**: Shader metadata in YAML format, pure HLSL code for DXC compilation.

#### 2. RHI (Render Hardware Interface) 🖥️
- **Design Philosophy**: High-performance thin abstraction over modern graphics APIs.
- **Supported Backends**:
  - **Vulkan**: Primary backend. Deeply optimized for low overhead, explicit synchronization, and bindless resources.
  - **DirectX 12**: Planned for future expansion.
- **Core Functions**:
  - Handle-based resource management (Bindless ready).
  - Explicit synchronization (Barriers, Semaphores, Fences).
  - Multi-threaded command recording.
  - Dynamic resource binding via Descriptor Indexing.

#### 3. Shader Compilation System ⚙️
- **Compiler**: DirectX Shader Compiler (DXC).
- **Target Formats**: SPIR-V (Vulkan).
- **Features**:
  - Cross-platform compilation from HLSL to SPIR-V.
  - Shader variant management (multi_compile, shader_feature).
  - Include file dependency resolution.
  - Optimization level control.

#### 4. Editor System 🛠️
- **UI Framework**: Avalonia (Cross-platform .NET UI).
- **Core Functions**:
  - Project management and asset browser.
  - Shader editor and preview.
  - Scene editor and entity management.
  - Real-time rendering preview window.
- **Extensibility**: Modular design based on plugins.

#### 5. Binding Generation System 🔗
- **Tech Stack**: CppSharp (C++ to C# binding generation).
- **Functions**:
  - Automatic generation of C# bindings for C++ native code.
  - Type-safe memory management.
  - Cross-language call optimization (P/Invoke).

### Data Flow Architecture

```
ShaderLab File → ShaderLab Parser → YAML Metadata
                                    ↓
Pure HLSL Code → DXC Compiler → SPIR-V Bytecode
                                    ↓
SPIR-V Reflection → Descriptor Info → Vulkan Pipeline Build
                                    ↓
Render Pipeline → GPU Execution → Framebuffer Output
```

## 🗺️ High-Performance Roadmap

ArisenEngine aims to rival modern commercial engines in rendering architecture, focusing on "GPU-Driven," "Bindless," and "Parallelism."

### Phase 1: The Modern Foundation (Current Focus) 🏗️
Establishing a rock-solid, zero-overhead foundation for modern rendering.
- [x] **High-Performance RHI (Vulkan)**
    - [x] Handle-based resource access (preparing for Bindless).
    - [x] Explicit memory management (VMA integration).
    - [x] Synchronization primitive abstraction.
- [ ] **Data-Oriented Core**
    - [ ] Job System / Task Graph (Fiber-based multitasking).
    - [ ] SIMD-optimized math library.
    - [ ] Low-fragmentation memory allocators (Frame/Stack allocators).
- [ ] **Bindless Architecture**
    - [ ] Global Descriptor Heap design.
    - [ ] "Bindless" texture and buffer access in shaders.
    - [ ] Removal of legacy descriptor set binding management.

### Phase 2: GPU-Driven Rendering Pipeline 🚀
Moving the heavy lifting from CPU to GPU.
- [ ] **GPU-Driven Geometry**
    - [ ] GPU Culling (Frustum, Occlusion) via Compute Shaders.
    - [ ] Mesh Shaders pipeline support.
    - [ ] Indirect Drawing (Multi-Draw Indirect).
- [ ] **Modern Render Graph**
    - [ ] Automatic resource barrier placement.
    - [ ] Transient resource management (memory aliasing).
    - [ ] Async Compute scheduling.

### Phase 3: Advanced Graphics Features 🌟
Implementing state-of-the-art rendering techniques.
- [ ] **Global Illumination**
    - [ ] Hybrid Ray Tracing pipeline.
    - [ ] Real-time GI (DDGI or Voxel-based).
    - [ ] Screen Space Glocal Illumination (SSGI).
- [ ] **Virtual Geometry**
    - [ ] Cluster-based rendering (similar to Nanite).
    - [ ] Continuous Level of Detail (LOD).
- [ ] **Volumetric Effects**
    - [ ] Volumetric Fog & Lighting.
    - [ ] Volumetric Clouds.

### Phase 4: Production Readiness & Tooling 🛠️
Making the engine usable for actual game production.
- [ ] **Asset Pipeline**
    - [ ] Fast texture compression/transcoding (KTX2/DDS).
    - [ ] Model optimization (MeshOptimizer).
    - [ ] Asynchronous Asset Streaming.
- [ ] **Editor & Debugging**
    - [ ] GPU Frame Profiler integration.
    - [ ] Visual Shader Graph.
    - [ ] Scene serialization & Prefab system.

## ⚙️ Build Instructions (Windows)

### ✅ Quick Build Editor (Recommended)

Run the following script to generate the Editor solution:

```bash
Scripts/Windows/generate_editor_all.bat
```
