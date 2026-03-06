# Arisen Engine Overview

Welcome to the Arisen Engine documentation. Arisen is designed as a **next-generation, high-performance, and data-driven game engine**. It marries the raw performance and low-level control of modern C++ with the rapid iteration and high-level productivity of modern C# (.NET).

## Core Philosophy

1. **Performance First (C++ Core)**: The heaviest lifting—such as interfacing with the OS, memory tracking, multi-threading platforms, and chatting with the GPU (Vulkan/DX12)—is exclusively handled by the C++ backend.
2. **Productivity and Iteration (C# Engine)**: The entire rendering pipeline, scene management, asset loading, and gameplay logic are authored in C#. This prevents recompilation bottlenecks and leverages the massive ecosystem of .NET.
3. **Seamless Interop (AutoBinding)**: Developers should never write manual `P/Invoke` wrappers. A centralized `BindingGenerator` automatically parses C++ headers and exports ultra-fast, zero-allocation C# handles.
4. **Data-Oriented Design (ECS)**: The C# game logic and engine subsystems are transitioning towards an Entity-Component-System (ECS) architecture to ensure contiguous memory layouts and painless multi-threading.

## Directory Structure Overview

The project is organized into distinct responsibilities:

### 1. The Native Core (`Core` / `3rdparty`)
Written in C++. This is the foundational layer.
- **`Core.RHI`**: The Render Hardware Interface abstracting modern APIs.
- **`RHI.Vulkan` & `RHI.DX12`**: Implementations of the RHI.
- **`Core.HAL`**: Hardware Abstraction Layer for OS-level threading, windowing, and file IO.
- **`Core.Foundation` / `Core.Diagnostic`**: Memory allocation strategies, logging, and Tracy profiler integration.
- **`3rdparty`**: External dependencies (e.g., Vulkan SDK, Tracy).

### 2. The Binding Layer (`BindingGenerator` / `AutoBinding`)
- **`BindingGenerator`**: A standalone C# console application that reads the C++ AST and generates native export functions and C# wrapper classes.
- **`AutoBinding`**: The generated C# project containing the low-level API projections used by the managed engine.

### 3. The Managed Engine (`Engine`)
Written in C#. This is what developers directly interact with.
- **`Core`**: The `EngineKernel`, Lifecycle definitions, JobSystem, and ECS foundations.
- **`Rendering`**: The high-level `RenderGraph`, material systems, and Render Pipelines that drive the C++ RHI.
- **`Resources`**: The Asset Pipeline (e.g., glTF loading) and resource caches.
- **`Platform`**: C# abstractions bridging the HAL.

### 4. The Editor (`Editor`)
The toolkit and IDE for the engine, built using the modern, cross-platform UI framework **Avalonia**.
- **`ArisenEditorShell`**: The dockable shell and plugin architecture.
- **`ArisenEditor`**: Common editor utilities, property inspectors, and engine viewports.
- **`ArisenEditor.Desktop`**: The entry point executable for the Windows/Desktop platform.

### 5. External Submodules (`External`)
Shared libraries and infrastructure components utilized across multiple projects, including Arisen Engine and Arisen Studio (an AI studio tool).
- **`ArisenDAG`**: A general GraphSystem used across different tools.
- **`ArisenEditorFramework`**: Fundamental editor infrastructure that serves as a base library for the GameEngineEditor and other user extensions. Also used in Arisen Studio.
- **`ArisenNodeCanvas`**: A UI framework used to display and edit the graph system, leveraged by multiple projects.

## Documentation Navigation

This documentation folder is split into several categories for easy navigation:

- **`Roadmap/`**: Development plans, actionable tasks, and feature milestones. Start here to see what we are building next.
- **`Design/`**: Deep-dive technical design documents for specific subsystems (e.g., RHI architecture, ECS and Jobs, Engine Lifecycle).
- **`Projects/`**: Specific game projects in development (e.g., `AngerAsController`).
- **`Media/`**: Development logs, video scripts, and marketing materials.

*(Auto-generated during the Documentation Suite Rebuild)*
