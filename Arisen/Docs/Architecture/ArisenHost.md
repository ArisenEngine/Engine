# Architecture Spec: Engine Bootstrapping & Binary Resolution

**Status**: Active  
**Module**: `ArisenKernel.Lifecycle`

The **Engine Bootstrapper** (formerly `ArisenHost`) is the unified, bare-metal entry logic integrated directly into the `ArisenKernel.dll`. It is responsible for discovering the Workspace, resolving all package binaries, and handing execution over to the Engine Kernel.

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

1. **Deducation**: If `--workspace` is missing, the bootstrapper automatically deduces the workspace root based on the isolated binary folder structure (moving 4 levels up from `bin/`).
2. **Manifest Resolution**: It parses the workspace `manifest.json` to identify the required packages and active profile.
3. **Topological Mounting**: It iterates through all active packages, loading their managed assemblies into the `AssemblyLoadContext` and mapping their native C++ payloads into memory.

---

## 3. Co-Located Binary Resolution

Arisen uses a **Strict Co-Location Strategy**. All compiled artifacts (Engine Kernel, Package DLLs, Native RHI/HAL binaries, and Transitive NuGet dependencies) are placed in a single flat directory:
`MyGame/.arisen/bin/{profile}/{configuration}/`.

When the bootstrapper runs:
1. It looks for assemblies first in the global `bin/` directory.
2. If an assembly is missing (e.g., in a non-built source package), it falls back to the package's local `Managed/` folder.
3. Native binaries are mapped globally, ensuring `[DllImport]` works flawlessly across package boundaries.

---

## 4. Boot Hand-off

Once all packages are mounted, the bootstrapper attempts to find a "Master Controller" to yield the main thread to:

1. **IApplicationHost**: If a package (like `com.arisen.editor`) has registered an `IApplicationHost` service, the bootstrapper yields the main thread to it (launching the Editor UI).
2. **Bare-Metal Kernel**: If no application host is detected, the bootstrapper engages the default engine tick loop via `EngineKernel.Instance.Run()`.
