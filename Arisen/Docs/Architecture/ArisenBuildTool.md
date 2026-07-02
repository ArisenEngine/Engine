# Architecture Spec: IDE Solution Generation (ArisenBuildTool)

**Status**: Draft / Active  
**Module**: ArisenBuildTool

To provide World-Class Developer Experience (DX), Arisen Engine uses a "Generated Project" workflow similar to Unreal Engine (`GenerateProjectFiles.bat`). 

Human developers do not manually create or maintain `.sln`, `.csproj`, or `.vcxproj` files. Instead, `ArisenBuildTool` reads the Workspace's `manifest.json` and automatically generates a perfect IDE solution.

---

## Validation Pipeline

Before generation, `ArisenBuildTool` validates the selected workspace/profile package graph. This is available as an explicit command and is also run automatically by `generate`.

```bat
ArisenBuildTool.exe validate --workspace "Path\To\MyGame" --profile Development
```

Validation is intentionally strict. The command exits with code `0` on success and non-zero on failure. It reports:

- selected workspace and profile,
- discovered package count,
- stable resolved package order,
- duplicate package declarations,
- missing package directories,
- missing `package.json` files,
- package ID mismatches,
- invalid package types,
- malformed package entry blocks,
- dependency cycles,
- invalid service contract declarations,
- `services.requires` entries with no matching selected `services.provides` provider,
- optional service requirements as warnings when no provider is selected,
- deferred service contracts as graph-validated but late-registered runtime contracts.

A successful validation produces the same package order that generation uses for `manifest.resolved.json`. Launcher/editor package management should call this validation path rather than reimplementing graph checks. The launcher launch path invokes `validate` with the selected profile before invoking `generate` with that same profile, so CLI and launcher launches report package graph errors through the same validation implementation.

### Validation Fixture Tests

Pure package graph and metadata validation is covered by `ArisenBuildTool.Tests`:

```bat
dotnet test Arisen\External\ArisenBuildTool.Tests\ArisenBuildTool.Tests.csproj
```

The fixture tests synthesize small temporary workspaces and verify valid graph sorting, missing dependencies, dependency cycles, missing required services, duplicate package declarations, and invalid native test metadata.

### Fast Validation Gate

Run the fast local/CI gate before package architecture or build-tool changes:

```bat
Arisen\Scripts\Windows\validate_fast.bat
```

This command runs the build-tool fixture tests, kernel package lifecycle tests, and `ArisenBuildTool validate` for the canonical `Development`, `Production`, and `RHIVulkanTesting` profiles.

### Runtime Validation Gate

Run the full runtime gate before committing changes that affect boot, platform windows, generated launch metadata, package lifecycle, RHI initialization, smoke-mode execution, or profile macro behavior:

```bat
Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --frames 1
```

This command runs the fast gate first, builds generated runtime outputs for the canonical workspace, and launches bounded smoke runs for the selected runtime profiles. It is the preferred validation path for the platform/RHI/rendering roadmap because it proves the package graph under a real process instead of only validating metadata.

---

## Graph Inspection

`ArisenBuildTool graph` renders the validated package dependency graph for a workspace/profile without generating IDE files.

```bat
ArisenBuildTool.exe graph --workspace "Path\To\MyGame" --profile Development --format text
ArisenBuildTool.exe graph --workspace "Path\To\MyGame" --profile Development --format json --output ".arisen\package-graph.json"
ArisenBuildTool.exe graph --workspace "Path\To\MyGame" --profile Development --format dot --output ".arisen\package-graph.dot"
```

Supported formats:

- `text` - readable topological order, per-package dependencies, and edge list.
- `json` - machine-readable package nodes and dependency edges for launcher/editor tooling.
- `dot` - Graphviz DOT output for visual dependency diagrams.

The command uses the same validation and effective package manifest merge path as `validate` and `generate`. Invalid graphs fail before output.

---

## Launch Boundary

Interactive launch intentionally stays outside `ArisenBuildTool` for now. The build tool owns validation, graph inspection, generated project files, native payload deployment, `manifest.resolved.json`, and `launch.config.json`. Process orchestration belongs to the launcher, IDE launch profiles, scripts, or the generated thin executable host.

This keeps launch-specific concerns out of the build generator:

- debugger attachment and IDE-specific launch profiles,
- editor/game mode selection,
- graphics diagnostics such as Vulkan validation or RenderDoc setup,
- process lifetime management and log capture,
- user-selected engine installation state in the launcher.

`ArisenBuildTool` may add a thin non-interactive `run` command later for CI or scripting, but that command should only locate the generated output, start the host process with the correct working directory, forward arguments, and return the child process exit code. It must not become a second launcher.

---

## Package Test Workspace Generation

`ArisenBuildTool test --package <id>` creates an isolated `Testing` workspace profile for one package and its companion test package.

```bat
ArisenBuildTool.exe test --workspace "Path\To\MyGame" --package com.arisen.rhi.vulkan.native
```

The command expects local packages under `Local/`:

- `com.arisen.core`
- the requested package id
- the companion package id, usually `<id>.test`
- `com.arisen.testrunner`

Before generation it logs the workspace, engine root, companion test package, and full virtual manifest package list. Missing local package folders or missing `package.json` files are reported before invoking the normal generation pipeline.

`build_workspace.bat --package <id>` builds the generated `Testing` profile. Add `--run-tests` to execute the generated test host after a successful build and return the test process exit code:

```bat
Arisen\Scripts\Windows\build_workspace.bat --package com.arisen.rhi.vulkan.native --config Debug --run-tests
```

Native test packages declare their registration libraries through `nativeTests` in `package.json`. The generated `manifest.resolved.json` carries this metadata to `com.arisen.testrunner`, which loads each declared native library from `.arisen/bin/Testing/{configuration}/` and calls the declared registration export.

---

## Package Archive Publishing

`ArisenBuildTool pack` creates a distributable package archive from a selected workspace/profile package:

```bat
ArisenBuildTool.exe pack --workspace "Path\To\MyGame" --profile Development --package com.user.inventory --output ".arisen\Packages"
```

The command validates the selected workspace/profile graph first, then writes `{packageId}-{version}.zip`. The zip root contains the package contents directly, including `package.json`, which matches the launcher cache restore format for remote package URLs.

By default the command writes to `.arisen\Packages\` and refuses to overwrite an existing archive. Pass `--overwrite` to replace the existing zip. Transient build/generated directories are excluded from package archives:

- `.arisen/`
- `.git/`
- `bin/`
- `obj/`

---

## Package Registry Index Publishing

`ArisenBuildTool registry-index` creates a deterministic `registry.json` from a directory of package archives:

```bat
ArisenBuildTool.exe registry-index --source ".arisen\Packages" --base-url "https://packages.example.com/arisen" --output ".arisen\Packages\registry.json"
```

The command scans top-level `*.zip` files in `--source`. Each archive must use the `pack` archive format, with `package.json` at the zip root. The generated index is sorted by package id and version, and intentionally excludes generation timestamps so the file is stable in source control and CDN publishing workflows.

Each package entry includes:

- package id and version,
- display metadata copied from `package.json`,
- archive URL derived from `--base-url` plus the zip filename,
- archive SHA-256 hash,
- archive byte size.

If `--output` is omitted, the command writes `registry.json` inside the source directory. Existing output files are not overwritten unless `--overwrite` is passed.

---

## The Generation Pipeline

When a user clicks "Generate IDE Files" in the Launcher, it invokes:
`ArisenBuildTool.exe generate --workspace "Path/To/MyGame" --profile Development`

The tool executes four strict phases:

### Phase 1: Package Validation & Workspace Resolution
1. The tool parses `manifest.json` at the workspace root.
2. It gathers base packages plus packages from the selected profile.
3. It recursively resolves dependencies from each package's `package.json` to build the full Directed Acyclic Graph (DAG) for that profile.
4. It fails before generation if any required package is missing, malformed, duplicated with conflicting metadata, participates in a dependency cycle, or requires a service contract that no selected package provides.
5. It sorts packages in a stable topological order.

### Phase 2: IDE Project Generation (The `.arisen` hidden folder)
We DO NOT pollute the user's `Local/` or package source folders with IDE-specific configuration files.
Instead, the tool creates a hidden folder inside the workspace: `MyGame/.arisen/Projects/`.

For every discovered package:
- **Managed Packages (C#)**: It generates `com.user.mygame.csproj` inside the hidden folder. It defines `<Compile Include="../../Local/com.user.mygame/**/*.cs" />` to link the source code. It automatically resolves exact `<ProjectReference>` links based on the `package.json` dependencies.
- **Native Packages (C++)**: It generates `CMakeLists.txt` or `.vcxproj` pointing to the native source code.

### Phase 3: Developer UX Injection (Generated Package Metadata)
As defined in [ConfigurationFormats.md](ConfigurationFormats.md), users should not manually configure code-derived metadata such as `entry`, generated `services.provides`, generated `subsystems`, or `nativeRuntimes` in their human-owned `package.json`.

During Phase 2, `ArisenBuildTool` injects a post-build metadata step into the generated `.csproj`.
- Every time the user clicks "Build" in Visual Studio/Rider, the injected step scans the compiled assembly for `IPackageEntry`, `[EngineSubsystem]`, and `[EngineService]` metadata.
- The step writes `package.generated.json` next to `package.json` instead of overwriting `package.json`.
- Validation, generation, and runtime fallback merge `package.json` + `package.generated.json` into an effective package manifest.

### Phase 4: Resolved Manifest & Entry Point Generation
The tool generates a separate solution file for each **Profile** defined in the workspace:
- **Solution Naming**: `{ProjectName}_{Profile}.sln` (e.g., `MyGame_Development.sln`).
- **Storage**: Solutions are stored in `.arisen/Projects/{Profile}/`.
- **Profile Macros**: Each solution automatically defines the preprocessor macro `ARISEN_PROFILE_{PROFILE}` for both C++ and C# projects.
- **Editor Macro**: Editor-capable generated projects define `ARISEN_ENGINE_EDITOR`. Use this compile-time macro for editor/runtime behavior policy such as UI host ownership, standalone window creation, and RHI surface boot. Do not use runtime flags such as `EngineConfig.IsEditor` for these ownership decisions.
- **Unified Entry Point**: The tool generates a thin `Program.cs` stub in the workspace project. This stub calls `ArisenKernel.Lifecycle.EngineBootstrapper.Run(args)`, making the workspace a manageable .NET executable.
- **Resolved Manifest**: The tool writes `manifest.resolved.json` into each `.arisen/bin/{profile}/{configuration}/` output directory. It includes sorted packages plus debug metadata such as type, entry, dependency, and service declarations. Runtime boot treats this resolved manifest as authoritative when present so package mount order matches build-time validation.
- **Launch Config**: The tool writes `launch.config.json` beside the executable so the bootstrapper can recover the workspace/profile without relying only on path deduction.
- **Organization**: Projects are organized into logical Solution Folders: `Engine Packages`, `Local Packages`, and `Native Dependencies`.

---

## Configuration Mapping

To handle the differences between managed (C#) and native (C++) build systems, `ArisenBuildTool` performs automatic configuration mapping within the solution:

### 1. Isolated Binary Outputs
To ensure perfect portability and SDK readiness, all managed and native artifacts for a profile are co-located in a unified, isolated directory:
- **Path**: `.arisen/bin/{profile}/{configuration}/`
- **Co-Location**: Every project in the solution (including the entry point and all packages) redirect their `OutputPath` to this folder.
- **Dependency Deployment**: Managed projects use `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` to ensure transitive NuGet dependencies (like Avalonia) are correctly deployed to this folder even for library packages.

### 2. Configuration Mapping
Managed projects use standard `Debug` and `Release` configurations. Native projects (CMake) are generated to match these names exactly:
- **1:1 Mapping**: Solutions map `Debug`➔`Debug` and `Release`➔`Release` directly.
- **Macro Injection**: The `ARISEN_PROFILE_{PROFILE}` macro is injected into all projects to allow conditional compilation based on the active engine profile (e.g., enabling Editor code only in `Development`).

This ensures a seamless development experience where IDE configuration names match across all languages, while maintaining the engine's strict profile-specific macro architecture.

---

## Why this Architecture is Superior

1. **Zero Git Conflicts**: Because `.sln` and `.csproj` are transient and generated locally, multiple developers adding scripts will never have XML merge conflicts. You only commit `package.json` and `manifest.json`.
2. **Instant Engine Debugging**: Because the `manifest.json` resolves engine dependencies locally, the generated Solution includes engine source C# projects automatically. The user can seamlessly step-in and debug engine Kernel code interchangeably with their game code.
3. **Automated Configurations**: If a user switches the target platform from Windows to Linux in the Launcher, ArisenBuildTool just re-generates the `CMakeLists` and `.csproj` flags instantly.
