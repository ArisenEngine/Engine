# AI Coding Manifesto: Arisen Engine

This document defines the strict, non-negotiable rules for any AI agent interacting with the Arisen Engine codebase.

## 1. Zero-Overhead and Data-Oriented Design (DOD)
- **NEVER** allocate managed classes (`new MyClass()`) in any per-frame hot path (like `Update`, `Tick`, or Render loops).
- **ALWAYS** prefer `struct` (value types) over `class` (reference types) for data containers.
- **NEVER** use `virtual` methods or object-oriented inheritance for hot-path game logic. Use composition and ECS.

## 2. Entity Component System (ECS) Restrictions
- All components **MUST** implement the `IComponent` interface and **MUST** be defined as `struct`.
- Components **MUST NOT** contain any logic or methods, only raw data (fields).
- Systems **MUST** process components in bulk iterating over `ComponentPool<T>.GetRawComponentArray()` or using Jobs.
- **NEVER** store reference types (`class`, `string`, `object`) inside an `IComponent`. Use indices, IDs, or Native strings.

## 3. Parallelism and Multithreading
- Engine systems are fully multithreaded. You **MUST** assume your code will run on multiple threads concurrently.
- **NEVER** mutate shared state outside of your system's designated components without explicit synchronization (e.g., Atomics, Interlocked).
- Avoid `lock` statements in hot paths.

## 4. C# and Native Interop
- When writing bridging code between C# Engine and C++ Core, structures **MUST** be blittable (`[StructLayout(LayoutKind.Sequential)]`).
- **ALWAYS** use `Span<T>`, `ReadOnlySpan<T>`, or `unsafe` pointers when passing large arrays to C++ to avoid GC pinning costs.

## 5. Editor and UI
- The Editor is built on Avalonia UI. Stick to the MVVM pattern for the Editor.
- UI ViewModels should notify property changes (`INotifyPropertyChanged`).
- Editor code **MUST NOT** run C++ engine logic directly on the UI thread if it blocks for more than 16ms.
