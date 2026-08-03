# Arisen Engine Package Architecture

Arisen Engine is built on the principle that **"Everything is a Package"**. This architecture ensures that the engine is highly modular, customizable, and scalable.

## What Is A Package?

A package in Arisen is a self-contained unit of functionality. It consists of:
-   **Manifest**: A `package.json` file that defines the package's ID, version, name, entry point, services, and dependencies.
-   **Source Code**: The managed C# or native C++ code that implements the package's functionality.
-   **Assets**: Any resources (textures, models, shaders) required by the package.
-   **Services**: A list of interfaces the package provides or requires from the `ServiceRegistry`.

## Architecture Layers

Arisen Engine organizes its core packages into six logical layers to manage complexity and dependencies. Every package declares its layer in `package.json`, and `ArisenBuildTool validate` rejects reverse dependencies.

### 1. Foundation (Lowest Layer)
These packages form the absolute base of the engine.
-   **`ArisenKernel.Contracts`**: Kernel-owned service contracts shared across packages, validated by `ArisenBuildTool`.
-   **`com.arisen.core`**: Provides managed base types and package-level foundation services such as `ILogger` and `ICommandManager`.
-   **`com.arisen.core.native`**: The monolithic C++ foundation payload for Foundation, HAL, Diagnostics, RHI base types, and Shader Compiler. It is exposed to packages through managed facade services rather than direct service registration from native code.
-   **`com.arisen.dag`**: A generic graph-based execution system.
-   **`com.arisen.taskgraph`**: The shared worker executor for one-shot task graphs and reusable compiled simulation schedules.
-   **`com.arisen.platform.desktop`**: The desktop platform/window provider.

### 2. Domain (Core Features)
This layer implements the standard features expected of a game engine.
-   **`com.arisen.ecs`**: The foundational Entity Component System.
-   **`com.arisen.rendering`**: The RenderGraph and core pipeline management.
-   **`com.arisen.resources`**: Background asset discovery and database management.
-   **`com.arisen.generic-renderpipeline`**: The default concrete RenderPipeline implemented via RenderGraph.
-   **`com.arisen.vegetation`**: Backend-neutral vegetation identity, runtime cluster data, bounded queries, diagnostics, and authoring-preview contracts.

### 3. Driver (Backend Implementations)
Concrete hardware/API backends. Driver packages should depend only on Foundation packages and expose backend functionality through shared contracts/services.
-   **`com.arisen.rhi.vulkan.native`**: The primary Vulkan hardware driver implementation.

### 4. Tooling (Editor & Authoring)
Packages that provide the visual environment for creating games.
-   **`com.arisen.editor`**: The main Avalonia host and panel management system.
-   **`com.arisen.nodecanvas`**: A reusable UI foundation for all node-based editing.
-   **`com.arisen.vegetation.editor`**: Optional vegetation authoring extension selected by the Editor profile.

Optional authoring packages extend the Editor through the setup-only
`IEditorExtensionRegistry` service. An adapter registers one stable
`IEditorExtension` during package `OnLoad`; the Editor freezes extensions in
`(Order, ExtensionId)` order before Avalonia starts and accepts bounded panel,
SceneView-overlay, menu-provider, and property-editor contributions while
constructing the UI. SceneView overlay registrations are independently ordered
and duplicate-checked; each package owns its control, visibility UI, state, and
disposal without adding a feature dependency to `com.arisen.editor`.
Panel descriptors select a dock region without requiring the Editor package to
reference the feature package. Registrations are cleaned up when the UI exits,
the registry is unfrozen, and adapter packages unregister during reverse package
shutdown. An empty frozen extension set is valid, so `com.arisen.editor` starts
without optional authoring packages selected.

### 5. User (Application & Projects)
The highest non-test layer where the final project is assembled.
-   **`com.arisen.packagegame`**: The specific game logic and root configuration.

### 6. Test
Test runner and package-specific test fixtures may depend on any layer because they are excluded from normal runtime profiles.

Layer dependency policy:

| Package layer | May depend on |
| :--- | :--- |
| `foundation` | `foundation` |
| `domain` | `foundation`, `domain` |
| `driver` | `foundation` |
| `tooling` | `foundation`, `domain`, `tooling` |
| `user` | `foundation`, `domain`, `driver`, `tooling`, `user` |
| `test` | any layer |

---

## Composition And Provider Selection

Package composition is explicit. A package can consume a contract without directly referencing a concrete provider type, but the selected workspace graph must still include a concrete provider package.

This creates two different dependency styles:

1. **Reusable domain packages** depend on shared contracts and foundation packages. They express runtime needs through `services.requires`, for example requiring `ArisenKernel.Contracts.IRHIDevice` or `ArisenKernel.Contracts.IRHIBackend` with a capability such as `vulkan`.
2. **Composition/root packages** choose concrete providers. A game/root package may declare a normal package dependency on `com.arisen.rhi.vulkan.native`, `com.arisen.rhi.dx12.native`, or a future Metal backend because that package is deciding which backend ships in the product.

This is not a contradiction. Domain code stays backend-agnostic, while the root package keeps the selected concrete provider from being culled out of the package graph.

Example:

```json
{
  "id": "com.arisen.packagegame",
  "layer": "user",
  "dependencies": {
    "com.arisen.rendering": "1.0.0",
    "com.arisen.generic-renderpipeline": "1.0.0",
    "com.arisen.rhi.vulkan.native": "1.0.0"
  },
  "services": {
    "requires": [
      { "interface": "ArisenKernel.Contracts.IRHIBackend", "capabilities": ["vulkan"] }
    ]
  }
}
```

The Vulkan package then declares the contracts it provides:

```json
{
  "id": "com.arisen.rhi.vulkan.native",
  "layer": "driver",
  "services": {
    "provides": [
      { "interface": "ArisenKernel.Contracts.IRHIBackend", "capabilities": ["vulkan"] },
      { "interface": "ArisenKernel.Contracts.IRHIDevice", "capabilities": ["vulkan"], "deferred": true }
    ]
  }
}
```

`IRHIDevice` and the other cross-backend RHI contracts are shared contracts. Vulkan, DX12, and Metal packages implement/provide them; they do not each define incompatible versions of the same engine contract.

### Vegetation Package Composition

Vegetation follows the same composition rule while keeping ownership explicit:

- `com.arisen.vegetation` is the reusable domain package. It owns package-neutral
  contracts and immutable runtime snapshots and does not depend on a concrete
  render pipeline, Editor, or RHI backend.
- `com.arisen.vegetation.generic-renderpipeline` is the optional Generic RP
  adapter. It depends on the vegetation runtime and Generic RP feature registry,
  resolves services once during package load, and unregisters its feature before
  package unload.
- `com.arisen.vegetation.editor` is the optional authoring adapter. It depends on
  the vegetation runtime and Editor extension registry and is selected only by
  the `Editor` profile.

The canonical `PackageGame` composition selects the runtime and Generic RP
adapter in `Editor`, `Development`, and `Production`, and selects the Editor
adapter only in `Editor`. `RHIVulkanTesting` remains vegetation-free. The
workspace/composition package still selects `com.arisen.rhi.vulkan.native`; no
vegetation package depends on Vulkan, so a future DX12 or Metal composition can
replace the provider without changing vegetation contracts.

---

## The "Everything is a Package" Vision

In Arisen, **no core system is hard-coded into the kernel**. This means:
-   **RHI Swapping**: If you want to use DirectX 12 instead of Vulkan, the user/composition package replaces `com.arisen.rhi.vulkan.native` with a `com.arisen.rhi.dx12.native` package and updates its `IRHIBackend` capability requirement. Because render/domain packages depend on shared contracts instead of concrete backend packages, the rest of the engine remains unaffected.
-   **UI Hosting**: If you don't want to use an Editor, you exclude `com.arisen.editor` and build your game purely using game-level packages.
-   **Platform Portability**: Porting to a new platform (like macOS or Android) is as simple as writing a new `com.arisen.platform.[platform-name]` package that provides its own `IWindowProvider`.

---

## Package Guidelines

Every package MUST adhere to the following rules to maintain engine integrity:

1.  **Zero-Overhead in Hot Paths**: Packages in the Rendering or ECS layers must NOT perform managed allocations in their update loops.
2.  **Service-Based Decoupling**: Packages should interact with each other via the `IServiceRegistry` whenever possible, rather than direct assembly references.
3.  **Strict Manifests**: All dependencies MUST be declared in the `package.json` to allow the Arisen Build Tool to correctly resolve the project's dependency graph.
