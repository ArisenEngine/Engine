---
name: create-package
description: Scaffolds a new Arisen Engine package. Use when adding features, subsystems, or new game modules.
---

# Create Package SOP

When scaffolding a new package, follow this procedure to ensure it integrates seamlessly with the microkernel and build system.

## 1. Directory Structure Checklist
- [ ] Determine the target directory:
    - `Engine/Packages/`: Core engine systems.
    - `Local/`: User-defined components and gameplay code.
- [ ] Create the new package folder. Use lowercase (e.g., `com.arisen.physics`).

## 2. package.json Scaffold
Generate a valid `package.json` in the root of the new folder. Follow these strict rules:
- [ ] `$schema`: Use "https://arisen.dev/schemas/package-v2.json".
- [ ] `id`: Unique lowercase ID using dot notation.
- [ ] `dependencies`: Mandatory empty or populated map.
- [ ] **NO manual editing** of `subsystems`, `nativeRuntimes`, or `entry.assembly`.

### Template:
```json
{
  "$schema": "https://arisen.dev/schemas/package-v2.json",
  "id": "com.user.newpackage",
  "name": "New Package",
  "version": "1.0.0",
  "author": "Your Name",
  "dependencies": {}
}
```

## 3. C# Entry Point
- [ ] Create a `PackageEntry.cs` that implements `ArisenKernel.Packages.IPackageEntry`.
- [ ] Implement `OnLoad()` for subsystem registration.

## 4. Mandatory Manifest Registration
- [ ] Update the workspace's `manifest.json`.
- [ ] Add the package ID and relative URL.
- [ ] **CRITICAL**: If not registered, the Kernel will completely ignore the directory.

## 5. Verification
Validate that the new package compiles and is discoverable:
```powershell
./Engine/Arisen/Scripts/Windows/build_workspace.bat Debug
```
Confirm the package appears in the `Assets Browser` if integrated with the Editor.
