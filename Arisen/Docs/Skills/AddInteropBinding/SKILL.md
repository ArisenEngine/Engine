---
name: add_interop_binding
description: "How to expose C++ structures to C#"
---

# Adding Interop Bindings

When writing code that bridges the C++ core and the C# engine, data must be fully blittable.

## Rules:
1. The C# struct must have `[StructLayout(LayoutKind.Sequential)]`.
2. Ensure types match exactly. (`int` in C++ -> `int` in C#, `float` -> `float`, pointers `void*` -> `IntPtr` or `unsafe void*`).
3. For strings, always use raw pointers and unmanaged memory mechanisms instead of implicit marshalling penalties.
4. **NEW:** C++ definitions MUST include the `ARISEN_BIND_PACKAGE` macro to tell the BindingGenerator which package this interop code belongs to.

## Example:

**C# Definition (Engine):**
```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct NativeRenderMesh
{
    public int VertexCount;
    public int IndexCount;
    public unsafe void* VertexData;
    public unsafe void* IndexData;
    public int MaterialId;
}
```

**C++ Definition (Core):**
```cpp
#pragma once
#include <stdint.h>
#include "BindingMacros.h"

ARISEN_BIND_PACKAGE("com.arisen.rendering.core")
ARISEN_BIND_MODULE("Arisen.Rendering.Core.dll")
ARISEN_BIND_NAMESPACE("Arisen.Rendering.Native")

extern "C" {
    struct NativeRenderMesh {
        int32_t VertexCount;
        int32_t IndexCount;
        void* VertexData;
        void* IndexData;
        int32_t MaterialId;
    };
    
    // An exported function that C# would PInvoke
    __declspec(dllexport) void SubmitMesh(NativeRenderMesh* mesh);
}
```
