# Arisen Engine - AI Development Guidelines (GEMINI.MD)

Welcome to the Arisen Engine source tree. This document serves as the absolute constitution and roadmap for AI assistants (like Antigravity/Gemini) and developers working on this codebase.

## 1. The Vision: "Everything is a Package"
Arisen Engine is a **Next-Generation, Package-Centric Microkernel Architecture**. 
- The engine itself is nothing but a thin shell (Kernel) that discovers and loads packages.
- **Ultimate Customization**: Users can do absolutely anything via packages. If they don't like the rendering backend, they can build their own RHI package. If they don't want the default Editor, they write a new Editor package.
- **Beyond Games**: If a user wants to develop a regular desktop application rather than a game, they simply exclude game-specific packages (like ECS or Physics) and use UI/Platform packages instead.

### Current Goal
Our immediate operational goal is to **finalize the core engine infrastructure** (RenderGraph, TaskGraph, ECS) and successfully develop a game using this package-centric microkernel to prove the architecture.

#### Key Milestones
- [x] Decentralize Kernel from Application logic.
- [x] Implement DAG and RenderGraph foundational packages.
- [/] Integrate internal Job System (TaskGraph) into ECS.
- [/] Bridge Vulkan RHI to Editor Viewport (Texture Sharing).

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

## 3. Documentation Index (MANDATORY)

All documentation files are located under:

Engine/Arisen/Docs/

### ⚠️ File Access Rules (CRITICAL)

- ALL paths below are relative to the repository root (ArisenEngine/)
- NEVER assume current working directory
- NEVER use implicit or guessed paths
- If a file cannot be found → STOP and report missing context
- NEVER hallucinate architecture details

---

### 📚 Architecture Documents

- Engine/Arisen/Docs/Architecture/ProjectManagement.md
- Engine/Arisen/Docs/Architecture/ConfigurationFormats.md
- Engine/Arisen/Docs/Architecture/PackageLifecycle.md
- Engine/Arisen/Docs/Architecture/ArisenHost.md
- Engine/Arisen/Docs/Architecture/ArisenBuildTool.md
- Engine/Arisen/Docs/Architecture/ServiceRegistry.md
- Engine/Arisen/Docs/Architecture/AssetPipeline.md
- Engine/Arisen/Docs/Architecture/PackageArchitecture.md
- Engine/Arisen/Docs/Architecture/PackageRegistry.md
- Engine/Arisen/Docs/Architecture/Rendering.md

---

### 🧠 Skills & Implementation Guides

- Engine/Arisen/Docs/Skills/

This directory contains implementation-level knowledge and best practices.
When solving concrete coding or system problems, consult relevant files here.

---

### 🧭 Context Loading Strategy (VERY IMPORTANT)

When solving problems, you MUST load context as follows:

- Architecture / system design questions  
  → Read ALL relevant files under Architecture/

- Build / compilation / project generation issues  
  → Read:
    - ArisenBuildTool.md
    - ArisenHost.md

- Runtime / initialization / dependency issues  
  → Read:
    - PackageLifecycle.md
    - ServiceRegistry.md

- Asset / resource / pipeline issues  
  → Read:
    - AssetPipeline.md

- Package structure / modularization questions  
  → Read:
    - PackageArchitecture.md
    - ProjectManagement.md

---

### 🚫 Forbidden Behavior

- DO NOT answer architecture questions without reading relevant docs
- DO NOT assume missing systems
- DO NOT invent APIs or workflows not defined in docs

# Context Depth Rules

- For simple questions → read 1-2 relevant files
- For system design → read multiple Architecture files
- For critical changes → cross-check ALL related documents

***

**Final Note to AI:**
When generating code or suggesting architectures, **you must continuously validate your output against the principles and architectures linked above.** Any violation of zero-overhead, DOD, or Package Interface Strictness is considered a critical failure.
