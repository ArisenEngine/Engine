# Arisen Engine - AI Development Guidelines (GEMINI.MD)

Welcome to the Arisen Engine source tree. This document serves as the absolute constitution and roadmap for AI assistants (like Antigravity/Gemini) and developers working on this codebase.

## 1. The Vision: "Everything is a Package"
Arisen Engine is a **Next-Generation, Package-Centric Microkernel Architecture**. 
- The engine itself is nothing but a thin shell (Kernel) that discovers and loads packages.
- **Ultimate Customization**: Users can do absolutely anything via packages. If they don't like the rendering backend, they can build their own RHI package. If they don't want the default Editor, they write a new Editor package.
- **Beyond Games**: If a user wants to develop a regular desktop application rather than a game, they simply exclude game-specific packages (like ECS or Physics) and use UI/Platform packages instead.

### Current Goal
Our immediate operational goal is to **build out the core game-engine packages** (Rendering, ECS, Editor) and successfully develop a game using this package-centric engine to prove the architecture.

---

## 2. Core Development Principles

Every single line of code written must adhere strictly to these four pillars:

1. **Data-Driven (Data-Oriented Design)**
   - The engine relies heavily on an Entity Component System (ECS).
   - Components MUST be pure data `struct`s with no logic or reference types.
   - Systems MUST process data in bulk arrays in memory-contiguous blocks (e.g. `ComponentPool<T>.GetRawComponentArray()`).

2. **Multi-Thread Friendly**
   - Assume almost all engine systems run concurrently on a Job System.
   - Never mutate shared state outside of designated ECS commands without explicit atomic synchronization.
   - Avoid `lock` statements entirely in hot paths.

3. **High-Performance**
   - Minimize native/managed interop overhead. When bridging C# and C++, batch data and pass large `Span<T>` or `NativeArray` memory chunks to C++ instead of calling functions per-entity.
   - Use `FrameArena` for transient, one-frame memory allocations to eliminate GC pressure.

4. **Zero-Overhead**
   - **No managed allocations in hot paths**: Never `new` objects in Update/Tick or Render loops.
   - Prefer `struct` (value types) over `class` (reference types) throughout the engine.
   - Never use `virtual` methods or object-oriented inheritance for high-frequency game logic.

---

## 3. Documentation Index

To maintain organization, detailed technical specs are split into specific files. When an AI agent encounters a design question, they MUST refer to these index files first:

* **[Project Management Architecture (ProjectManagement.md)](Architecture/ProjectManagement.md)**  
  Defines the directory layout, `.arisenproj` identity rules, and the strict Boot/Creation lifecycles for the Engine.
* **[Configuration Formats (ConfigurationFormats.md)](Architecture/ConfigurationFormats.md)**  
  Defines the exact JSON schemas and data structures for `manifest.json`, `.arisenproj`, and `package.json`.
* **[Package Lifecycle & Initialization (PackageLifecycle.md)](Architecture/PackageLifecycle.md)**  
  Details how the Kernel guarantees deterministic boot order using Topological Dependency Sorting and Engine Phases/Priorities.
* **[ArisenHost Bootstrapping & Binary Resolution (ArisenHost.md)](Architecture/ArisenHost.md)**  
  Details how the universal executable finds workspaces, manages uniform DLL output paths, and safely intertwines C# and C++ memory loading.
* **[ArisenBuildTool Generation (ArisenBuildTool.md)](Architecture/ArisenBuildTool.md)**  
  Defines how the build tool generates transient `.sln` and `.csproj` files to provide a perfect IDE experience without polluting package source control.
* **[Inter-Package Communication (ServiceRegistry.md)](Architecture/ServiceRegistry.md)**  
  The strict rules for how packages fetch and provide services via C# Interfaces without ever statically referencing each other's concrete code.
* **[Asset Pipeline & Resources (AssetPipeline.md)](Architecture/AssetPipeline.md)**  
  Details how the engine achieves Zero-Overhead memory loading using GUIDs, YAML local `.meta` files, and pre-computed binary Cooking.
* **[Package Architecture / Profiles (PackageArchitecture.md)](../Roadmap/PackageArchitecture.md)**  
  Contains the transition plan moving the monolithic engine to packages and defines the Universal Host/Profiles mapping.

***

**Final Note to AI:**
When generating code or suggesting architectures, **you must continuously validate your output against the principles and architectures linked above.** Any violation of zero-overhead, DOD, or Package Interface Strictness is considered a critical failure.
