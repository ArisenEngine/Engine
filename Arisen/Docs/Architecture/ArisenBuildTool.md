# Architecture Spec: IDE Solution Generation (ArisenBuildTool)

**Status**: Draft / Active  
**Module**: ArisenBuildTool

To provide World-Class Developer Experience (DX), Arisen Engine uses a "Generated Project" workflow similar to Unreal Engine (`GenerateProjectFiles.bat`). 

Human developers do not manually create or maintain `.sln`, `.csproj`, or `.vcxproj` files. Instead, `ArisenBuildTool` reads the Workspace's `manifest.json` and automatically generates a perfect IDE solution.

---

## The Generation Pipeline

When a user clicks "Generate IDE Files" in the Launcher, it invokes:
`ArisenBuildTool.exe generate --workspace "Path/To/MyGame"`

The tool executes four strict phases:

### Phase 1: Package Discovery & Workspace Resolution
1. The tool parses `manifest.json` at the workspace root.
2. It gathers **ALL** packages listed (Base packages + packages from every Profile including Development/Server).
3. It recursively resolves dependencies from each package's `package.json` to build the full Directed Acyclic Graph (DAG) of the workspace.

### Phase 2: IDE Project Generation (The `.arisen` hidden folder)
We DO NOT pollute the user's `Local/` or package source folders with IDE-specific configuration files.
Instead, the tool creates a hidden folder inside the workspace: `MyGame/.arisen/Projects/`.

For every discovered package:
- **Managed Packages (C#)**: It generates `com.user.mygame.csproj` inside the hidden folder. It defines `<Compile Include="../../Local/com.user.mygame/**/*.cs" />` to link the source code. It automatically resolves exact `<ProjectReference>` links based on the `package.json` dependencies.
- **Native Packages (C++)**: It generates `CMakeLists.txt` or `.vcxproj` pointing to the native source code.

### Phase 3: Developer UX Injection (Source Generators)
As defined in [ConfigurationFormats.md](ConfigurationFormats.md), users should not manually configure `subsystems` or `nativeRuntimes` in their `package.json`. 

During Phase 2, `ArisenBuildTool` automatically injects the **Arisen Roslyn Source Generator** into the generated `.csproj`. 
- Every time the user clicks "Build" in Visual Studio/Rider, the injected analyzer scans the code for `[EngineSubsystem]` attributes and instantly overwrites the `package.json` with the compiled metadata!

### Phase 4: Solution Generation
Finally, the tool generates a master `MyGame.sln` file at the root of the workspace.
- This solution references the generated `.csproj` files.
- It organizes them into logical Solution Folders (e.g., `Engine Packages`, `Local Packages`, `Native`).

---

## Why this Architecture is Superior

1. **Zero Git Conflicts**: Because `.sln` and `.csproj` are transient and generated locally, multiple developers adding scripts will never have XML merge conflicts. You only commit `package.json` and `manifest.json`.
2. **Instant Engine Debugging**: Because the `manifest.json` resolves engine dependencies locally, the generated Solution includes engine source C# projects automatically. The user can seamlessly step-in and debug engine Kernel code interchangeably with their game code.
3. **Automated Configurations**: If a user switches the target platform from Windows to Linux in the Launcher, ArisenBuildTool just re-generates the `CMakeLists` and `.csproj` flags instantly.
