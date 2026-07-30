# Architecture Spec: Package Lifecycle & Initialization Order

**Status**: Draft / Active  
**Module**: ArisenKernel (`PackageSubsystem`, `EngineKernel`)

In an engine where "Everything is a Package," the Kernel's most critical job is ensuring packages and their internal systems boot up (and shut down) in the exact, perfectly deterministic order. 

The Engine achieves this through a **two-tier initialization architecture**: Package-level Topological Sorting and Subsystem-level Phase & Priority grouping.

---

### Ownership Rule

`PackageSubsystem` is the single runtime owner of package mount state. `EngineBootstrapper` resolves either the workspace/profile package URL list or the deployed output-owned package list, and `EngineKernel` coordinates lifecycle, but neither should directly instantiate package entry classes or maintain an independent loaded-package collection.

Runtime package mounting follows this responsibility split:

1. `EngineBootstrapper` resolves project root, profile, `manifest.json`, and preferably `manifest.resolved.json`. A `Mode: Deployed` launch roots all of these beside the executable and rejects workspace redirection.
2. `EngineKernel.Initialize()` ensures `PackageSubsystem` exists and mounts the graph when it has not already been mounted through the package-only boundary.
3. `EngineKernel` passes the already ordered package URL list to `PackageSubsystem.MountPackages()`.
4. `PackageSubsystem` reads each effective `package.json` from either a source package root or deployed `Packages/<id>/`, loads the co-located entry assembly if present, creates the entry class, calls `IPackageEntry.OnLoad(IServiceRegistry)`, invokes any declared native `initExport` hooks, validates declared service providers/requirements, and records `ArisenPackageInfo`.
5. `PackageSubsystem.Shutdown()` calls `IPackageEntry.OnUnload(IServiceRegistry)` and any declared native `shutdownExport` hooks in reverse mount order, then unregisters services provided by each package after that package finishes unloading.

This avoids split-brain package state between bootstrapper, kernel, and package tracking UI. It also centralizes runtime service-contract validation so a package that declares non-deferred `services.provides` must actually register those services during `OnLoad()`, and all non-optional/non-deferred `services.requires` contracts must exist before subsystem initialization continues.

Runtime package lifecycle behavior is covered by `ArisenKernel.Tests`:

```bat
dotnet test Arisen\ArisenKernel.Tests\ArisenKernel.Tests.csproj
```

The tests create temporary package manifests, boot them through `EngineKernel.Initialize()`, verify package entry loading and service registration, assert that shutdown unloads package entries in reverse mount order, confirm package-provided services are removed during shutdown, cover package-only mount/unload with no subsystem phases, and cover native lifecycle metadata failure behavior.

### Managed Assembly Load Context Policy

`PackageSubsystem` owns the managed package assembly load policy:

- `ArisenKernel.dll` entry declarations are resolved to the already-loaded kernel assembly.
- Entry assemblies resolved under `AppContext.BaseDirectory` are loaded in the default context. This is the expected path for generated workspace outputs and shared engine assemblies that must exchange kernel contract types without type identity splits.
- Entry assemblies resolved from package-local roots such as `Managed/` are loaded in a collectible `PackageLoadContext`. The context uses `AssemblyDependencyResolver` for package-private managed and unmanaged dependencies.

Unloadability is best-effort and applies only to assemblies loaded through `PackageLoadContext`. `PackageSubsystem.Shutdown()` first calls `IPackageEntry.OnUnload()` in reverse package order, runs native shutdown hooks, unregisters services provided by each unloaded package, clears package state, and then unloads collectible contexts. Actual memory reclamation depends on package code releasing all references to objects, types, delegates, threads, and unmanaged callbacks from that context. Default-context assemblies are intentionally process-lifetime assemblies and are not unloadable.

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

### The Loading Sequence (Mounting)
As defined in [ArisenHost.md](ArisenHost.md), the **EngineBootstrapper** resolves which package directories should participate in boot. Actual package mounting is then performed by **PackageSubsystem**. Because `ArisenBuildTool` writes `manifest.resolved.json` in topological order and the bootstrapper prefers it, the kernel guarantees that `com.arisen.core.native` is loaded into memory and its `IPackageEntry.OnLoad()` is called **strictly before** `com.arisen.rhi.vulkan`. Finalized Production manifests point at metadata-only deployed package directories; workspace profiles retain source package roots for authoring and import.
This ensures that native foundational layers (like Memory Allocators or Logging) are fully ready before higher-level graphics packages attempt to interact with them.

### Package-Only Mounting

`EngineKernel.MountPackageGraph(EngineConfig)` is the explicit boundary for package-aware tools that need package entries and coarse services but must not start the engine. It performs the same `PackageSubsystem.MountPackages()` operation as normal initialization while keeping `CurrentPhase` at `None`:

- package entries run `OnLoad()` and register services/providers;
- package metadata may register subsystem objects with the kernel;
- no subsystem `Initialize()` method runs, so windows, RHI devices, render loops, and live scene activation are not started;
- `IsPackageGraphMounted` distinguishes this state from both an untouched and a fully initialized kernel;
- `Shutdown()` directly shuts down `PackageSubsystem` when subsystem phases never began, preserving reverse package unload and service removal;
- a later `Initialize()` may continue only with the same `EngineConfig` instance, preventing a mounted graph from being initialized under different composition data.

The generated runtime asset cook host uses this path directly. `EngineBootstrapper` also mounts before initialization so an `IApplicationHost` with `RequiresEngineInitialization == false` can take over at this boundary. The native test runner uses that option to prevent ordinary game subsystems from creating a second window or a competing RHI instance. Smoke launches intentionally continue through full initialization.

This is a lifecycle boundary, not a second package loader: `PackageSubsystem` remains the sole owner of assembly contexts, native lifecycle hooks, package records, runtime service validation, and reverse unload.

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

Within each phase, the Kernel executes subsystems deterministically by package topological order, then subsystem priority, then class-name tie-breaker. Package metadata can declare subsystem class names; `PackageSubsystem` instantiates those classes from the package assembly and validates they implement `IEngineSubsystem` before phase initialization starts.

### Working Example

If the Kernel discovers the following subsystems:
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

When the engine shuts down, the Kernel executes subsystem teardown in reverse subsystem order, and `PackageSubsystem` unloads package entries in **reverse package mount order**. Validating the teardown order prevents crashes caused by dependent packages or subsystems outliving their providers.

1. `EditorShell` shuts down.
2. `ECS` shuts down.
3. `VulkanRHI` shuts down.
4. Package entries unload in reverse topological order, and their service registrations are removed after each package unloads.
5. `Logger` flushes and shuts down.
