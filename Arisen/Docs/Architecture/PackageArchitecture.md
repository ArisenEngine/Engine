# Arisen Engine Package Architecture

Arisen Engine is built on the principle that **"Everything is a Package"**. This architecture ensures that the engine is highly modular, customizable, and scalable.

## 📦 What is a Package?

A package in Arisen is a self-contained unit of functionality. It consists of:
-   **Manifest**: A `package.json` file that defines the package's ID, version, name, entry point, services, and dependencies.
-   **Source Code**: The managed C# or native C++ code that implements the package's functionality.
-   **Assets**: Any resources (textures, models, shaders) required by the package.
-   **Services**: A list of interfaces the package provides or requires from the `ServiceRegistry`.

## 🏗️ Architecture Layers

Arisen Engine organizes its core packages into six logical layers to manage complexity and dependencies. Every package declares its layer in `package.json`, and `ArisenBuildTool validate` rejects reverse dependencies.

### 1. Foundation (Lowest Layer)
These packages form the absolute base of the engine.
-   **`com.arisen.core`**: Provides the base types and the core `ServiceRegistry`.
-   **`com.arisen.core.native`**: The monolithic C++ foundation, providing hardware abstraction (HAL) and low-level diagnostics.
-   **`com.arisen.dag`**: A generic graph-based execution system.
-   **`com.arisen.taskgraph`**: The high-performance job system for all multi-threaded operations.
-   **`com.arisen.platform.desktop`**: The desktop platform/window provider.

### 2. Domain (Core Features)
This layer implements the standard features expected of a game engine.
-   **`com.arisen.ecs`**: The foundational Entity Component System.
-   **`com.arisen.rendering`**: The RenderGraph and core pipeline management.
-   **`com.arisen.resources`**: Background asset discovery and database management.
-   **`com.arisen.generic-renderpipeline`**: The default concrete RenderPipeline implemented via RenderGraph.

### 3. Driver (Backend Implementations)
Concrete hardware/API backends. Driver packages should depend only on Foundation packages and expose backend functionality through shared contracts/services.
-   **`com.arisen.rhi.vulkan.native`**: The primary Vulkan hardware driver implementation.

### 4. Tooling (Editor & Authoring)
Packages that provide the visual environment for creating games.
-   **`com.arisen.editor`**: The main Avalonia host and panel management system.
-   **`com.arisen.nodecanvas`**: A reusable UI foundation for all node-based editing.

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

## 🔗 The "Everything is a Package" Vision

In Arisen, **no core system is hard-coded into the kernel**. This means:
-   **RHI Swapping**: If you want to use DirectX 12 instead of Vulkan, the user/composition package replaces `com.arisen.rhi.vulkan.native` with a `com.arisen.rhi.dx12.native` package and updates its `IRHIBackend` capability requirement. Because render/domain packages depend on shared contracts instead of concrete backend packages, the rest of the engine remains unaffected.
-   **UI Hosting**: If you don't want to use an Editor, you exclude `com.arisen.editor` and build your game purely using game-level packages.
-   **Platform Portability**: Porting to a new platform (like macOS or Android) is as simple as writing a new `com.arisen.platform.[platform-name]` package that provides its own `IWindowProvider`.

---

## 🛠️ Package Guidelines

Every package MUST adhere to the following rules to maintain engine integrity:

1.  **Zero-Overhead in Hot Paths**: Packages in the Rendering or ECS layers must NOT perform managed allocations in their update loops.
2.  **Service-Based Decoupling**: Packages should interact with each other via the `IServiceRegistry` whenever possible, rather than direct assembly references.
3.  **Strict Manifests**: All dependencies MUST be declared in the `package.json` to allow the Arisen Build Tool to correctly resolve the project's dependency graph.
