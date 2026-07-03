# ArisenEngine

ArisenEngine is a package-centric microkernel engine and editor stack for learning, research, and high-performance runtime architecture. The engine stays intentionally thin: workspaces declare packages, packages provide the real functionality, and `ArisenBuildTool` generates the runnable/editor/testable outputs from that package graph.

## Requirements

| Component | Version |
| --- | --- |
| Visual Studio | 2022 with MSBuild and C++23 toolchain |
| Windows SDK | 10+ |
| CMake | 3.29+ |
| .NET SDK | 9.0+ |
| Vulkan SDK | Latest recommended |
| Python | 3+ |

## Repository layout

- `Arisen/ArisenKernel` - kernel contracts, bootstrapping, package loading, service registry, subsystem lifecycle
- `Arisen/External/ArisenBuildTool` - workspace/package discovery, dependency resolution, project and solution generation
- `Arisen/Development/PackageGame` - canonical development workspace
- `Arisen/Development/PackageGame/Local/com.*` - engine, editor, rendering, and testing packages under active development
- `Arisen/Editor/ArisenLauncher` - launcher/editor workspace
- `Arisen/BindingGenerator` - managed/native binding generation
- `Arisen/Docs/Architecture` - source-of-truth architecture and build documentation

## Canonical workspace

The main development workspace is:

- `Arisen/Development/PackageGame`

Its source-of-truth manifest is:

- `Arisen/Development/PackageGame/manifest.json`

The workspace currently defines these main profiles:

- `Editor` - editor-enabled authoring workspace with `com.arisen.editor`
- `Development` - standalone runtime profile with diagnostics/profiler enabled
- `Production` - standalone runtime profile without profiler instrumentation
- `RHIVulkanTesting` - test runner plus Vulkan native test package

The base workspace package set includes the core kernel/runtime, ECS, DAG, desktop platform, resources, rendering, and Vulkan backend packages.

## Build the main workspace

Run commands from the repository root.

Build all profiles from the canonical workspace:

```bat
Arisen\Scripts\Windows\build_workspace.bat
```

Build a specific profile/configuration:

```bat
Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Development
Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Editor
Arisen\Scripts\Windows\build_workspace.bat --config Release --profile Production
```

Build an explicit workspace manifest:

```bat
Arisen\Scripts\Windows\build_workspace.bat --manifest Arisen\Development\PackageGame\manifest.json --config Debug --profile Development
```

`build_workspace.bat` performs the current end-to-end workspace pipeline:

- locates and initializes the Visual Studio developer environment
- refreshes generated bindings
- builds `ArisenBuildTool`
- resolves workspace packages and generates per-profile solutions
- builds generated native projects with CMake when present
- restores NuGet packages and builds the generated solution with MSBuild

## Build the launcher/editor workspace

Build the launcher/editor workspace with:

```bat
Arisen\Scripts\Windows\build_launcher_all.bat
```

This generates the launcher workspace, builds native components in Debug and Release, then builds the managed launcher solution in Debug and Release.

## Refresh bindings only

```bat
Arisen\Scripts\Windows\run_binding_generator_debug.bat
Arisen\Scripts\Windows\run_binding_generator_release.bat
```

## Current test workflow

This repository does not currently expose a standard `dotnet test` / xUnit / NUnit workflow.

The active testing path is package-level workspace generation through `ArisenBuildTool test`, exposed by `build_workspace.bat --package`.

Example:

```bat
Arisen\Scripts\Windows\build_workspace.bat --package com.arisen.rhi.vulkan.native --config Debug
```

The canonical workspace manifest also includes a concrete testing profile:

- `RHIVulkanTesting`

## Generated outputs

Generated `.sln`, `.csproj`, native build glue, and workspace output under `.arisen/` are derived artifacts produced by `ArisenBuildTool`. Treat package metadata, workspace manifests, and source code as the source of truth instead of hand-maintaining generated files.

## Further reading

For architecture and build details, read:

- `Arisen/Docs/Architecture/ProjectManagement.md`
- `Arisen/Docs/Architecture/ArisenBuildTool.md`
- `Arisen/Docs/Architecture/ConfigurationFormats.md`
- `Arisen/Docs/Architecture/PackageLifecycle.md`
- `Arisen/Docs/Architecture/ServiceRegistry.md`
- `Arisen/Docs/Architecture/Rendering.md`
