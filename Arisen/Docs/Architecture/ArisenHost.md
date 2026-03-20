# Architecture Spec: ArisenHost Bootstrapping & Binary Resolution

**Status**: Draft / Active  
**Module**: `ArisenHost`

The `ArisenHost` is the universal, bare-metal executable that boots the Arisen Engine. It contains no game logic and no Editor UI. Its sole responsibility is to find the Workspace, resolve binaries (both C# and native C++), and hand execution over to the Kernel.

---

## 1. How ArisenHost Finds the Project

The Host must be launched with command-line arguments, typically invoked by the `ArisenLauncher` or a terminal:

```bash
ArisenHost.exe --workspace "D:/MyGame" --profile "Development"
```

1. **Finding the Manifest**: The Host looks at the `--workspace` path and expects to find `D:/MyGame/manifest.json`.
2. **Finding the Packages**: The Host parses `manifest.json`. For every package listed:
   - If the `Url` is local (e.g., `file:///Local/com.user.game`), it resolves `D:/MyGame/Local/com.user.game/package.json`.
   - If the `Url` is empty (default engine package), it looks in the global Engine installation directory (e.g., `Engine/Packages/`).

---

## 2. Uniform Build Outputs & Multiple DLLs

When `ArisenBuildTool` compiles the solution, it MUST send all `.dll` outputs to a unified binary folder. 

**The Uniform Output Rule**: 
All compiled C# code for a workspace is redirected to `MyGame/.arisen/bin/`.
When `ArisenHost` boots, it sets its `AssemblyLoadContext` to look inside `.arisen/bin/`.

**Handling Multiple DLLs in One Package**:
If a package compiles into multiple C# DLLs (e.g., `Core.Math.dll` and `Core.Physics.dll`), it is completely fine. 
The `package.json` only requires the `entry.assembly` field if the package has an `IPackageEntry` class that the Kernel needs to forcefully invoke. The other DLLs will automatically be loaded by the .NET runtime the exact moment another script tries to call a function inside them.

---

## 3. Hybrid Packages: C# and C++ Existing Together

A single package **can** contain both managed C# code and native C++ code!

If `com.arisen.rhi.vulkan` has both:
1. The C++ code compiles into `libvulkan_backend.dll` and is placed in `runtimes/win-x64/native/libvulkan_backend.dll`.
2. The C# code compiles into `Arisen.RHI.Vulkan.dll` and is placed in `lib/net9.0/`.
3. In `package.json`, BOTH are listed:
```json
"entry": { "assembly": "Arisen.RHI.Vulkan.dll" },
"nativeRuntimes": { "win-x64": ["libvulkan_backend.dll"] }
```

When the Engine boots, it loads the native DLL into process memory *before* it executes the C# assembly, ensuring that `[DllImport]` or `LibraryImport` calls work instantly.

---

## 4. Cross-Package Native Dependencies
*Scenario: Package A (Pure C#) depends on Package B (Pure C++).*

Code packages communicate across boundaries flawlessly without manual intervention:
1. `Package A` lists `Package B` in its `dependencies`.
2. The Engine's Topological Sorter (see `PackageLifecycle.md`) analyzes the graph and processes `Package B` first.
3. The Engine reads `Package B`'s `package.json`, sees `"nativeRuntimes"`, and uses the OS `NativeLibrary.Load()` to load the C++ binaries into RAM globally.
4. The Engine then processes `Package A`. When `Package A` executes its first PInvoke call to the C++ code, the OS automatically resolves it because `Package B` was already pushed into memory during step 3.
