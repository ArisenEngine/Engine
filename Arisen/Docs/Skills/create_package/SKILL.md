---
name: Create Arisen Package
description: Scaffolds a new Arisen Engine package following the strict Workspace and Config architectures.
---

# Creating an Arisen Engine Package
If the user asks you to create a new package for the engine or a game, you MUST follow these absolute rules derived from `Docs/Architecture/ConfigurationFormats.md` and `ProjectManagement.md`.

## 1. Directory Structure
Create the new package folder inside the `Local/` workspace directory (for user code) or `Engine/Packages/` (for core engine code). DO NOT generate `.sln` or `.csproj` files directly inside the package folder.

## 2. package.json Scaffold
Generate a `package.json` file inside the root of the new package folder.
**CRITICAL:** Only populate `id`, `name`, `version`, `author`, and `dependencies`. 
**DO NOT** manually generate `subsystems`, `nativeRuntimes`, or `entry.assembly`. These are auto-populated by the `ArisenBuildTool` compiler.

```json
{
  "$schema": "https://arisen.dev/schemas/package-v2.json",
  "id": "com.user.newpackage",
  "name": "New Package",
  "version": "1.0.0",
  "dependencies": {}
}
```

## 3. C# Entry Point
Generate a C# file (e.g., `PackageEntry.cs`) that explicitly implements `ArisenKernel.Packages.IPackageEntry`. This gives the Kernel a hook to execute code on boot.

## 4. Manifest Updates
You MUST add the new package's `id` and `Url` to the workspace's `manifest.json`. If it is not listed in the `manifest.json` (either in base `Packages` or a specific `Profile`), the Kernel will completely ignore the folder.
