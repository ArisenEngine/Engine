# Architecture Spec: Configuration Formats

**Status**: Draft / Active  
**Module**: Entire Ecosystem (Launcher, Kernel, Build Tool)

This document defines the strict `JSON` structural schemas for the core configuration files driving the Arisen Engine's package-centric architecture.

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
  
  "Packages": [
    {
      "Id": "com.user.mygame",
      "Url": "file:///Local/com.user.mygame/",
      "Version": "1.0.0"
    }
  ],

  // Profiles are purely optional
  "Profiles": {
    "Development": [
      {
        "Id": "com.arisen.editor.default",
        "Url": "",
        "Version": "1.0.0"
      }
    ]
  }
}
```

### Detailed Field Specification

| Field | Type | Required? | Description |
|---|---|---|---|
| `$schema` | `string` | Optional | URL to the JSON schema for IDE autocomplete and validation. |
| `Name` | `string` | **Yes** | The user-friendly name of the project. |
| `EngineVersion` | `string` | **Yes** | The specific version of Arisen Engine required (e.g., `"1.2.0"` or `"Current"`). |
| `Packages` | `array` | **Yes** | The base list of packages loaded regardless of what Profile is active. |
| `Packages[].Id` | `string` | **Yes** | The unique identifier of the package to load. |
| `Packages[].Url` | `string` | Optional | A path to resolve the package locally (`file:///...`), a direct remote package archive (`https://.../package.zip`), or a remote registry index (`https://.../registry.json`). If empty, resolves from local/engine package search paths. |
| `Packages[].Version` | `string` | **Yes** | Exact package version or supported registry semantic range (e.g., `"1.0.0"`, `">=1.2.0 <2.0.0"`, `"^1.2.0"`, `"~1.2.0"`, `"*"`). |
| `Profiles` | `object` | Optional | A dictionary of named profiles mapped to additional package arrays. |
| `Profiles[Key]` | `array` | Optional | A list of packages formatted identically to the base `Packages` array. These are **appended** to the base list when this specific profile is requested via command line (`--profile Key`). |

Source precedence is intentional: `file://` means local source, `http(s)://` means a restored cache package, and an empty `Url` is fallback discovery. Local folders do not silently override remote manifest entries.

**Behavioral Rule:** If the workspace is launched via its thin executable (e.g., `MyGame.exe`), it automatically invokes the **EngineBootstrapper** logic in the Kernel. The bootstrapper resolves and loads the base `Packages` array by two approaches:

1. **Topological Dependency Sorting**: When the engine reads all the package.json files, it builds a Directed Acyclic Graph (DAG). If package B depends on package A, the Engine mathematically guarantees package A is loaded first.

2. **Subsystem Phases and Priorities**: Packages register "Subsystems". The Engine gathers all subsystems across all packages, groups them by an EnginePhase (like PreInit, Init, PostInit), and then ticks them based on a numeric priority.
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
| `configurations` | `array<string>` | Optional | Configuration metadata for future config-specific deployment. Current validation parses it but does not filter by it yet. |
| `exports` | `array<string>` | Optional | Expected DLL exports. For existing static DLL payloads, validation checks these exports during `ArisenBuildTool validate`. |
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
