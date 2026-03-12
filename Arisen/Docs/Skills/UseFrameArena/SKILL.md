---
name: use_frame_arena
description: "How to allocate transient memory from the FrameArena."
---

# Using the FrameArena

The `FrameArena` is your primary tool for per-frame allocations. It is extremely fast because it never calls `new` and is reset at the end of every frame.

## Rules
1. **Never** store the resulting `Span<T>` or a pointer to it outside the current frame loop.
2. Only use for `unmanaged` types (structs, primitives).

## Example

```csharp
using ArisenEngine.Core.Memory;

public void ProcessLightVisibility()
{
    // Need a temporary list of visible light IDs
    int maxPotentialLights = 1024;
    Span<int> visibleLightIndices = FrameArena.Instance.Alloc<int>(maxPotentialLights);
    
    int actualCount = 0;
    // ... cull lights and fill visibleLightIndices ...
    
    // Pass to renderer
    SubmitLights(visibleLightIndices.Slice(0, actualCount));
}
```
