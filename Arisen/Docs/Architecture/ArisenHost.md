# Architecture Spec: Engine Bootstrapping & Binary Resolution

**Status**: Active  
**Module**: `ArisenKernel.Lifecycle`

The **Engine Bootstrapper** (formerly `ArisenHost`) is the unified, bare-metal entry logic integrated directly into the `ArisenKernel.dll`. It is responsible for discovering the Workspace, resolving the active profile package list, and handing execution over to the Engine Kernel. Runtime package mounting itself is owned by `PackageSubsystem`.

---

## 1. Unified Entry Point (Workspace Stub)

To provide a native experience, every Arisen workspace contains a thin **Entry Point Project** (e.g., `MyGame.csproj`). This project is a standard .NET 9.0 Executable that serves as a wrapper:

```csharp
public class Program {
    public static void Main(string[] args) {
        if (RuntimeAssetCookHost.IsCookCommand(args)) {
            Environment.ExitCode = RuntimeAssetCookHost.Run(args);
            return;
        }

        ArisenKernel.Lifecycle.EngineBootstrapper.Run(args);
    }
}
```

This architecture ensures that:
1. **Manageability**: The game project is a real, debuggable process in the IDE.
2. **Zero Overhead**: All complex bootstrapping logic is offloaded to the Kernel library.
3. **Isolation**: The executable is automatically co-located with all dependencies in `.arisen/bin/{profile}/{configuration}/`.

When `com.arisen.core` participates in the generated graph, the entry project compile-references that package so it can dispatch package-aware build commands. Other package project references remain runtime-loaded and use `ReferenceOutputAssembly="false"`; normal game/editor composition still comes from the selected package graph rather than static entry-project references.

### Build-Stage Cook Dispatch

The generated host recognizes:

```bash
MyGame.exe --arisen-cook-runtime-assets --workspace "D:/MyGame" --profile Production --configuration Release --runtime-identifier win-x64 --output-root "D:/MyGame/.arisen/bin/Production/Release"
```

`RuntimeAssetCookHost` reuses `EngineBootstrapper.ResolvePackageGraph()` and prefers the generated `.arisen/Projects/{Profile}/manifest.source.resolved.json`. This source-resolved graph is build-stage-only, so repeat builds retain package source roots after the launch output has been rewritten for deployment. The host then calls `EngineKernel.MountPackageGraph()` instead of full initialization. Package entries can register source-format-owned cooker providers, but subsystem phases never begin, so the command does not create windows, initialize the RHI, run frames, or activate a scene.

The cook host sets `EngineConfig.ExecutionMode` to `RuntimeAssetCook`, which gives `IAssetDatabase` the corresponding source-access mode. This is an execution-purpose boundary, not an Editor flag: package-owned cookers may read indexed workspace source, while ordinary runtime execution selects cooked payloads.

The command writes `.arisen/Intermediate/Cook/{Profile}/{Configuration}/runtime-assets.json` and returns a process exit code. When `--output-root` is supplied, it also stages and verifies the complete catalog closure, replaces the owned `<output-root>/Content/` tree, and publishes `<output-root>/runtime-assets.json`. Production generated projects invoke this path automatically after `Build`, then run `ArisenBuildTool deploy-runtime-metadata` to publish output-owned package/project metadata. Omitting `--output-root` retains intermediate-only behavior for explicit tooling.

---

## 2. Workspace & Profile Discovery

The bootstrapper resolves the environment using standard arguments:

```bash
MyGame.exe --workspace "D:/MyGame" --profile "Development"
```

Ordinary Development and test launches index workspace assets but select cooked scene/rendering payloads. Source selection is a deliberate diagnostic action:

```bash
MyGame.exe --workspace "D:/MyGame" --profile Development --diagnostic-source-assets
```

The bootstrapper maps this flag to `EngineConfig.EnableSourceAssetDiagnostics`. `com.arisen.resources` then selects `AssetSourceAccessMode.Diagnostic`; without the flag it selects `Disabled`. Editor source access remains compile-owned through `ARISEN_ENGINE_EDITOR`, while Production and deployed launches reject the flag before package initialization.

1. **Deduction**: For a workspace launch, if `--workspace` is missing, the bootstrapper deduces the workspace root based on the isolated binary folder structure.
2. **Launch Config**: If `launch.config.json` exists beside the executable, it is used before path deduction. `Mode: Deployed` roots metadata beside the executable and rejects `--workspace` or a conflicting profile override.
3. **Manifest Resolution**: It parses the workspace `manifest.json` to identify base packages and active profile packages.
4. **Resolved Manifest Authority**: If `manifest.resolved.json` exists beside the executable, the bootstrapper treats it as the authoritative package list and topological order. Invalid resolved manifests are fatal by default; raw `manifest.json` fallback is allowed only when `--allow-manifest-fallback` is passed.
5. **Kernel Hand-off**: It passes the ordered package URL list to `EngineKernel.MountPackageGraph()`. After package entries register their services, the bootstrapper either continues through `EngineKernel.Initialize()` or hands a package-only application host the main thread.

For an ordinary `ARISEN_PROFILE_PRODUCTION` launch, `com.arisen.resources` requires `<app-base>/runtime-assets.json` and `<app-base>/Content/`. Catalog profile, paths, sizes, and hashes are validated before any asset lookup is published. The runtime database contains no source records, uses `Disabled` source access, and rejects cooking, invalidation, or source refresh; missing or incompatible cooked content is fatal instead of falling back to workspace `Assets` or `.arisen/Cache`.

A finalized Production output also contains a sanitized `manifest.json` plus effective descriptors under `<app-base>/Packages/<package-id>/package.json`. Both that manifest and `manifest.resolved.json` use only `file://Packages/...` URLs. Package assemblies and native payloads remain co-located in `<app-base>`, so moving or copying the complete output preserves package loading without copying package source directories.

---

## 3. Co-Located Binary Resolution

Arisen uses a **Strict Co-Location Strategy**. All compiled artifacts (Engine Kernel, Package DLLs, Native RHI/HAL binaries, and Transitive NuGet dependencies) are placed in a single flat directory:
`MyGame/.arisen/bin/{profile}/{configuration}/`.

When `PackageSubsystem` mounts a package:
1. It looks for entry assemblies first in the global `bin/` directory.
2. If an assembly is missing from `bin/`, it falls back to the package root and then the package's local `Managed/` folder.
3. Entry classes must implement `IPackageEntry` when declared.
4. Native binaries are mapped globally by deployment/copy rules, ensuring `[DllImport]` works across package boundaries.

---

## 4. Boot Hand-off

Once all packages are mounted by `PackageSubsystem`, the bootstrapper resolves the process owner:

1. **Package-only application host**: An `IApplicationHost` may return `false` from `RequiresEngineInitialization`. The bootstrapper then yields immediately after package mounting, without entering subsystem phases. `com.arisen.testrunner` uses this mode so native tests own their RHI instance, render window, and Win32 message loop; the game platform, scene, and rendering subsystems never start alongside them.
2. **Full-engine application host**: The default contract value is `true`. The bootstrapper initializes all subsystem phases before yielding to hosts such as `com.arisen.editor`.
3. **Bare-metal kernel**: If no application host is detected, the bootstrapper initializes the engine and engages the default tick loop through `EngineKernel.Instance.Run()`.

Smoke mode always takes the full-engine path and exits through its bounded kernel loop, even when the selected package graph also contains a package-only host. This preserves `RHIVulkanTesting` runtime smoke coverage while keeping an ordinary interactive test launch isolated.

Smoke validation is a bounded variant of the same boot path:

```bash
MyGame.exe --workspace "D:/MyGame" --profile "Development" --smoke-mode scene --frames 1
```

Kernel-owned smoke modes are `boot`, `scene`, and `hot-reload`. `boot` preserves the legacy one-frame bounded loop. `scene` runs at least two frames so packages that defer scene setup to first `OnFrameEnd` still render one prepared scene frame. `hot-reload` currently runs a multi-frame scene stability window and logs that true file-change recook/reload smoke still needs a runtime-owned asset-change harness.

Selected packages can register additional bounded modes through `IRuntimeSmokeScenarioRegistry`. The current `com.arisen.resources` provider owns `world-streaming`, and `com.arisen.terrain` owns `terrain-streaming`. The kernel supplies the scenario context, frame callbacks, wall-clock deadline, optional named visual capture, guaranteed engine shutdown, and one post-shutdown inspection callback; it does not depend on world or terrain implementations.

`--visual-summary-output <path>` implies scene visual capture and overrides the default workspace-relative artifact path. Runtime validation uses it so a deployed player can remain rooted to its copied output while CI artifacts are written to the canonical validation log directory.
