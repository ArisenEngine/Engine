---
trigger: always_on
---

# Arisen Engine Agentic Routing Rules

- Arisen is a package-centric microkernel: treat workspaces, manifests, and packages as the primary composition model.
- Verify behavior from source code and local documentation before answering architecture, build, lifecycle, rendering, or package-boundary questions.
- Read `AGENTS.md` first for repository workflow, build commands, and documentation ownership.
- Read `Arisen/Docs/Architecture/*.md` before making or describing architectural decisions.
- Use `.agents/skills/*/SKILL.md` for Agentic-specific task procedures.
- For scene/world/rendering work, verify the current policy in `AGENTS.md`, `Rendering.md`, `AssetPipeline.md`, `WorldStreaming.md`, and `TerrainOutdoorWorldNextTodo.md` before changing code.
- Keep this file thin: do not duplicate architecture docs, skill docs, or long-form workflow guidance here.

## Core Development Principles

Every single line of code written must adhere strictly to these pillars:

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

5. **Empirical Reliability (Verify, Don't Assume)**
   - **Never assume state or API behavior.**
   - When encountering uncertainty in core systems (RHI, TaskGraph, ECS), you MUST investigate the source code to find the definitive technical answer.
   - Assumptions lead to architectural drift; discovery of 'Source Truth' is mandatory before implementation.

## File Access Rules (CRITICAL)

- ALL paths are relative to the repository root (`ArisenEngine/`).
- NEVER assume the current working directory.
- NEVER use implicit or guessed paths.
- If a file cannot be found, STOP and report the missing context.
- NEVER hallucinate architecture details.

## Forbidden Behavior

- DO NOT answer architecture questions without reading relevant docs and source code.
- DO NOT assume missing systems or API behaviors.
- DO NOT invent APIs or workflows not defined in docs or verified in source code.
- STOP and investigate if you reach a point of "conceptual" implementation; find the concrete path.

## Context Depth Rules

- For simple questions, read 1-2 relevant files.
- For system design, read multiple Architecture files.
- For critical changes, cross-check ALL related documents.

---

**Final Note to AI:**
When generating code or suggesting architectures, you MUST continuously validate your output against the principles and architectures linked above. Any violation of zero-overhead, DOD, or package interface strictness is a critical failure.
