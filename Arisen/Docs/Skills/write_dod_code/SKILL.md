---
name: Write Zero-Overhead DOD Code
description: Guidelines for writing high-performance Entity Component System (ECS) hot-paths.
---

# Writing Data-Oriented (DOD) Hot Paths
If the user asks you to optimize a loop, build a high-frequency system, or pass data between packages during the simulation tick, you MUST adhere to the Zero-Overhead rules defined in `Docs/GEMINI.MD` and `Docs/Architecture/ServiceRegistry.md`.

## 1. Ban on Interfaces in Hot Paths
**CRITICAL:** You must NEVER call `IServiceRegistry` or any C# `interface` virtual method inside an entity loop (`Update`, `Tick`, `Render`). Virtual dispatch destroys CPU cache locality. The Service Registry is for Macro-System initialization ONLY.

## 2. Ban on Managed Allocations
Never allocate classes (`new MyObject()`) in the hot path. This creates Garbage Collection (GC) pressure.
Use purely unmanaged `struct` types. If you need transient memory for a single frame, use `FrameArena.Instance.Alloc<T>()` and do not store the reference.

## 3. Communicating via ECS
If `com.arisen.physics` and `com.arisen.rendering` need to share position data natively, they DO NOT statically reference each other or call methods on each other!
1. Rely on a shared `struct` defined in a Foundation package (e.g. `struct TransformComponent`).
2. Ask the ECS for the `NativeArray<TransformComponent>`.
3. Iterate directly over the flat memory using `Span<T>` or pointers. This guarantees 100% C++ equivalent speed.
