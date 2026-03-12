# Memory Management Spec

Arisen Engine uses a tiered memory strategy to achieve zero-overhead performance and eliminate Garbage Collection (GC) pauses in simulation loops.

## 1. Frame-Temporary Data (`FrameArena`)
For any data that only needs to live for the duration of a single frame (e.g., transient light lists, temporary math buffers), you **MUST** use the `FrameArena`.

- **Mechanism**: A pre-allocated unmanaged linear buffer.
- **Rules**:
  - **NEVER** store references to `FrameArena` memory across frame boundaries. The buffer is reset to zero offset at the end of every frame.
  - **Usage**: Use `FrameArena.Instance.Alloc<T>(count)` to get a `Span<T>`.
  - **Thread Safety**: The `FrameArena` is per-thread or uses atomic offsets. (Verify current implementation if extending).

## 2. Long-Lived Unmanaged Data (`NativeArray<T>`)
For data that must persist across frames (e.g., Mesh buffers, Large ECS pools) but should stay off the managed heap, use `NativeArray<T>`.

- **Rules**:
  - **MUST** be disposed manually or via `using` blocks to prevent memory leaks.
  - **Thread Safety**: Safe to read from multiple threads. Write with caution.

## 3. Managed Heap Restrictions
- **NO ALLOCATIONS** in the `Update`, `Tick`, or `Render` paths. 
- Use Object Pooling if you absolutely must use managed classes, but prefer `struct` on unmanaged memory.
- Avoid `Garbage Collector` pressure by using `ref` and `in` parameters to pass large structs.
