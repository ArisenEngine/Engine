# Architecture Spec: Engine Bootstrapping & Binary Resolution

**Status**: Active  
**Module**: `ArisenKernel.Lifecycle`

The **Engine Bootstrapper** (formerly `ArisenHost`) is the unified, bare-metal entry logic integrated directly into the `ArisenKernel.dll`. It is responsible for discovering the Workspace, resolving the active profile package list, and handing execution over to the Engine Kernel. Runtime package mounting itself is owned by `PackageSubsystem`.

---

## 1. Unified Entry Point (Workspace Stub)

To provide a native experience, every Arisen workspace contains a thin **Entry Point Project** (e.g., `MyGame.csproj`). This project is a standard .NET 9.0 Executable that serves as a wrapper:

```csharp
public class Program {
    public static void Main(string[] args) => ArisenKernel.Lifecycle.EngineBootstrapper.Run(args);
}
```

This architecture ensures that:
1. **Manageability**: The game project is a real, debuggable process in the IDE.
2. **Zero Overhead**: All complex bootstrapping logic is offloaded to the Kernel library.
3. **Isolation**: The executable is automatically co-located with all dependencies in `.arisen/bin/{profile}/{configuration}/`.

---

## 2. Workspace & Profile Discovery

The bootstrapper resolves the environment using standard arguments:

```bash
MyGame.exe --workspace "D:/MyGame" --profile "Development"
```

1. **Deduction**: If `--workspace` is missing, the bootstrapper automatically deduces the workspace root based on the isolated binary folder structure (moving 4 levels up from `bin/`).
2. **Launch Config**: If `launch.config.json` exists beside the executable, it is used to recover profile/workspace information before path deduction.
3. **Manifest Resolution**: It parses the workspace `manifest.json` to identify base packages and active profile packages.
4. **Resolved Manifest Authority**: If `manifest.resolved.json` exists beside the executable, the bootstrapper treats it as the authoritative package list and topological order. Invalid resolved manifests are fatal by default; raw `manifest.json` fallback is allowed only when `--allow-manifest-fallback` is passed.
5. **Kernel Hand-off**: It passes the ordered package URL list to `EngineKernel.Initialize()`. The kernel ensures `PackageSubsystem` exists and delegates actual package mounting to it.

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

Once all packages are mounted by `PackageSubsystem`, the bootstrapper attempts to find a "Master Controller" to yield the main thread to:

1. **IApplicationHost**: If a package (like `com.arisen.editor`) has registered an `IApplicationHost` service, the bootstrapper yields the main thread to it (launching the Editor UI).
2. **Bare-Metal Kernel**: If no application host is detected, the bootstrapper engages the default engine tick loop via `EngineKernel.Instance.Run()`.

Smoke validation is a bounded variant of the same boot path:

```bash
MyGame.exe --workspace "D:/MyGame" --profile "Development" --smoke-mode scene --frames 1
```

Supported smoke modes are `boot`, `scene`, and `hot-reload`. `boot` preserves the legacy one-frame bounded loop. `scene` runs at least two frames so packages that defer scene setup to first `OnFrameEnd` still render one prepared scene frame. `hot-reload` currently runs a multi-frame scene stability window and logs that true file-change recook/reload smoke still needs a runtime-owned asset-change harness.
