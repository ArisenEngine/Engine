# Architecture Spec: Package Lifecycle & Initialization Order

**Status**: Draft / Active  
**Module**: ArisenKernel (`PackageSubsystem`, `EngineKernel`)

In an engine where "Everything is a Package," the Kernel's most critical job is ensuring packages and their internal systems boot up (and shut down) in the exact, perfectly deterministic order. 

The Engine achieves this through a **two-tier initialization architecture**: Package-level Topological Sorting and Subsystem-level Phase & Priority grouping.

---

## Tier 1: Package-Level Dependencies (Topological Sort)

Before any code is executed, the Kernel reads the `dependencies` graph from every loaded `package.json`. 

```json
// com.arisen.rhi.vulkan / package.json
{
  "dependencies": {
    "com.arisen.core.native": ">=1.0.0"
  }
}
```

The Kernel builds a **Directed Acyclic Graph (DAG)** of all these dependencies. It then performs a **Topological Sort**. 
If a cycle is detected (e.g., Package A depends on Package B, which depends on Package A), the Kernel immediately throws a Fatal Error and halts.

### The Loading Sequence
Because of the Topological Sort, the Kernel guarantees that `com.arisen.core.native` is loaded into memory and its `IPackageEntry.OnLoad()` is called **strictly before** `com.arisen.rhi.vulkan`. 
This ensures that native foundational layers (like Memory Allocators or Logging) are fully ready before higher-level graphics packages attempt to interact with them.

---

## Tier 2: Subsystems (Phases & Priorities)

Even if packages are loaded in the correct order, we need fine-grained control over exactly *when* specific components tick. To solve this, packages define **Subsystems** in their `package.json`.

```json
"subsystems": [
  {
    "class": "ArisenEngine.RHI.Vulkan.VulkanSubsystem",
    "phase": "Init",
    "priority": 10
  },
  {
    "class": "ArisenEngine.ECS.ECSSubsystem",
    "phase": "Init",
    "priority": 50
  }
]
```

### The Initialization Execution Loop

Once all packages are loaded, the `EngineKernel` looks at all discovered `IEngineSubsystem`s across the entire engine and groups them into strictly ordered **EnginePhases**:

1. `PreInit`: Very low-level systems (Memory, Logging, Config).
2. `Init`: Standard systems (RHI, ECS, Audio, Asset Database).
3. `PostInit`: Systems that depend on standard systems being active (Editor UI, Scene Loaders).
4. `Running`: The active Game/Tick loop.
5. `Shutdown`: Memory cleanup.

Within each phase, the Kernel executes the subsystems sorted by **Priority (Lowest first)**. 

### Working Example

If the Kerne discovers the following subsystems:
* `Logger` (Phase: `PreInit`, Priority: `0`)
* `VulkanRHI` (Phase: `Init`, Priority: `10`)
* `ECS` (Phase: `Init`, Priority: `50`)
* `EditorShell` (Phase: `PostInit`, Priority: `100`)

The execution order will strictly be:
1. `Logger` (PreInit, 0)
2. `VulkanRHI` (Init, 10)
3. `ECS` (Init, 50)
4. `EditorShell` (PostInit, 100)

### Graceful Teardown

When the engine shuts down, the Kernel executes the teardown loop in the **exact reverse order** of Initialization. Validating the teardown order prevents crashes caused by the RHI tearing down while ECS is still trying to flush GPU command buffers.

1. `EditorShell` shuts down.
2. `ECS` shuts down.
3. `VulkanRHI` shuts down.
4. `Logger` flushes and shuts down.
