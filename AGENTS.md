# AGENTS.md

This file provides guidance to agentic Code (ai/code) when working with code in this repository.

## Prerequisites

The root `README.md` lists the expected Windows toolchain:

- CMake 3.29+
- Vulkan SDK
- Visual Studio 2022 with MSBuild / C++23 support
- Windows SDK 10+
- .NET SDK 9.0+
- Python 3+

## Documentation ownership

Use a single source of truth for each kind of documentation:

- `README.md` - public overview and quick-start
- `AGENTS.md` - repo-specific AI and developer workflow guidance
- `Arisen\Docs\Architecture` - source-of-truth architecture and build documentation
- `.agents\rules\arisen.md` - thin always-on agents routing rules
- `.agents\skills\*\SKILL.md` - procedural skills

Do not mirror assistant-specific guidance between `Arisen\Docs` and `.agents`. Keep architecture and domain documentation under `Arisen\Docs\Architecture`, and keep runtime guidance under `AGENTS.md`, `.agents\rules`, and `.agents\skills`.

## Common commands

Run commands from the repository root.

### Build the main development workspace

The canonical workspace in this repo is `Arisen\Development\PackageGame`. The main build script defaults to that workspace when no manifest is passed.

- Build all profiles from the default workspace:
  - `Arisen\Scripts\Windows\build_workspace.bat`
- Build a specific profile/configuration:
  - `Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Editor`
  - `Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Development`
  - `Arisen\Scripts\Windows\build_workspace.bat --config Release --profile Production`
- Build a specific workspace manifest explicitly:
  - `Arisen\Scripts\Windows\build_workspace.bat --manifest Arisen\Development\PackageGame\manifest.json --config Debug --profile Development`

What this script does:
- initializes the VS developer environment via `vswhere` + `vcvars64.bat`
- refreshes generated bindings
- builds `ArisenBuildTool`
- generates the solution for the requested profile(s)
- builds native code with CMake when present
- restores NuGet packages and builds the generated solution with MSBuild

### Build the launcher/editor workspace

- `Arisen\Scripts\Windows\build_launcher_all.bat`

This generates the `Arisen\Editor\ArisenLauncher` workspace for package metadata/native components, builds native components in Debug and Release, then builds the real Avalonia launcher desktop app into `.arisen\bin\Development\{Configuration}\`. The runnable launcher apphost at that path is `ArisenLauncher.exe`.

### Refresh bindings only

- Debug bindings: `Arisen\Scripts\Windows\run_binding_generator_debug.bat`
- Release bindings: `Arisen\Scripts\Windows\run_binding_generator_release.bat`

### Test workflow

Fast package/kernel validation is available through a single no-pause command:

- `Arisen\Scripts\Windows\validate_fast.bat`

What this runs:
- `dotnet test Arisen\External\ArisenBuildTool.Tests\ArisenBuildTool.Tests.csproj`
- `dotnet test Arisen\ArisenKernel.Tests\ArisenKernel.Tests.csproj`
- `ArisenBuildTool validate` for the default workspace profiles:
  - `Editor`
  - `Development`
  - `Production`
  - `RHIVulkanTesting`

Use this as the first validation gate for package graph, service-contract, native-test metadata, and runtime package lifecycle changes.

Full runtime validation is available when a change touches boot, generated workspaces, platform windows, RHI startup, profile macros, package loading, or smoke-mode behavior:

- `Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1`

What this runs:
- `validate_fast.bat`
- generated workspace build(s) for the canonical runtime profiles
- bounded runtime smoke launches for generated `PackageGame.exe`
- `Editor`, `Development`, `Production`, and `RHIVulkanTesting` boot coverage where applicable

Use `validate_runtime.bat` as the main local gate for runtime/rendering work. If a GPU-dependent path is unavailable on the machine, report that explicitly instead of hiding it behind a generic build failure.

Package-level workspace generation/testing remains available for native package integration:

- Build an isolated package test workspace:
  - `Arisen\Scripts\Windows\build_workspace.bat --package com.arisen.rhi.vulkan.native --config Debug`
- Build and run the isolated package tests:
  - `Arisen\Scripts\Windows\build_workspace.bat --package com.arisen.rhi.vulkan.native --config Debug --run-tests`

How this works:
- `build_workspace.bat --package X` calls `ArisenBuildTool test --package X`
- the build tool creates a virtual `Testing` workspace that includes:
  - `X`
  - `X.test` (or `X` unchanged if it already ends with `.test`)
  - `com.arisen.testrunner`
  - `com.arisen.core`

The default workspace manifest also contains a concrete testing profile:
- `RHIVulkanTesting` in `Arisen\Development\PackageGame\manifest.json`

### Lint / static analysis

No dedicated lint command or repo-local static-analysis script was found in the current tree. Treat build success as the primary validation path unless the task adds a new checker.

## Important repo notes

- Generated solutions and project files are produced by `ArisenBuildTool`; do not hand-maintain `.sln`, `.csproj`, or native build glue unless the task is specifically about the generator.
- The main generated outputs live under workspace-local `.arisen/`, especially:
  - `.arisen\Projects\{Profile}\`
  - `.arisen\bin\{Profile}\{Configuration}\`

## Big-picture architecture

### Repository shape

The important top-level areas are:

- `Arisen\ArisenKernel` - kernel contracts, bootstrapping, package loading, service registry, subsystem lifecycle
- `Arisen\External\ArisenBuildTool` - manifest/package discovery, topological package resolution, generated solution/CMake/project output
- `Arisen\BindingGenerator` - managed/native binding generation used by the build scripts
- `Arisen\Development\PackageGame` - the canonical development workspace used by the main build script
- `Arisen\Development\PackageGame\Local\com.*` - engine and app packages under active development
- `Arisen\Editor\ArisenLauncher` and `Arisen\Editor\ArisenLauncher.Desktop` - Avalonia launcher/editor host
- `Arisen\Docs\Architecture` - source-of-truth architecture docs for package lifecycle, build generation, rendering, service registry, manifests, and project layout
- `.agents\skills` - procedural skills for review, package creation, DOD guidance, and service-registry usage
- `.agents\rules` - thin always-on routing rules

### Core model: everything is a package

Arisen is a package-centric microkernel. The engine shell is intentionally thin; functionality is assembled by loading packages from a workspace manifest.

A workspace is not itself a package. The workspace root contains `manifest.json`, and actual engine/game logic lives in packages under `Local\...`.

The canonical development workspace currently lives at:
- `Arisen\Development\PackageGame`

Its `manifest.json` defines:
- base packages such as `com.arisen.core`, `com.arisen.ecs`, `com.arisen.rendering`, `com.arisen.generic-renderpipeline`, `com.arisen.rhi.vulkan.native`
- profiles such as `Editor`, `Development`, `Production`, and `RHIVulkanTesting`

Package dependency intent:
- composition/root packages may depend on concrete provider packages because they choose the product assembly, for example selecting `com.arisen.rhi.vulkan.native`
- reusable domain packages should depend on shared contracts/foundation packages and express runtime needs through `services.requires`
- concrete provider packages must declare `services.provides` and any package dependencies required for their own implementation
- a package is only part of the selected graph if the workspace manifest or another selected package dependency pulls it in

### Boot flow

The real entry point pattern is:
1. a thin executable starts
2. it calls `ArisenKernel.Lifecycle.EngineBootstrapper.Run(args)`
3. the bootstrapper resolves the workspace and profile
4. it reads `manifest.json` plus any selected profile packages
5. if present, it prefers `manifest.resolved.json` generated at build time for topologically sorted package order
6. it initializes `EngineKernel` with the resolved package URLs
7. if an `IApplicationHost` service is registered, control is yielded to the UI host; otherwise the kernel runs the default engine loop

Relevant files:
- `Arisen\ArisenKernel\Lifecycle\Bootstrapper.cs`
- `Arisen\Editor\ArisenLauncher.Desktop\Program.cs`

### Build system model

`ArisenBuildTool` is central to the developer workflow.

It reads the workspace manifest, discovers all base/profile packages, resolves package dependencies as a DAG, sorts them topologically, then generates:
- managed project files
- native build files
- per-profile solutions
- resolved manifests and launch config files in `.arisen\bin\...`

Important implications:
- generated IDE files are output artifacts, not the source of truth
- package metadata in `package.json` drives generation
- the workspace manifest controls which package set becomes a runnable/editor/testable application

Relevant files:
- `Arisen\External\ArisenBuildTool\Program.cs`
- `Arisen\Docs\Architecture\ArisenBuildTool.md`
- `Arisen\Docs\Architecture\ConfigurationFormats.md`

### Package contracts and lifecycle

Each package is described by `package.json` and may expose:
- `entry.assembly` / entry class
- package dependencies
- required/provided services
- subsystems
- native runtime payloads

Runtime hooks:
- `IPackageEntry.OnLoad(IServiceRegistry services)`
- `IPackageEntry.OnUnload(IServiceRegistry services)`

Subsystem ordering is two-tiered:
- package dependency DAG first
- then subsystem phase/priority ordering

Current subsystem phases in code:
- `PreInit`
- `Init`
- `PostInit`
- `Running`
- `PreShutdown`
- `Shutdown`

Relevant files:
- `Arisen\ArisenKernel\Packages\IPackageEntry.cs`
- `Arisen\ArisenKernel\Lifecycle\IEngineSubsystem.cs`
- `Arisen\Docs\Architecture\PackageLifecycle.md`

### Service registry rules

Cross-package communication is intentionally decoupled through `IServiceRegistry`.

Use services for macro-level engine systems such as platform, RHI, task graph, editor/application hosts, asset access, etc. Do not use service interface dispatch inside hot loops.

Rules to preserve the architecture:
- domain packages should not directly depend on concrete types from other domain packages
- register and consume interfaces, not concrete implementations
- cache the resolved service, not the registry itself
- do not cast services back to concrete types

Relevant files:
- `Arisen\ArisenKernel\Services\IServiceRegistry.cs`
- `Arisen\Docs\Architecture\ServiceRegistry.md`
- `.agents\skills\use_service_registry\SKILL.md`

### Hot-path / DOD rules

This codebase is strict about DOD and zero-overhead hot paths.

For ECS systems, simulation loops, and render-pass execution:
- prefer `struct` data
- avoid managed allocations in hot paths
- avoid `lock` in hot paths
- avoid interface dispatch in inner loops
- use contiguous ECS/component data and batch processing
- use service registry boundaries for coarse-grained systems, not per-entity work

This is especially important when touching:
- ECS
- TaskGraph/job execution
- RenderGraph pass execution
- managed/native interop boundaries

Relevant docs:
- `.agents\skills\write_dod_code\SKILL.md`
- `Arisen\Docs\Architecture\Rendering.md`
- `Arisen\Docs\Architecture\Profiling.md`

### Rendering stack

The rendering path is layered:
- `com.arisen.dag` - generic DAG/topological ordering
- `com.arisen.rendering` - RenderGraph infrastructure and pipeline base types
- `com.arisen.generic-renderpipeline` - default concrete pipeline assembly
- `com.arisen.rhi.vulkan.native` - Vulkan backend/native driver

Render work is expected to be expressed as RenderGraph passes instead of ad hoc direct orchestration. Multi-threaded rendering is tied to the task graph.

Current platform/RHI ownership policy:
- standalone runtime builds create and pump the main Win32 window through `IWindowProvider`
- editor builds use `ARISEN_ENGINE_EDITOR`; the Avalonia/editor host owns native UI windows and the platform package must not create an independent runtime window loop
- runtime Vulkan initialization must validate `IWindowProvider.GetWindowInfo()` before registering `IRHIDevice`
- editor Vulkan initialization may use a virtual/device-only path until editor-hosted viewport surfaces are implemented
- future DX12/Metal backends should follow the same contracts: common RHI interfaces stay shared, concrete backend packages stay selectable by composition packages

### What to read before making architectural changes

Do not answer architecture questions from memory alone; read the docs and relevant source first.

Start with:
- package/workspace/build questions:
  - `Arisen\Docs\Architecture\ProjectManagement.md`
  - `Arisen\Docs\Architecture\ArisenBuildTool.md`
  - `Arisen\Docs\Architecture\ConfigurationFormats.md`
- boot/lifecycle/dependency questions:
  - `Arisen\Docs\Architecture\ArisenHost.md`
  - `Arisen\Docs\Architecture\PackageLifecycle.md`
  - `Arisen\Docs\Architecture\ServiceRegistry.md`
- rendering questions:
  - `Arisen\Docs\Architecture\Rendering.md`
  - `Arisen\Docs\Architecture\ProductionSceneRenderingNextTodo.md`
- package-boundary questions:
  - `Arisen\Docs\Architecture\PackageArchitecture.md`
  - `Arisen\Docs\Architecture\PackageRegistry.md`

## Working assumptions for edits

- Prefer editing package source and manifests over editing generated project files.
- When adding a package, remember it must be present in the workspace `manifest.json` or it will not load.
- Prefer keeping package dependencies explicit in `package.json`.
- Be cautious when hand-editing auto-derived package metadata such as subsystem/native-runtime details; the build tool and generators are intended to own much of that shape.
- For architecture-sensitive tasks, verify behavior in both docs and source before changing code.
