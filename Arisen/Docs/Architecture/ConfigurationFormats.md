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
| `Packages[].Url` | `string` | Optional | A path to resolve the package locally (`file:///...`) or remotely (`https://...`). If empty, resolves from the Engine's default registry. |
| `Packages[].Version` | `string` | **Yes** | Semantic versioning requirement (e.g., `"1.0.0"`, `"^1.2.0"`). |
| `Profiles` | `object` | Optional | A dictionary of named profiles mapped to additional package arrays. |
| `Profiles[Key]` | `array` | Optional | A list of packages formatted identically to the base `Packages` array. These are **appended** to the base list when this specific profile is requested via command line (`--profile Key`). |

**Behavioral Rule:** If `ArisenHost` is launched without a `--profile` argument, or if the `Profiles` object does not exist, the engine purely resolves and loads the base `Packages` array by two approaches:

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
**Purpose**: Defines the package's identity, dependencies, C# entry assembly, and native requirements.

### Schema Spec
```json
{
  "$schema": "https://arisen.dev/schemas/package-v2.json",
  "id": "com.arisen.rhi.vulkan",
  "name": "Arisen RHI — Vulkan Support",
  "version": "1.2.0",
  "description": "Vulkan rendering backend implementation",
  "author": "Arisen Team",
  
  "engine": {
    "minVersion": "0.1.0"
  },
  "entry": {
    "assembly": "Arisen.RHI.Vulkan.dll"
  },
  "services": {
    "provides": [ { "interface": "ArisenEngine.Core.RHI.IRHIDevice", "priority": 100 } ],
    "requires": [ "ArisenEngine.Core.Platform.IWindowProvider" ]
  },
  "subsystems": [
    { "class": "ArisenEngine.RHI.Vulkan.VulkanSubsystem", "phase": "PreInit", "priority": 5 }
  ],
  "dependencies": {
    "com.arisen.core.native": ">=1.0.0"
  },
  "nativeRuntimes": {
    "win-x64": ["Core.RHI.dll", "RHI.Vulkan.dll"]
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
| `entry.assembly` | `string` | Optional | The managed C# DLL filename located in `lib/net9.0/`. Omitted for pure-data/asset packages. |
| `services.provides` | `array` | Optional | A list of explicit C# interfaces this package registers into the Kernel's `ServiceRegistry` on boot. |
| `services.requires` | `array` | Optional | A list of interfaces this package demands to exist in the registry before it can boot. |
| `subsystems` | `array` | Optional | Types implementing `IEngineSubsystem`. The Kernel automatically instantiates and ticks them based on `phase` and `priority`. |
| `dependencies` | `object` | Optional | Key-value pairs of required package IDs and their semantic version constraints. |
| `nativeRuntimes` | `object` | Optional | Key-value pairs matching a platform Runtime Identifier (RID) to a list of unmanaged DLLs located in `runtimes/{rid}/native/`. |

### ArisenBuildTool Auto-Generation (Developer UX)

A core philosophy of Arisen Engine is that humans should not manually write complex JSON configurations for compiled code. The `package.json` acts as the **highly-optimized compiled output** that the Kernel reads instantly at runtime. 

To achieve this without compromising Developer Experience (DX), the **ArisenBuildTool** completely automates the generation of complex fields:
1. **Subsystems (`subsystems`)**: Developers write standard C# code and tag it with `[EngineSubsystem(Phase, Priority)]`. During compilation, a Roslyn Source Generator or the ArisenBuildTool scans the assembly and injects this JSON node automatically.
2. **Native Runtimes (`nativeRuntimes`)**: When compiling C++ `vcxproj` files via the engine's toolchain, the output binaries are automatically registered here.
3. **Entry Assembly (`entry.assembly`)**: Inferred automatically based on the `.csproj` target output name.

The *only* fields a user or package author should ever manually edit in `package.json` are:
- `id`
- `version`
- `name` / `author` / `description`
- `dependencies`
