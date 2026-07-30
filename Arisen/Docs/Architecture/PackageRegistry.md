# Arisen Engine Package Registry

This registry lists all core packages currently located in the `Local/` directory, defining their domains and specific responsibilities within the microkernel architecture.

## 📦 Package Tiers

Packages are categorized into six layers based on their proximity to the Kernel and the User:

1.  **Foundation**: Direct kernel extensions, core contracts, low-level primitives, platform providers, and execution foundations.
2.  **Domain**: Core engine features (Rendering, ECS, Resources, generic render pipelines) that may depend on Foundation and peer Domain packages.
3.  **Driver**: Concrete hardware/backend implementations, such as Vulkan RHI, that should depend only on Foundation contracts/payloads.
4.  **Tooling**: Editor and visual authoring environments.
5.  **User**: Final application logic and high-level project configuration.
6.  **Test**: Test runner and package-specific test fixtures.

`package.json` must declare one of these values in its `layer` field. `ArisenBuildTool validate` enforces layer dependencies:

| Package layer | May depend on |
| :--- | :--- |
| `foundation` | `foundation` |
| `domain` | `foundation`, `domain` |
| `driver` | `foundation` |
| `tooling` | `foundation`, `domain`, `tooling` |
| `user` | `foundation`, `domain`, `driver`, `tooling`, `user` |
| `test` | any layer |

---

## Package Registry Source Format

A package registry source is a static JSON index plus package archives. The initial format is intentionally CDN-friendly and can be served from any HTTP file host.

Default index filename:

```text
registry.json
```

Index schema:

```json
{
  "schemaVersion": 1,
  "packages": [
    {
      "id": "com.example.inventory",
      "version": "1.0.0",
      "name": "com.example.inventory",
      "description": "Inventory package",
      "type": "managed",
      "layer": "user",
      "archive": {
        "url": "https://packages.example.com/arisen/com.example.inventory-1.0.0.zip",
        "sha256": "lowercase-hex-sha256",
        "sizeBytes": 12345
      }
    }
  ]
}
```

Rules:

- `schemaVersion` is currently `1`.
- `packages` is sorted by package id, then version, for reproducible diffs.
- `id` and `version` come from the archive root `package.json` and are required.
- `archive.url` points at a package zip whose root contains `package.json`.
- `archive.sha256` is the lowercase SHA-256 hash of the zip file.
- `archive.sizeBytes` is the zip file size in bytes.
- The index does not include a generation timestamp; publishing the same archives should produce identical JSON.

The index can be generated from archives created by `ArisenBuildTool pack`:

```bat
ArisenBuildTool.exe registry-index --source ".arisen\Packages" --base-url "https://packages.example.com/arisen" --output ".arisen\Packages\registry.json"
```

Manifest support accepts two remote package forms:

- direct package archive URL: `Url` points to a `.zip` and the archive is restored directly.
- registry index URL: `Url` points to `registry.json`, `Id` names the package, and `Version` selects a package version from the index.

Registry `Version` supports exact versions and these semantic range forms:

- `1.2.3`
- `>=1.2.0 <2.0.0`
- `^1.2.3`
- `~1.2.3`
- `*`

When a range matches multiple indexed package versions, the launcher selects the highest matching SemVer version. It verifies the downloaded archive against `archive.sha256`, extracts it into `.Cache/{packageId}`, and records the requested range plus the concrete `ResolvedVersion` in `.arisen/package-lock.json`. If the registry later offers a newer matching version, restore fails until the cached package and lock entry are removed, keeping range-based restores reproducible by default.

The Package Manager UI can also inspect a registry URL before restore. The remote package flow lists package-version entries from `registry.json`; selecting one fills the manifest package `Id` and exact `Version` while keeping `Url` pointed at the registry index. A user may also type a supported range into `Version`; confirmation resolves it to the highest matching exact version.

### Local Override Policy

Package source selection is explicit and comes from the workspace manifest:

- `Url: "file://..."` uses that local package path.
- `Url: "https://.../registry.json"` or `Url: "https://.../package.zip"` uses `.Cache/{packageId}` after launcher restore.
- An empty `Url` is a fallback lookup and may search `Local/{packageId}`, `.Cache/{packageId}`, then engine packages.

If a manifest entry points at a remote source and a same-ID folder exists under `Local/`, the local folder is ignored. `ArisenBuildTool validate` warns about this so local source never silently shadows a reproducible registry/cache dependency. To intentionally override a registry package with local source, change the manifest entry to `file://Local/{packageId}`. The Package Manager exposes this as a `Use Local` action when a same-ID local package exists.

---

## 🏗️ Core Package Directory

| Package ID | Layer | Duty |
| :--- | :--- | :--- |
| **`com.arisen.core`** | Foundation | Managed lifecycle, service registry abstractions, base types, and foundation services such as `ILogger` / `ICommandManager`. |
| **`com.arisen.core.native`** | Foundation | Monolithic C++ payload (Foundation, HAL, Diagnostics, RHI base types, Shader Compiler) exposed through managed facade services. |
| **`com.arisen.dag`** | Foundation | Generic Directed Acyclic Graph (DAG) system for data-driven execution logic. |
| **`com.arisen.taskgraph`** | Foundation | High-performance, multi-threaded internal **Job System** for engine-wide concurrency. |
| **`com.arisen.platform.desktop`** | Foundation | Desktop platform provider (Win32/X11 windowing, OS message loops). |
| **`com.arisen.ecs`** | Domain | Optimized Entity Component System (ECS) using memory-contiguous pools. |
| **`com.arisen.rendering`** | Domain | **RenderGraph Architecture**, RenderPipeline interfaces, and base shading logic. |
| **`com.arisen.resources`** | Domain | Asset Database, resource serialization, and background data discovery. |
| **`com.arisen.rhi.vulkan.native`** | Driver | Native Vulkan implementation of the Rendering Hardware Interface (RHI). |
| **`com.arisen.editor`** | Tooling | The official Avalonia-based visual authoring environment. |
| **`com.arisen.nodecanvas`** | Tooling | Foundation for node-based visual editing and graph manipulation. |
| **`com.arisen.generic-renderpipeline`** | Domain | High-level default RenderPipeline implemented via RenderGraph. |
| **`com.arisen.terrain`** | Domain | Terrain source/cooked data, scene components, runtime queries, LOD planning, diagnostics, and streaming smoke ownership. |
| **`com.arisen.terrain.generic-renderpipeline`** | Domain | Optional Generic RP terrain preparation, layered rendering, cascaded shadows, and deferred device-resource ownership. |
| **`com.arisen.terrain.editor`** | Tooling | Terrain import, sculpt/paint authoring, diagnostics, transactional save/reimport/cook, and SceneView previews. |
| **`com.arisen.packagegame`** | User | The main assembly and project root for the active application/game. |

---

## 🔗 Key Relationships

### The Rendering Chain
`com.arisen.rhi.vulkan.native` $\rightarrow$ `com.arisen.rendering` $\rightarrow$ `com.arisen.generic-renderpipeline`
The RHI provides the hardware commands $\rightarrow$ Rendering provides the Graph architecture $\rightarrow$ The Pipeline provides the visual look.

### The Execution Foundation
`com.arisen.dag` $\rightarrow$ `com.arisen.taskgraph`
The DAG provides the logic for resolving node dependencies, while the TaskGraph executes those dependencies across all available CPU cores.

### The Authoring Stack
`com.arisen.nodecanvas` $\rightarrow$ `com.arisen.editor`
NodeCanvas provides the generic graph UI that the Editor uses for material editing, state machines, and render graph visualization.
