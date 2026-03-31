# Arisen Engine Package Registry

This registry lists all core packages currently located in the `Local/` directory, defining their domains and specific responsibilities within the microkernel architecture.

## 📦 Package Tiers

Packages are categorized into four distinct domains based on their proximity to the Kernel and the User:

1.  **Kernel/Foundation**: Direct extensions of the Arisen Kernel or low-level primitives.
2.  **Domain**: Core engine features (Rendering, ECS, Resources).
3.  **Tooling**: The Editor and visual authoring environments.
4.  **User**: Final application logic and high-level configurations.

---

## 🏗️ Core Package Directory

| Package ID | Domain | Duty |
| :--- | :--- | :--- |
| **`com.arisen.core`** | Kernel | Managed lifecycle, service registry abstractions, and base types. |
| **`com.arisen.core.native`** | Foundation | Monolithic C++ payload (Foundation, HAL, Diagnostics, Shader Compiler). |
| **`com.arisen.dag`** | Foundation | Generic Directed Acyclic Graph (DAG) system for data-driven execution logic. |
| **`com.arisen.taskgraph`** | Foundation | High-performance, multi-threaded internal **Job System** for engine-wide concurrency. |
| **`com.arisen.platform.desktop`** | Foundation | Desktop platform provider (Win32/X11 windowing, OS message loops). |
| **`com.arisen.ecs`** | Domain | Optimized Entity Component System (ECS) using memory-contiguous pools. |
| **`com.arisen.rendering`** | Domain | **RenderGraph Architecture**, RenderPipeline interfaces, and base shading logic. |
| **`com.arisen.resources`** | Domain | Asset Database, resource serialization, and background data discovery. |
| **`com.arisen.rhi.vulkan.native`** | Driver | Native Vulkan implementation of the Rendering Hardware Interface (RHI). |
| **`com.arisen.editor`** | Tooling | The official Avalonia-based visual authoring environment. |
| **`com.arisen.nodecanvas`** | Tooling | Foundation for node-based visual editing and graph manipulation. |
| **`com.arisen.generic-renderpipeline`** | User | High-level, user-configurable RenderPipeline implemented via RenderGraph. |
| **`com.arisen.packagegame`** | User | The main assembly and project root for the active application/game. |

---

## 🔗 Key Relationships

### The Rendering Chain
`com.arisen.rhi.vulkan.native` $\rightarrow$ `com.arisen.rendering` $\rightarrow$ `com.arisen.generic-renderpipeline`
The RHI provides the hardware commands $\rightarrow$ Rendering provides the Graph architecture $\rightarrow$ The Pipeline provides the visual look.

### The Execution Foundation
`com.arisen.dag` $\rightarrow$ `com.arisen.taskgraph`
The DAG provides the logic for resolving node dependencies, while the TaskGraph executes those dependencies across all available CPU cores.

### The Authoring Stack
`com.arisen.nodecanvas` $\rightarrow$ `com.arisen.editor`
NodeCanvas provides the generic graph UI that the Editor uses for material editing, state machines, and render graph visualization.
