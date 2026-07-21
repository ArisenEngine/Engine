# Architecture Spec: Configuration Formats

**Status**: Draft / Active  
**Module**: Entire Ecosystem (Launcher, Kernel, Build Tool)

This document defines the structural schemas for the core configuration files driving the Arisen Engine's package-centric architecture.

## Manifest Syntax Policy

Human-authored Arisen manifests use a conservative JSONC-compatible syntax:

- `manifest.json`
- `package.json`

Tooling also accepts the same syntax for `package.generated.json` during local development/transition, although generated writers should normally emit strict JSON.

Allowed authoring conveniences:

- `// line comments`
- `/* block comments */`
- trailing commas in objects and arrays

Not allowed:

- unquoted object keys
- single-quoted strings
- hexadecimal numbers
- other full JSON5-only syntax

Generated distribution files remain strict JSON and should not contain comments:

- `manifest.resolved.json`
- deployed `manifest.json` and `launch.config.json`
- deployed `Packages/<package-id>/package.json` descriptors
- `runtime-assets.json`
- `.arisen/package-lock.json`
- registry `registry.json`

The file extension remains `.json` for compatibility with existing tools and package conventions. Tooling reads authored files with comments/trailing commas enabled, but writers should emit strict JSON unless the file is an initial human-facing template.

Use comments in human-authored files to explain intent, ownership, or generator boundaries. Do not rely on comments as data. Generated files should prefer stable, strict JSON so package archives, registry indexes, and resolved manifests remain portable across standard JSON tooling.

## Canonical Casing

Arisen chooses canonical JSON casing by the surface that owns each file:

- Workspace/project files (`manifest.json`, `.arisenproj`) use **PascalCase** keys because they are edited mainly by launcher/project tooling and mirror the managed project model.
- Package files (`package.json`, `package.generated.json`) use **camelCase** keys because they are package metadata, registry payloads, and generated package descriptors.
- Other machine-generated distribution formats use the casing fixed by their versioned schema; `runtime-assets.json` uses **camelCase** keys.

Tooling may deserialize case-insensitively for transition compatibility, but generated files and documentation must use the canonical casing above. Validation treats the field names listed in this document as the source of truth.

---

## 1. Project Manifest (`manifest.json`)

**Location**: Adjacent to `.arisenproj` in the root of user projects.  
**Purpose**: Defines the packages required to boot the project, explicitly isolating different runtime modes via Profiles.

### Schema Spec
```json
{
  "$schema": "https://arisen.dev/schemas/manifest-v1.json",
  "Name": "MyGame",
  "EngineVersion": "Current",

  // The package-owned scene activated before frame zero.
  "StartupScene": {
    "Guid": "11111111-2222-3333-4444-555555555555",
    "PackageId": "com.user.mygame"
  },

  // Cooked world/cell descriptor selected for streaming-capable composition.
  "StartupWorld": {
    "Guid": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
    "PackageId": "com.user.mygame"
  },

  // Package-owned render-pipeline quality settings selected at startup.
  "RenderPipeline": {
    "Guid": "22222222-3333-4444-5555-666666666666",
    "PackageId": "com.arisen.generic-renderpipeline"
  },

  "Packages": [
    {
      "Id": "com.user.mygame",
      "Url": "file:///Local/com.user.mygame/",
      "Version": "1.0.0"
    }
  ],

  // Profiles are optional named build/runtime policies.
  "Profiles": {
    "Editor": {
      "IsEditor": true,
      "EnableProfiler": true,
      "Packages": [
        {
          "Id": "com.arisen.editor.default",
          "Url": "",
          "Version": "1.0.0"
        }
      ]
    },
    "Development": {
      "IsEditor": false,
      "EnableProfiler": true,
      "Packages": []
    },
    "Production": {
      "IsEditor": false,
      "EnableProfiler": false,
      "Packages": []
    }
  }
}
```

### Detailed Field Specification

| Field | Type | Required? | Description |
|---|---|---|---|
| `$schema` | `string` | Optional | URL to the JSON schema for IDE autocomplete and validation. |
| `Name` | `string` | **Yes** | The user-friendly name of the project. |
| `EngineVersion` | `string` | **Yes** | The specific version of Arisen Engine required (e.g., `"1.2.0"` or `"Current"`). |
| `StartupScene` | `object` | Optional | Package-owned `Scene` asset activated by the root composition package before frame zero. Projects intended to run a scene should provide it. |
| `StartupScene.Guid` | `Guid` | Required with `StartupScene` | Stable `.arisenscene.meta` identity. Empty GUIDs fail workspace validation. |
| `StartupScene.PackageId` | `string` | Required with `StartupScene` | Package that owns the selected scene asset. Empty package IDs fail workspace validation. |
| `StartupWorld` | `object` | Optional | Package-owned `World` descriptor used as the `startupWorld` cooked root and runtime activation root. Its persistent scene activates during `PostInit`; cells require a streaming source or explicit pin. |
| `StartupWorld.Guid` | `Guid` | Required with `StartupWorld` | Stable `.arisenworld.meta` identity. Empty GUIDs fail workspace validation. |
| `StartupWorld.PackageId` | `string` | Required with `StartupWorld` | Package that owns the selected world. It must be present in the base `Packages` list so Production closure cannot depend on a profile-only package. |
| `RenderPipeline` | `object` | Optional globally; required by workspaces that load `RenderSubsystem` | Package-owned `RenderPipelineSettings` asset used to activate the selected pipeline provider. |
| `RenderPipeline.Guid` | `Guid` | Required with `RenderPipeline` | Stable `.arisrenderpipeline.meta` identity. Empty GUIDs fail workspace validation. |
| `RenderPipeline.PackageId` | `string` | Required with `RenderPipeline` | Package that owns the selected settings asset. It must be present in the base `Packages` list and may be the game package rather than the provider package. |
| `Packages` | `array` | **Yes** | The base list of packages loaded regardless of what Profile is active. |
| `Packages[].Id` | `string` | **Yes** | The unique identifier of the package to load. |
| `Packages[].Url` | `string` | Optional | A path to resolve the package locally (`file:///...`), a direct remote package archive (`https://.../package.zip`), or a remote registry index (`https://.../registry.json`). If empty, resolves from local/engine package search paths. |
| `Packages[].Version` | `string` | **Yes** | Exact package version or supported registry semantic range (e.g., `"1.0.0"`, `">=1.2.0 <2.0.0"`, `"^1.2.0"`, `"~1.2.0"`, `"*"`). Local file packages should still declare an exact version so duplicate entries and package locks remain deterministic. |
| `Profiles` | `object` | Optional | A dictionary of named profiles mapped to profile metadata objects. |
| `Profiles[Key].IsEditor` | `bool` | Optional | If `true`, generated projects define `ARISEN_ENGINE_EDITOR`. Use this for editor/window/RHI ownership policy, not runtime flags. Defaults to `false`. |
| `Profiles[Key].EnableProfiler` | `bool` | Optional | If `true`, generated managed and native projects define `ARISEN_PROFILER_ENABLED`. Defaults to `false`. |
| `Profiles[Key].Packages` | `array` | Optional | Additional packages formatted identically to the base `Packages` array. These are **appended** to the base list when this specific profile is requested via command line (`--profile Key`). |

Source precedence is intentional: `file://` means local source, `http(s)://` means a restored cache package, and an empty `Url` is fallback discovery. Local folders do not silently override remote manifest entries.

**Behavioral Rule:** If the workspace is launched via its thin executable (e.g., `MyGame.exe`), it automatically invokes the **EngineBootstrapper** logic in the Kernel. The bootstrapper resolves and loads the base `Packages` array by two approaches:

1. **Topological Dependency Sorting**: When the engine reads all the package.json files, it builds a Directed Acyclic Graph (DAG). If package B depends on package A, the Engine mathematically guarantees package A is loaded first.

2. **Subsystem Phases and Priorities**: Packages register "Subsystems". The Engine gathers all subsystems across all packages, groups them by an EnginePhase (like PreInit, Init, PostInit), and then ticks them based on a numeric priority.

3. **Startup Content Selection**: The root composition package prefers `StartupWorld` when present. `IRuntimeWorldStreamingService` activates that descriptor's persistent scene into the stable `SceneSubsystem` entity manager during `PostInit`, and Production cooking closes/deploys its persistent and cell graph under the `startupWorld` root. Streamable cells activate asynchronously after a camera source or explicit pin selects them. Without `StartupWorld`, `StartupScene` remains the compatibility bootstrap through `IRuntimeSceneService`.

4. **Render Pipeline Activation**: A concrete package selected by composition provides the single `IRenderPipelineProvider`. During `RenderSubsystem.Initialize`, the engine passes the selected asset GUID/package identity to that provider, which validates its schema and activates the resulting code-defined pipeline. `RenderPipeline.PackageId` owns the asset; it does not select the provider. The settings asset stores durable quality policy, not an arbitrary pass graph.

Production deployment emits a strict-JSON runtime form of `manifest.json` beside the executable. It retains only project identity, startup scene/world selections, render-pipeline selection, and the resolved package list; every package URL is `file://Packages/<package-id>/`. It contains no workspace, package-source, authoring-asset, or cache path.
---

## 2. Project Identity (`.arisenproj`)

**Location**: Root of user projects.  
**Purpose**: UI metadata for the `ArisenLauncher`. Not generally read by the Kernel during execution.

### Schema Spec
```json
{
  "$schema": "https://arisen.dev/schemas/arisenproj-v1.json",
  "ProjectId": "29849513-097b-42d7-ab98-7d99f34fa4e1",
  "Name": "PackageGame",
  "Description": "My first package-centric game",
  "EngineVersionId": "29849513-097b-42d7-ab98-7d99f34fa4e1",
  "LastModified": "2026-03-20T15:48:05Z",
  "PreviewImageURL": "",
  "IconURL": ""
}
```

### Detailed Field Specification

| Field | Type | Required? | Description |
|---|---|---|---|
| `$schema` | `string` | Optional | Schema URL for IDE validation. |
| `ProjectId` | `string` | **Yes** | A unique Guid generated at creation. Critical for collision prevention in local caches and matchmaking. |
| `Name` | `string` | **Yes** | The display name in the Launcher. |
| `EngineVersionId` | `string` | **Yes** | Guid mapping to the local Engine Installation. |
| `Description` | `string` | Optional | User-defined description. |
| `LastModified` | `string` | Optional | ISO-8601 timestamp tracked by the Launcher. |
| `PreviewImageURL` | `string` | Optional | Relative path to a display banner. |
| `IconURL` | `string` | Optional | Relative path to a thumbnail icon. |

**CRITICAL RULE**: `.arisenproj` MUST NOT contain absolute filesystem paths. Absolute paths break physical project portability.

---

## 3. Package Definition (`package.json`)

**Location**: Root of every package directory (e.g. `Engine/Packages/com.arisen.ecs/package.json` or `Local/com.user.mygame/package.json`).  
**Purpose**: Defines the package's human-authored identity, dependencies, and package-level requirements. Code-derived metadata is emitted to sibling `package.generated.json` and merged by tooling/runtime.

### Schema Spec
```json
{
  "$schema": "https://arisen.dev/schemas/package-v2.json",
  "id": "com.arisen.rhi.vulkan",
  "name": "Arisen RHI — Vulkan Support",
  "version": "1.2.0",
  "layer": "driver",
  "description": "Vulkan rendering backend implementation",
  "author": "Arisen Team",
  
  "engine": {
    "minVersion": "0.1.0"
  },
  "services": {
    "provides": [
      { "interface": "ArisenKernel.Contracts.IRHIFactory", "priority": 100, "capabilities": ["vulkan"] }
    ],
    "requires": [
      "ArisenKernel.Contracts.IWindowProvider",
      { "interface": "ArisenKernel.Contracts.IDebugCapture", "optional": true }
    ]
  },
  "dependencies": {
    "com.arisen.core.native": ">=1.0.0"
  },
  "nativeRuntimes": {
    "win-x64": [
      "RHI.Vulkan.dll",
      "3rdparty/vulkan/Layers/VkLayer_khronos_validation.dll",
      {
        "path": "3rdparty/renderdoc/renderdoc.dll",
        "source": "static",
        "required": false,
        "exports": ["RENDERDOC_GetAPI"]
      }
    ]
  },
  "nativeTests": {
    "win-x64": [
      {
        "library": "Arisen.RHI.Vulkan.Test.dll",
        "registerExport": "RegisterNativeTests"
      }
    ]
  }
}
```

### Detailed Field Specification

| Field | Type | Required? | Description |
|---|---|---|---|
| `$schema` | `string` | Optional | Schema URL for IDE validation. |
| `id` | `string` | **Yes** | The reverse-DNS globally unique identifier (e.g., `com.arisen.ecs`). |
| `name` | `string` | **Yes** | The human-readable display name. |
| `version` | `string` | **Yes** | Semantic versioning string (e.g., `"1.2.0"`). |
| `layer` | `string` | **Yes** | Architectural layer used by validation. Valid values: `foundation`, `domain`, `driver`, `tooling`, `user`, `test`. |
| `entry.assembly` | `string` | Generated | The managed C# DLL filename. Usually emitted into `package.generated.json`. |
| `entry.class` | `string` | Generated | The full name of the class implementing `IPackageEntry`. Usually emitted into `package.generated.json`. |
| `services.provides` | `array` | Optional/generated | C# interfaces this package registers into the Kernel's `ServiceRegistry` on boot. Entries may be strings or objects with a fully qualified `interface` type name, optional integer `priority`, optional string-array `capabilities`, and optional `deferred` for providers that are declared at graph-validation time but registered later. Prefer code generation/attributes for providers. Duplicate selected providers for the same interface are fatal. |
| `services.requires` | `array` | Optional/human | Interfaces this package demands to exist in the registry before it can boot. Entries may be strings or objects with a fully qualified `interface` type name, optional `optional`, optional `deferred`, and optional string-array `capabilities`. When capabilities are listed, validation requires a selected provider for the same interface to advertise every requested capability. `ArisenKernel.Contracts.*` names are validated against the kernel contracts source folder. |
| `subsystems` | `array` | Generated | Types implementing `IEngineSubsystem`. Prefer `package.generated.json`; runtime merges authored and generated entries for transition compatibility. |
| `dependencies` | `object` | Optional | Key-value pairs of required package IDs and their semantic version constraints. |
| `nativeRuntimes` | `object` | Optional | Key-value pairs matching a platform Runtime Identifier (RID) to unmanaged runtime payload declarations. String shorthand remains supported. |
| `nativeTests` | `object` | Optional/test-only | Key-value pairs matching a platform Runtime Identifier (RID) to native test registration declarations. Valid only for packages in the `test` layer. |

### Native Runtime Entries

`nativeRuntimes` entries may use either string shorthand or object form.

String shorthand:

```json
"nativeRuntimes": {
  "win-x64": [
    "RHI.Vulkan.dll",
    "3rdparty/vulkan/Layers/VkLayer_khronos_validation.dll"
  ]
}
```

Rules:
- A bare file name such as `"RHI.Vulkan.dll"` is treated as a build output produced by the package's native project.
- A relative path with `/` or `\` is treated as a static payload copied from the package directory.
- Static payloads are required by default and `ArisenBuildTool validate` fails if they are missing for the active target RID.
- Runtime paths must be package-relative and must not escape the package directory.

Object form:

```json
{
  "path": "3rdparty/vulkan/Layers/VkLayer_khronos_validation.dll",
  "source": "static",
  "required": true,
  "configurations": ["Debug", "Release"],
  "exports": ["vkGetInstanceProcAddr"],
  "initExport": "ArisenNativeInit",
  "shutdownExport": "ArisenNativeShutdown"
}
```

Object fields:

| Field | Type | Required? | Description |
|---|---|---|---|
| `path` / `name` | `string` | **Yes** | Package-relative payload path or build-output file name. |
| `source` / `kind` | `string` | Optional | `static` or `buildOutput`. If omitted, inferred from whether the path contains a directory separator. |
| `required` | `bool` | Optional | Defaults to `true`. Missing optional static payloads produce warnings instead of errors. |
| `configurations` | `array<string>` | Optional | Configuration filter for generated/deployed output validation. If present, `validate-native-output` only checks the entry when the active configuration matches, for example `Debug` or `Release`. |
| `exports` | `array<string>` | Optional | Expected DLL exports. For existing static DLL payloads, `ArisenBuildTool validate` checks these exports. For built/deployed output payloads, `ArisenBuildTool validate-native-output` checks these exports in the generated bin directory before runtime boot. |
| `initExport` | `string` | Optional | Parameterless C ABI lifecycle export called by `PackageSubsystem` when the package is mounted. Return `0` for success; non-zero fails package load. |
| `shutdownExport` | `string` | Optional | Parameterless C ABI lifecycle export called by `PackageSubsystem` in reverse package order during shutdown. Return `0` for success; non-zero is logged during shutdown. |

Native lifecycle exports are optional. Managed or hybrid packages should continue to prefer `IPackageEntry.OnLoad()` / `OnUnload()` when managed code owns lifecycle orchestration. Use native lifecycle exports only for native-only payloads or low-level initialization that must occur inside the native DLL.

### Native Test Entries

`nativeTests` declares native libraries that expose test registration functions for `com.arisen.testrunner`.

```json
"nativeTests": {
  "win-x64": [
    {
      "library": "Arisen.RHI.Vulkan.Test.dll",
      "registerExport": "RegisterNativeTests"
    }
  ]
}
```

Rules:
- `nativeTests` is valid only in packages with `layer: "test"`.
- `library` must be a deployed file name, not a path.
- The same `library` must also appear in `nativeRuntimes` for the same runtime identifier.
- `registerExport` defaults to `RegisterNativeTests` when string shorthand is used.
- At runtime, `com.arisen.testrunner` reads `manifest.resolved.json`, loads each declared library from the output directory, calls the registration export, and fails the test run if a declared library or export cannot be loaded.

### ArisenBuildTool Auto-Generation (Developer UX)

A core philosophy of Arisen Engine is that humans should not manually write complex JSON configurations for compiled code. `package.json` is human-authored package intent; `package.generated.json` is tool-authored code metadata. Build-time validation, workspace generation, and runtime fallback read the **effective package manifest** formed by merging both files.

To achieve this without compromising Developer Experience (DX), the **ArisenBuildTool** writes generated fields to sibling `package.generated.json` instead of mutating human-owned `package.json`:
1. **Subsystems (`subsystems`)**: Developers write standard C# code and tag it with `[EngineSubsystem(Phase, Priority)]`. During compilation, a Roslyn Source Generator or the ArisenBuildTool scans the assembly and writes this JSON node automatically to `package.generated.json`.
2. **Native Runtimes (`nativeRuntimes`)**: When compiling C++ projects via the engine's toolchain, output binaries are automatically registered in generated metadata.
3. **Entry (`entry.assembly`, `entry.class`)**: Inferred automatically from the compiled managed assembly and discovered `IPackageEntry` class.

The *only* fields a user or package author should normally edit in `package.json` are:
- `id`
- `version`
- `layer`
- `name` / `author` / `description`
- `services.requires` (for cross-package service contracts; use string form for required services and object form for `optional`, `deferred`, or capability metadata)
- `dependencies`

### Dependency And Service Intent

`dependencies` and `services` answer different questions:

- `dependencies` decides which packages are in the resolved graph and establishes package load ordering.
- `services.provides` and `services.requires` describe runtime contracts between packages in that selected graph.

Reusable packages should avoid depending on concrete backend providers when a shared contract is sufficient. For example, `com.arisen.rendering` should require RHI contracts, not depend on `com.arisen.rhi.vulkan.native`.

Composition/root packages are different: they choose the concrete product assembly. It is correct for a user/root package to depend on a concrete provider such as `com.arisen.rhi.vulkan.native` when that product selects Vulkan. Without either a manifest entry or a package dependency, the provider package is not selected and can be absent from the generated graph even if another package requires the service contract.

Provider packages then advertise the contracts they implement:

```json
"services": {
  "provides": [
    { "interface": "ArisenKernel.Contracts.IRHIBackend", "capabilities": ["vulkan"] },
    { "interface": "ArisenKernel.Contracts.IRHIDevice", "capabilities": ["vulkan"], "deferred": true }
  ]
}
```

Consumer packages request contracts and capabilities:

```json
"services": {
  "requires": [
    { "interface": "ArisenKernel.Contracts.IRHIBackend", "capabilities": ["vulkan"] }
  ]
}
```

---

## 4. Deployed Launch And Package Metadata

**Location**: Production output root.
**Purpose**: Makes package/project boot independent of the source workspace.

`launch.config.json` schema version 1 uses:

```json
{
  "SchemaVersion": 1,
  "Mode": "Deployed",
  "Profile": "Production"
}
```

Rules:

- `Mode: Deployed` binds project/package metadata to `AppContext.BaseDirectory`.
- `--workspace` and a conflicting `--profile` are rejected for deployed launches.
- `manifest.resolved.json` remains the authoritative topological order and every deployed package URL must resolve beneath `Packages/`.
- `Packages/<package-id>/package.json` is the strict-JSON effective package descriptor formed from authored plus generated package metadata. It contains runtime entry, dependency, service, subsystem, and native declarations but no package source path.
- Package assemblies and native payloads remain co-located in the output root; descriptor directories are metadata-only.
- `.arisen/Projects/{Profile}/manifest.source.resolved.json` is a separate build-stage artifact. It is never copied into the Production output and lets repeat cooking resolve source package roots after deployed metadata finalization.

`ArisenBuildTool deploy-runtime-metadata` stages and validates the complete descriptor set, then replaces `Packages/`, `manifest.json`, `manifest.resolved.json`, and `launch.config.json` with rollback. Replacing the complete owned descriptor tree removes stale package records deterministically.

---

## 5. Runtime Asset Catalog (`runtime-assets.json`)

**Location**: Future profile output beside generated launch/resolved metadata, with artifact paths interpreted relative to that output's supplied content root.
**Purpose**: Defines the immutable, relocatable set of cooked artifacts available to one runtime profile. It is separate from `.arisen/Cache/CookedAssets/AssetManifest.json`, which is mutable development cache state and may contain machine-local paths and timestamps.

`RuntimeAssetCatalog` schema version 1 is strict JSON with exact camelCase property names:

```json
{"schemaVersion":1,"targetProfile":"Production","roots":[{"name":"startupScene","guid":"11111111-2222-3333-4444-555555555555","packageId":"com.user.mygame","assetType":"Scene","variant":"runtime.scene.v1"}],"artifacts":[{"guid":"11111111-2222-3333-4444-555555555555","packageId":"com.user.mygame","assetType":"Scene","variant":"runtime.scene.v1","path":"com.user.mygame/scenes/startup.ariscene","sizeInBytes":4096,"sha256":"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef","formatVersion":1,"dependencies":[{"guid":"22222222-3333-4444-5555-666666666666","packageId":"com.user.mygame","assetType":"Material","variant":"material.runtime","required":true}]},{"guid":"22222222-3333-4444-5555-666666666666","packageId":"com.user.mygame","assetType":"Material","variant":"material.runtime","path":"com.user.mygame/materials/default.arimaterial","sizeInBytes":1024,"sha256":"abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789","formatVersion":6,"dependencies":[]}]}
```

| Field | Type | Required? | Description |
|---|---|---|---|
| `schemaVersion` | `int` | **Yes** | Catalog schema version. The current reader accepts exactly version `1`. |
| `targetProfile` | `string` | **Yes** | Profile for which the closed artifact set was built. |
| `roots` | `array<object>` | **Yes** | Deterministically sorted named entry assets, for example `startupScene`, `startupWorld`, and `renderPipeline`. |
| `roots[].name` | `string` | **Yes** | Unique semantic root name; comparison also rejects case-only duplicates. |
| `roots[].guid` | canonical GUID string | **Yes** | Root artifact GUID. |
| `roots[].packageId` | `string` | **Yes** | Owning package; must exactly match the referenced artifact. |
| `roots[].assetType` | `string` | **Yes** | Typed asset identity; must exactly match the referenced artifact. |
| `roots[].variant` | `string` | **Yes** | Cooked variant; `(guid, variant)` is the catalog lookup key. |
| `artifacts` | `array<object>` | **Yes** | Complete deterministically sorted artifact set. |
| `artifacts[].path` | `string` | **Yes** | Forward-slash content-root-relative path, never a source/cache/machine path. |
| `artifacts[].sizeInBytes` | `long` | **Yes** | Exact deployed file size; must be non-negative. |
| `artifacts[].sha256` | `string` | **Yes** | Exactly 64 lowercase hexadecimal characters covering deployed bytes. |
| `artifacts[].formatVersion` | `int` | **Yes** | Positive package-owned payload format version, independent of catalog schema version. |
| `artifacts[].dependencies` | `array<object>` | **Yes** | Typed dependency identities in canonical order. Every listed dependency resolves inside this catalog. |
| `dependencies[].required` | `bool` | **Yes** | Preserves required/optional consumption intent for validation and diagnostics. |

Rules:

- Catalog roots, artifacts, and dependencies are canonicalized before writing. Serialization contains no timestamps and uses compact strict JSON with one trailing LF, so stable model input produces byte-identical output.
- Root names, `(GUID, variant)` artifact identities, dependency identities within one artifact, and output paths must be unique. Output paths use case-insensitive collision checks for portable deployment.
- Rooted paths, URI/drive prefixes, backslashes, empty path segments, `.`/`..`, trailing dots/spaces, and any path resolving outside the supplied content root are invalid.
- Parsing rejects comments, trailing commas, duplicate JSON properties, unknown properties, wrong JSON types, unsupported schema versions, invalid GUIDs, and malformed hashes.
- Deployment validation rejects missing/tampered/wrong-size files and symbolic-link or reparse-point traversal. Relocating the complete content root is valid because no machine-rooted value is serialized.
- The schema contract is implemented in `com.arisen.core`. Profile closure, cooking, output layout, and emission are owned by `ArisenBuildTool` and remain separate orchestration concerns.
