# Architecture Spec: Inter-Package Communication (ServiceRegistry)

**Status**: Draft / Active  
**Module**: `ArisenKernel` (`IServiceRegistry`)

The most strict and immutable rule in a Package-Centric Architecture is loose coupling. **A package MUST NEVER directly reference the concrete types of another domain package.** 

If `com.arisen.rendering` directly references `class VulkanDevice` inside `com.arisen.rhi.vulkan`, then "swapping the RHI package" becomes completely impossible. 

To solve this, Arisen Engine relies entirely on a **Service Locator Pattern** driven by the `IServiceRegistry`.

---

## 1. The Core Philosophy

Packages communicate explicitly through abstract C# interfaces. These interfaces are defined in ultra-lightweight, foundation packages (like `com.arisen.core` or a dedicated `ArisenKernel.Contracts` assembly).

- **Providers**: Packages register concrete classes that implement `IService` contracts.
- **Consumers**: Packages ask the Kernel for implementations of `IService` contracts.

---

## 2. Registering Services (Providers)

When a package is designed to provide core functionality to the rest of the engine, it must do two things:

**1. Declare the intent in `package.json`**
The package tells the Kernel what interfaces it provides. The current service registry is single-provider: the active workspace/profile must select exactly one provider package for each service contract. The optional `priority` field is accepted as metadata for future explicit provider selection, but it does not make duplicate selected providers valid today.
```json
"services": {
  "provides": [
    { "interface": "ArisenEngine.Core.RHI.IRHIDevice", "priority": 100, "capabilities": ["vulkan"] }
  ]
}
```

**2. Register the concrete instance in `IPackageEntry.OnLoad`**
During the Topological Boot phase, the package spins up its internal types and hands them to the registry.
```csharp
public class VulkanPackageEntry : IPackageEntry
{
    public void OnLoad(IServiceRegistry registry)
    {
        var vulkanDevice = new VulkanRHIDevice();
        
        // The Kernel now holds this instance globally
        registry.Register<IRHIDevice>(vulkanDevice);
    }
}
```

---

## 3. Consuming Services

When another package's `IEngineSubsystem` ticks and needs to draw something, it never knows it is talking to Vulkan.

**1. Declare the requirement in `package.json`**
```json
"services": {
  "requires": [
    "ArisenEngine.Core.RHI.IRHIDevice"
  ]
}
```
*(The Kernel validates this before Boot Matrix execution. If NO loaded package provides `IRHIDevice`, the Kernel halts with a Fatal Error: "Unsatisfied Service Dependency" before any code crashes.)*

**2. Retrieve the Service in C#**
```csharp
public class MeshRenderSubsystem : IEngineSubsystem
{
    private IRHIDevice _rhi;

    public void OnInit(IServiceRegistry registry)
    {
        // Safely acquire the backend
        _rhi = registry.Get<IRHIDevice>();
    }
    
    public void OnTick()
    {
        _rhi.DrawMesh(...); // Could be Vulkan, DX12, or Metal!
    }
}
```

---

The canonical service declaration format is intentionally small:

- simple string form: `"Namespace.IContract"`;
- object form: `{ "interface": "Namespace.IContract" }`;
- service contract names must be fully qualified type names, not short names such as `"IApplicationHost"`;
- object metadata:
  - `priority` integer is allowed on providers for future profile-driven provider selection;
  - `capabilities` array of non-empty strings is allowed on providers/requirements; when a requirement lists capabilities, `ArisenBuildTool validate` requires a selected provider for the same interface to advertise all requested capabilities;
  - `optional: true` is valid on requirements and means missing providers produce warnings instead of errors;
  - `deferred: true` is valid on providers or requirements and means build-time provider validation still runs, but initial runtime registration checks are skipped.

The validator accepts both service declaration forms:

```json
"services": {
  "provides": [
    "ArisenEngine.Core.RHI.IRHIDevice",
    { "interface": "ArisenEngine.Core.RHI.IRHIFactory", "priority": 100, "capabilities": ["vulkan"] }
  ],
  "requires": [
    "ArisenEngine.Core.RHI.IRHIDevice",
    { "interface": "ArisenEngine.Core.RHI.IDebugCapture", "optional": true },
    { "interface": "ArisenEngine.Core.RHI.ILateBoundDevice", "deferred": true }
  ]
}
```

`ArisenBuildTool validate` checks service contracts during package graph validation:

- malformed `services.provides` / `services.requires` entries are fatal,
- empty service contract names are fatal,
- unqualified service contract names without a namespace separator (`.`) are fatal,
- object-form `priority` must be an integer when present and is only valid in `services.provides`,
- object-form `capabilities` must be an array of non-empty strings when present,
- object-form `optional` is only valid in `services.requires`,
- a required service with no selected provider package is fatal,
- a required service with requested capabilities is fatal if the selected provider for that interface does not advertise those capabilities,
- an optional service with no selected provider package logs a warning,
- an optional service with requested capabilities logs a warning if the selected provider does not advertise those capabilities,
- `deferred` service contracts still participate in build-time graph/provider validation but are not required to be registered during initial package mount,
- duplicate selected providers are fatal. The current `IServiceRegistry` stores one active provider per contract and does not support automatic priority-based overrides.

Runtime boot performs a second validation pass through `PackageSubsystem`:

- while a package entry runs `OnLoad()`, `ServiceRegistry` records the current package as the provider context,
- metadata-driven subsystems are instantiated by `PackageSubsystem` and registered as concrete services with their provider package context so editor/runtime tooling can resolve them,
- after `OnLoad()` and metadata subsystem registration, every non-deferred declared `services.provides` contract must have been registered by that same package,
- after all packages mount, every non-optional and non-deferred declared `services.requires` contract must be present in the registry,
- `ServiceRegistry.GetRegisteredServices()` exposes contract, implementation, and provider package metadata for diagnostics/editor UI.

This two-stage validation catches both graph-level mistakes before generation and implementation-level mistakes during boot.

---

## 4. Architectural Rules for Services

1. **No Data Storage in Services**: Services should generally be stateless managers or gateways (like `IAssetDatabase`, `IRHIDevice`, `ITaskGraph`, `IPhysicsWorld`). Game-state data MUST live in the ECS `ComponentPool<T>`.
2. **Read-Only Interfaces**: Prefer using `IReadOnly...` interfaces if a package is exposing data that shouldn't be mutated by other packages.
3. **Never Cache the Registry**: Subsystems should fetch the services they need during `OnInit()` and cache the service instance itself, *not* the global `IServiceRegistry`. 
4. **Strict Cast Prevention**: Developers MUST NOT attempt to cast a Service Interface back to its concrete type (e.g. `(VulkanRHIDevice)registry.Get<IRHIDevice>()`). Doing so bypasses the package boundaries and breaks the architectural paradigm.

---

## 5. The Performance Question: Virtual Dispatch Overhead

A common concern with strictly communicating via `interfaces` is performance. In .NET, calling an interface method requires a virtual dispatch (v-table lookup), which prevents inlining and destroys CPU cache locality.

If Arisen Engine demands **Zero-Overhead** and **Data-Oriented Design (DOD)**, how do we use interfaces?

**The Golden Rule:**
**The `IServiceRegistry` and interface communication are strictly for MACRO-LEVEL subsystems.**
- Calling `IRHIDevice.SubmitCommandBuffer()` once per frame has a performance cost of 0.0001%. It is completely negligible.
- Calling `IVirtualEntity.UpdateXYZ()` 100,000 times in a `for` loop would absolutely destroy the frame rate.

**How do Packages share High-Frequency Hot Path code?**
They don't use the Service Registry for Hot Paths! They use the **ECS (Entity Component System)** locally.
If `com.arisen.physics` needs to communicate positions to `com.arisen.rendering`, they do NOT call interface methods on each other. 
1. Both packages simply depend on the same shared `struct` definition (`struct TransformComponent`).
2. Both packages ask the ECS for a contiguous `NativeArray<TransformComponent>`.
3. They iterate instantly over flat memory. **Zero interfaces. Zero virtual calls. 100% Native C++ speed.**

---

## 6. Static Methods & Direct Assembly References

If interfaces are too slow for hot-paths, and Package A absolutely *must* call a static helper method in Package B, how does `ArisenBuildTool` handle this? Can we just define a dependency in `package.json` and call `PackageB.StaticClass.DoWork()`?

**The answer depends entirely on the TYPE of package.**

### 1. Foundation / Core Packages (YES)
If Package B is a Foundation package (e.g., `com.arisen.core`, `com.arisen.math`, `com.arisen.ecs`), **YES**.
These packages contain pure data structures, math libraries, and fundamental types. They do not contain any "domain execution" logic. 
When `ArisenBuildTool` sees that your game depends on `com.arisen.math`, it actually generates a direct `<ProjectReference>` in your `.csproj`. You can call `ArisenMath.FastSin()` directly in your hot path with zero virtual overhead.

### 2. Domain / Feature Packages (ABSOLUTELY NOT)
If Package B is a Domain package (e.g., `com.arisen.physics.jolt`, `com.arisen.rhi.vulkan`, `com.arisen.audio.fmod`), **NO**.
You cannot directly reference them. If your Game package statically linked to `JoltPhysics`, your game would crash if a user tried to boot it with `PhysX` instead. You have permanently destroyed the Microkernel architecture. 
If you need hot-path communication between domain packages, they must communicate purely by iterating over shared ECS `struct` data that was defined in a Foundation package.
