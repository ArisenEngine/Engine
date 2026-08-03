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
3. `EngineKernel` passes package URLs plus every selected workspace/profile constraint and each exact build-resolved package version to `PackageSubsystem.MountPackages()`.
4. `PackageSubsystem` reads each effective `package.json` from either a source package root or deployed `Packages/<id>/`, validates every selected and dependency constraint, rejects dependency cycles, and topologically sorts the descriptors it just read. It then loads the co-located entry assembly if present, creates the entry class, calls `IPackageEntry.OnLoad(IServiceRegistry)`, invokes any declared native `initExport` hooks, validates declared service providers/requirements, and records `ArisenPackageInfo`.
5. Only after every selected package and the final required-service set validate does `EngineKernel` commit `IsPackageGraphMounted` and retain the supplied `EngineConfig`.
6. `PackageSubsystem.Shutdown()` calls `IPackageEntry.OnUnload(IServiceRegistry)` and any declared native `shutdownExport` hooks in reverse mount order, then unregisters services and subsystem registrations provided by each package after that package finishes unloading. The `com.arisen.core` provider completes the diagnostics logger from its own `OnUnload`, after all dependent packages have emitted their teardown diagnostics. It closes native callback admission, drains in-flight native handlers, flushes and joins the asynchronous file queue, drains the managed notification dispatcher, releases event subscribers, and invalidates the kernel logger cache before the remaining provider/kernel messages fall back to the console.

This avoids split-brain package state between bootstrapper, kernel, and package tracking UI. It also centralizes runtime service-contract validation so a package that declares non-deferred `services.provides` must actually register those services during `OnLoad()`, and all non-optional/non-deferred `services.requires` contracts must exist before subsystem initialization continues.

Runtime package lifecycle behavior is covered by `ArisenKernel.Tests`:

```bat
dotnet test Arisen\ArisenKernel.Tests\ArisenKernel.Tests.csproj
```

The tests create temporary package manifests, boot them through `EngineKernel.Initialize()`, verify package entry loading and service registration, assert that shutdown unloads package entries in reverse mount order, confirm package-provided services are removed during shutdown, and cover package-only mount/unload with no subsystem phases. Deterministic fault fixtures inject managed type resolution, `OnLoad`, service validation, native load/init/shutdown, subsystem registration/initialize/shutdown, collectible-context, and `OnUnload` failures. Every fixture asserts the remaining package, service, subsystem, context, native-handle, phase, and mounted state rather than relying on elapsed time.

### Transactional Mount And Rollback

Package mounting is an all-or-nothing operation at two levels:

- each package provisionally owns its managed context, entry instance, package-attributed services and subsystems, initialized native runtimes, and metadata until provider validation succeeds;
- the requested graph provisionally owns every package added by that mount call until final required-service validation succeeds.

If a stage fails, `PackageSubsystem` rolls back in reverse package/runtime order. A completed managed `OnLoad` receives one `OnUnload` attempt; an `OnLoad` that threw does not. Native runtimes receive `shutdownExport` only after their `initExport` completed successfully, and every loaded library is freed even when a shutdown hook fails. Package-attributed service and subsystem registrations are removed, package records are discarded, and collectible contexts are marked for unload. If rollback itself reports errors, the original mount failure and every cleanup failure are returned together as an `AggregateException` after all independent cleanup has run.

`EngineKernel.MountPackageGraph()` exposes `Config` provisionally because package entries need it during `OnLoad`, but restores the previous value and removes an auto-created `PackageSubsystem` when mounting fails. A clean rollback may be followed by a new mount attempt without resetting the kernel.

Build-stage source resolution is a separate explicit operation. `ResolveBuildStagePackageGraph()`
first verifies the finalized runtime `manifest.resolved.json` beside the executing cook host,
including native payload hashes. It then reads `manifest.source.resolved.json` only to recover source
package URLs, while still validating package compatibility and native basename ownership. A
non-finalized source manifest is therefore never accepted as the runtime integrity authority.

### Managed Assembly Load Context Policy

`PackageSubsystem` owns the managed package assembly load policy:

- `ArisenKernel.dll` entry declarations are resolved to the already-loaded kernel assembly.
- Entry assemblies resolved under `AppContext.BaseDirectory` are loaded in the default context. This is the expected path for generated workspace outputs and shared engine assemblies that must exchange kernel contract types without type identity splits.
- Entry assemblies resolved from package-local roots such as `Managed/` are loaded in a collectible `PackageLoadContext`. The context uses `AssemblyDependencyResolver` for package-private managed and unmanaged dependencies.

Unloadability is best-effort and applies only to assemblies loaded through `PackageLoadContext`. `PackageSubsystem.Shutdown()` first calls `IPackageEntry.OnUnload()` in reverse package order, runs native shutdown hooks, unregisters services and subsystems provided by each unloaded package, removes package state, and then marks that package's collectible context for unload. Actual memory reclamation depends on package code releasing all references to objects, types, delegates, threads, and unmanaged callbacks from that context. Default-context assemblies are intentionally process-lifetime assemblies and are not unloadable.

### Asynchronous Unload Ownership

A package may release a task queue, event delegate, callback pointer, synchronization primitive, or
assembly context only after it has stopped new admission and proven that all admitted work is
complete. Unsubscription alone is not proof that an already-running callback returned, and a native
function pointer must remain bound to valid managed code until the native owner reports zero
in-flight calls. Package teardown must aggregate dispatch and cleanup failures after attempting every
independent release; it must not convert them into detached tasks or process-exit races.

Core diagnostics is the reference implementation of this rule:

1. managed event subscription admission closes while existing subscribers remain valid;
2. native logger state changes from `Accepting` to `StopRequested`;
3. the Foundation handler is detached under an admission counter and all borrowed handler calls
   drain;
4. the logger waits for every accepted log and reverse P/Invoke callback, then clears the callback
   pointer;
5. spdlog flushes and joins its asynchronous file worker;
6. the bounded managed notification queue stops admission, dispatches accepted messages in order,
   reports overflow or subscriber failures, and joins its one owned worker;
7. only after both native and managed drains complete are dispatcher targets and event subscribers
   cleared.

The managed dispatcher uses explicit `Accepting -> StopRequested -> Drained -> Disposed/Faulted`
states. Correctness does not depend on a timeout, sleep, retry, process exit, or garbage collection.

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
As defined in [ArisenHost.md](ArisenHost.md), the **EngineBootstrapper** resolves which package directories should participate in boot. Before returning that graph it independently validates every resolved dependency constraint, each package's `engine.minVersion`, the finalized build configuration, native basename ownership, finalized owner sets, sizes, and SHA-256 bytes. The configuration recorded in `manifest.resolved.json` filters configuration-specific native declarations even when a deployed output is relocated outside its original `Debug` or `Release` directory. Compatibility and integrity failures are hard failures even when diagnostic manifest fallback was requested. It preserves all base/profile requirements and adds every exact version recorded by the resolved manifest. `PackageSubsystem` validates those requirements against the effective descriptors it will load, rejects cycles, and sorts that current graph before any entry, native lifecycle hook, service, or subsystem mutates engine state. The kernel therefore does not trust stale URL order when a source descriptor changed. A dependency such as `com.arisen.rhi.vulkan -> com.arisen.core.native` guarantees that `com.arisen.core.native` loads first. Finalized Production manifests point at metadata-only deployed package directories; workspace profiles retain source package roots for authoring and import.
This ensures that native foundational layers (like Memory Allocators or Logging) are fully ready before higher-level graphics packages attempt to interact with them.

### Package-Only Mounting

`EngineKernel.MountPackageGraph(EngineConfig)` is the explicit boundary for package-aware tools that need package entries and coarse services but must not start the engine. It performs the same `PackageSubsystem.MountPackages()` operation as normal initialization while keeping `CurrentPhase` at `None`:

- package entries run `OnLoad()` and register services/providers;
- package metadata may register subsystem objects with the kernel;
- no subsystem `Initialize()` method runs, so windows, RHI devices, render loops, and live scene activation are not started;
- `IsPackageGraphMounted` becomes true only after the complete selected graph validates and distinguishes this state from both an untouched and a fully initialized kernel;
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

When a subsystem's `Initialize()` call begins, the Kernel records that subsystem as started before invoking it. If initialization throws after partial setup, terminal shutdown still gives that subsystem one cleanup attempt and then continues through every subsystem that started earlier.

When the engine shuts down, the Kernel executes subsystem teardown in reverse subsystem order. An exception from one subsystem is collected instead of stopping later cleanup. `PackageSubsystem` is deliberately held out of ordinary phase reversal and runs after every other started subsystem, including package-owned `PreInit` subsystems, so no subsystem outlives its package entry, service, native runtime, or assembly. It then unloads package entries in **reverse package mount order**. Diagnostics completion belongs to the Core provider rather than an application host: Editor returning from its UI loop does not shut down logging while Vulkan, rendering, and package teardown still need it.

1. `EditorShell` shuts down.
2. `ECS` shuts down.
3. `VulkanRHI` shuts down.
4. Package entries unload in reverse topological order, including concrete rendering/RHI providers.
5. `com.arisen.core` emits the final diagnostics-completion marker, drains native callbacks and the asynchronous file queue, then joins the ordered managed event dispatcher and releases its subscribers.
6. `com.arisen.core.native` and the remaining kernel state finish teardown with console fallback after the logger service is removed.

After all cleanup attempts, `EngineKernel` enters `Shutdown` and throws one aggregate when any cleanup failed. Repeated `Shutdown()` calls do not execute hooks twice; they return the same stored aggregate so an incomplete teardown cannot be mistaken for success.
