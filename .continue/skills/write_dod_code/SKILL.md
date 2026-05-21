---
name: write-dod-code
description: Guidelines for high-performance Data-Oriented Design (DOD) in the ECS. Use when optimizing hot paths, simulation loops, or rendering passes.
---

# DOD Performance Checklist

When writing hot-path engine code, follow these Data-Oriented Design rules to preserve zero-overhead execution.

## 1. Hot path definition
A hot path is any code that runs:
- inside an ECS system loop
- every frame during simulation or rendering
- inside RenderGraph pass execution or command recording
- inside task-graph-driven bulk processing

## 2. Zero-overhead essentials
- [ ] Use `struct` for components and hot-path local data.
- [ ] Do not allocate managed objects in the hot path.
- [ ] Avoid interface dispatch inside inner loops.
- [ ] Avoid `lock` in hot paths.

## 3. Memory and iteration
- [ ] Prefer contiguous component storage and batch processing.
- [ ] Use `ComponentPool<T>.GetRawComponentArray()` or equivalent flat-array access when available.
- [ ] Use `NativeArray<T>`, spans, or other bulk-transfer primitives for large cross-boundary data movement.
- [ ] Use transient frame allocators such as `FrameArena` for one-frame scratch memory when the subsystem supports them.

## 4. Verification
- Build and profile in Release when measuring hot-path performance.
- Confirm GC pressure stays out of simulation and rendering loops.
- Validate the final design against the relevant architecture docs and nearby package patterns instead of relying on generic OOP conventions.