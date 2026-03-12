---
name: create_cpp_subsystem
description: "How to define and integrate a new C++ Subsystem in Core."
---

# Creating a C++ Core Subsystem

When adding a new low-level native pipeline (e.g., a custom Physics solver or a new RHI backend feature), it must adhere to the engine's memory and lifecycle patterns.

## Rules:
1. **No Shared Pointers in hot paths**: Use raw pointers or opaque Handles.
2. **Pre-allocate Memory**: Avoid `new`/`malloc` in the `Update`/`Tick` logic.
3. **C API Export**: If the C# Engine layer needs to call it, expose it via a flat C API `extern "C"` and use the BindingGenerator macros.

## Example:

```cpp
#pragma once
#include "CoreFoundationCommon.h"

namespace ArisenEngine::Physics 
{
    // Opaque handle for C#
    typedef UInt32 PhysicsBodyHandle;
    constexpr PhysicsBodyHandle InvalidPhysicsBody = 0xFFFFFFFF;

    class PhysicsSolver 
    {
    public:
        void Initialize(UInt32 maxBodies);
        void Shutdown();

        // Hot Path - No Allocations Here!
        void StepSimulation(float deltaTime);

        PhysicsBodyHandle CreateBody();
    private:
        // Raw array pre-allocated during Initialize
        void* m_BodyDataArena;
    };
}

// Flat API for C# Interop
extern "C" {
    __declspec(dllexport) void Physics_StepSimulation(ArisenEngine::Physics::PhysicsSolver* solver, float dt)
    {
        solver->StepSimulation(dt);
    }
}
```
