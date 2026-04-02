---
name: write-dod-code
description: Guidelines for high-performance Data-Oriented Design (DOD) in the ECS. Use when optimizing hot paths, simulation loops, or rendering passes.
---

# DOD Performance Checklist

When writing high-performance engine code (hot paths), you MUST follow these Data-Oriented Design (DOD) principles to achieve zero-overhead execution.

## 1. Hot Path Definition
A hot path is any code that runs:
- Inside an ECS System loop.
- Every frame during the simulation tick (`Update`, `Tick`).
- Inside the RenderGraph command recording.

## 2. Zero-Overhead Essentials
- [ ] **Ban on Classes**: Do not use `class` for components or hot-path local data. Use `struct`.
- [ ] **Ban on Managed Allocations**: No `new` keywords in the hot path. These cause GC pressure.
- [ ] **Ban on Interfaces**: Never call a C# `interface` method inside a loop. Virtual dispatch kills CPU cache hits.
- [ ] **Ban on Locks**: Do not use `lock` statements for thread safety in hot paths. Use atomics or ECS commands.

## 3. Memory & Iteration
- [ ] **Flat Arrays**: Use `ComponentPool<T>.GetRawComponentArray()` for bulk processing.
- [ ] **Native Buffers**: For large data transfers between packages, use `NativeArray<T>`.
- [ ] **Transient Memory**: For single-frame allocations, use `FrameArena.Instance.Alloc<T>()`.
- [ ] **Pointer Iteration**: Prefer `Span<T>` or `fixed` pointer blocks when high-speed access to large data is required.

## 4. Verification
Benchmarks and performance verification:
1. Run with `Release` profile.
2. Monitor GC pauses (should be zero in simulation loops).
3. Use `scan-build.bat` for static analysis of performance-critical code.
