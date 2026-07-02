---
name: create-package
description: Scaffolds a new Arisen Engine package. Use when adding features, subsystems, or new game modules.
---

# Create Package SOP

When scaffolding a new package, follow this procedure so it integrates cleanly with the package graph and generated workspace.

## 1. Choose the target workspace and directory
- [ ] Identify the workspace that should own the package.
- [ ] Place the package under that workspace's package roots, typically `Local/` for actively developed engine, editor, or game packages.
- [ ] Use a lowercase dot-separated package ID such as `com.arisen.physics`.

For the canonical development workspace in this repo, packages live under paths such as:
- `Arisen/Development/PackageGame/Local/com.arisen.*`

## 2. Scaffold `package.json`
Create a valid `package.json` in the package root.

Rules:
- [ ] `$schema`: use `https://arisen.dev/schemas/package-v2.json`
- [ ] `id`: unique lowercase ID using dot notation
- [ ] `layer`: choose `foundation`, `domain`, `driver`, `tooling`, `user`, or `test`
- [ ] `dependencies`: include an explicit empty or populated object
- [ ] Use conservative JSONC-compatible authoring only: comments and trailing commas are okay, but keep quoted keys and normal JSON values
- [ ] Do not hand-maintain derived metadata such as `subsystems`, `nativeRuntimes`, or `entry.assembly` unless the task specifically requires it

Template:
```json
{
  "$schema": "https://arisen.dev/schemas/package-v2.json",
  "id": "com.user.newpackage",
  "name": "New Package",
  "version": "1.0.0",
  "layer": "user",
  "author": "Your Name",
  "dependencies": {}
}
```

## 3. Add the package entry point if needed
- [ ] Create a `PackageEntry.cs` that implements `ArisenKernel.Packages.IPackageEntry` when the package needs runtime registration.
- [ ] Use `OnLoad(IServiceRegistry services)` for subsystem or service registration.

## 4. Register the package in the workspace manifest
- [ ] Update the owning workspace `manifest.json`.
- [ ] Add the package ID, URL, and version.
- [ ] If the package is profile-specific, add it under the correct profile block.
- [ ] If the package is not present in the workspace manifest, the kernel and generated workspace will ignore it.
- [ ] If this package is a composition/root package choosing a concrete provider, declare the provider package in `dependencies` or the workspace manifest so it cannot be culled from the selected graph.
- [ ] If this package is reusable domain code, prefer `services.requires` for contracts and avoid depending on concrete provider packages such as a specific RHI backend.

For the canonical workspace in this repo, the manifest is:
- `Arisen/Development/PackageGame/manifest.json`

## 5. Verification
Validate that the package is discoverable and compiles through the generated workspace.

Examples:
```powershell
Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Development
Arisen\Scripts\Windows\build_workspace.bat --manifest Arisen\Development\PackageGame\manifest.json --config Debug --profile Development
```

For packages that affect boot, platform windows, RHI startup, profile macros, or runtime smoke behavior, run:

```powershell
Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --frames 1
```

If the package includes editor-facing functionality, verify it appears through the generated Development workspace after a successful build.
