# Arisen Engine — Package-Centric Architecture

**"Everything is a Package."**

This document defines the architecture for evolving Arisen Engine from a monolithic engine into a **microkernel + package** system, where the RHI, rendering pipeline, editor, ECS, and even user projects are all first-class packages composed via manifests.

---

## Table of Contents

1. [Vision & Philosophy](#1-vision--philosophy)
2. [Current State Audit](#2-current-state-audit)
3. [Architecture Overview](#3-architecture-overview)
4. [The Kernel (Shell)](#4-the-kernel-shell)
5. [Package Anatomy — The Multi-DLL Question](#5-package-anatomy--the-multi-dll-question)
6. [Package Manifest v2 Specification](#6-package-manifest-v2-specification)
7. [Interface Contracts (Service Provider Model)](#7-interface-contracts-service-provider-model)
8. [Migration Plan — Current Modules to Packages](#8-migration-plan--current-modules-to-packages)
9. [The Launcher & Package Manager UI](#9-the-launcher--package-manager-ui)
10. [Build System Evolution](#10-build-system-evolution)
11. [Phased Roadmap](#11-phased-roadmap)
12. [FAQ & Design Decisions](#12-faq--design-decisions)

---

## 1. Vision & Philosophy

### The Core Idea
The engine is reduced to a **thin kernel** (the "Shell") that provides only:
- Package discovery, resolution, and loading
- Lifecycle orchestration (boot → tick → shutdown)
- A service registry (interface contracts)
- Minimal diagnostics (logging, profiling bootstrap)

**Everything else**—RHI, rendering pipelines, ECS, physics, audio, editor, user game scripts—is a package that the kernel loads based on a project's manifest.

### Why This Matters
| Benefit | Explanation |
|---|---|
| **Total Customization** | Users compose their engine from packages. A VR studio swaps the RHI. A 2D dev strips 3D entirely. |
| **Parallel Development** | Each package has its own repo, CI, and release cycle. Teams don't block each other. |
| **AI-First** | An AI agent can programmatically compose manifests, swap packages, and generate new ones. |
| **Ecosystem** | Community-built packages extend the engine without touching core code. |
| **Pay-For-What-You-Use** | No dead code. Packages not in the manifest are never loaded. |

### Key Principle: C# is the Package Surface
> **Packages are defined, discovered, and loaded through C#.** A package's public API is always a set of C# interfaces and types. Whether the package internally uses C++, Rust, WASM, or pure C# is an **implementation detail** invisible to consumers.

This directly answers the question: *"We only care about C#, right?"* **Yes.** In "package view," you don't care how a package is implemented. The native DLLs are bundled assets of the package, just like shaders or textures.

---

## 2. Current State Audit

### What Already Exists (Strengths)

The existing codebase already has significant infrastructure toward this vision:

| Component | Location | Status |
|---|---|---|
| `PackageSubsystem` | `Engine/Core/Packages/PackageSubsystem.cs` | ✅ Manifest-based discovery, dependency resolution (topological sort), `AssemblyLoadContext` isolation |
| `PackageLoadContext` | `Engine/Core/Packages/PackageLoadContext.cs` | ✅ Handles **both** managed assemblies AND native DLLs via `LoadUnmanagedDll` |
| `DefaultPackageResolver` | `Engine/Core/Packages/DefaultPackageResolver.cs` | ✅ Supports `file://`, `http://`, `https://` URLs, ZIP extraction |
| `PackageManifest` | `Engine/Core/Packages/PackageSubsystem.cs` (inner class) | ✅ `package.json` with id, version, entryAssembly, entryClass, dependencies |
| `ProjectManifest` | `Engine/Core/Lifecycle/ProjectManifest.cs` | ✅ `.arisen` project file with `PackageRequirement` list |
| CMake `BuildBuiltinPackages` | `CMakeLists.txt:267-291` | ✅ Auto-discovers and builds all `.csproj` in `Packages/Builtin/` |
| `ArisenLauncher` | `Editor/ArisenLauncher/` | ✅ Engine version management, project creation/launching |
| Existing package | `com.arisen.builtin.forward-rp` | ✅ Real working package with native dependency management |

### What Needs Migration

| Current Module | Current Location | Target Package ID |
|---|---|---|
| Core C++ (Foundation + HAL + Diagnostic) | `Core/Core.Foundation`, `Core.HAL`, `Core.Diagnostic` | `com.arisen.core.native` (Kernel-embedded, NOT a package) |
| RHI C++ (Core.RHI + RHI.Vulkan + RHI.DX12) | `Core/Core.RHI`, `Core/RHI.Vulkan`, `Core/RHI.DX12` | `com.arisen.rhi.vulkan`, `com.arisen.rhi.dx12` |
| RHI C# Wrappers | `Engine/Core/RHI/` | Part of RHI packages above |
| AutoBinding (Generated PInvoke) | `AutoBinding/` | Embedded in each native package |
| ECS | `Engine/Core/ECS/` | `com.arisen.ecs` |
| Rendering Infrastructure | `Engine/Rendering/` | `com.arisen.rendering.core` |
| Forward RP | `Packages/Builtin/ForwardRP/` | `com.arisen.rendering.forward` (already exists) |
| Platform/Windowing | `Engine/Platform/` | `com.arisen.platform.desktop` |
| Asset Pipeline | `Engine/Core/Assets/` | `com.arisen.assets` |
| Serialization | `Engine/Core/Serialization/` | `com.arisen.serialization` |
| Editor | `Editor/ArisenEditor/` | `com.arisen.editor.default` |
| Editor Framework | `External/ArisenEditorFramework/` | `com.arisen.editor.framework` |
| DAG / Node Canvas | `External/ArisenDAG`, `External/ArisenNodeCanvas` | `com.arisen.tools.dag`, `com.arisen.tools.nodecanvas` |

---

## 3. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│                    ARISEN LAUNCHER                           │
│  ┌───────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ Project       │  │   Package    │  │  Engine Version  │  │
│  │ Manager       │  │   Browser    │  │  Manager         │  │
│  └───────────────┘  └──────────────┘  └──────────────────┘  │
└────────────────────────────┬─────────────────────────────────┘
                             │ reads project.arisen
                             ▼
┌──────────────────────────────────────────────────────────────┐
│                    ARISEN KERNEL (Shell)                      │
│                                                              │
│  ┌────────────┐ ┌──────────────┐ ┌────────────────────────┐ │
│  │ Manifest   │ │  Dependency  │ │   Service Registry     │ │
│  │ Parser     │ │  Resolver    │ │   (Interface Contracts) │ │
│  └────────────┘ └──────────────┘ └────────────────────────┘ │
│  ┌────────────┐ ┌──────────────┐ ┌────────────────────────┐ │
│  │ Package    │ │  Lifecycle   │ │   Memory Bootstrap     │ │
│  │ Loader     │ │  Manager     │ │   (FrameArena, Native) │ │
│  └────────────┘ └──────────────┘ └────────────────────────┘ │
│  ┌────────────┐ ┌──────────────┐                            │
│  │ Logging    │ │  Profiler    │  ← minimal bootstrap only  │
│  │ Bootstrap  │ │  Bootstrap   │                            │
│  └────────────┘ └──────────────┘                            │
└────────────────────────────┬─────────────────────────────────┘
                             │ loads packages in dependency order
              ┌──────────────┼──────────────┬────────────────┐
              ▼              ▼              ▼                ▼
     ┌──────────────┐ ┌──────────┐ ┌─────────────┐ ┌──────────────┐
     │ RHI.Vulkan   │ │   ECS    │ │  Rendering  │ │    Editor    │
     │ Package      │ │ Package  │ │  Package    │ │   Package    │
     │              │ │          │ │             │ │              │
     │ ┌──────────┐ │ │ C# only  │ │ ┌─────────┐ │ │ ┌──────────┐ │
     │ │C# API    │ │ │          │ │ │C# API   │ │ │ │Avalonia  │ │
     │ │(PInvoke) │ │ │          │ │ │(SRP)    │ │ │ │Views +   │ │
     │ ├──────────┤ │ │          │ │ └─────────┘ │ │ │ViewModels│ │
     │ │Native/   │ │ │          │ │             │ │ └──────────┘ │
     │ │ Core.RHI │ │ │          │ │             │ │              │
     │ │ RHI.Vk   │ │ │          │ │             │ │              │
     │ │ Core.HAL │ │ │          │ │             │ │              │
     │ └──────────┘ │ │          │ │             │ │              │
     └──────────────┘ └──────────┘ └─────────────┘ └──────────────┘
```

---

## 4. The Kernel (Shell)

The Kernel is the **only non-package assembly**. It is the entry point (`ArisenKernel.dll`) that boots the engine.

### What Lives in the Kernel

| Component | Why It's in the Kernel |
|---|---|
| **Manifest Parser** | Must exist before any package loads |
| **Dependency Resolver** | Topological sort, version conflict detection |
| **Package Loader** | `AssemblyLoadContext` management, native DLL loading |
| **Lifecycle Manager** | `EnginePhase` orchestration (PreInit → Init → PostInit → Running → Shutdown) |
| **Service Registry** | Interface-based service locator for cross-package communication |
| **Logger Bootstrap** | Minimal console logger (packages can replace with a full implementation) |
| **Profiler Bootstrap** | Tracy bootstrapping (or stub if profiler package not present) |
| **Memory Bootstrap** | `FrameArena`, `NativeArray<T>` — fundamental allocators |
| **Configuration** | `EngineConfig`, project manifest loading |

### What Moves OUT of the Kernel

Everything currently in `Engine/Core/` that is domain-specific:
- `ECS/` → `com.arisen.ecs`
- `RHI/` → Part of RHI packages
- `Assets/` → `com.arisen.assets`
- `Rendering/` → `com.arisen.rendering.core`
- `Serialization/` → `com.arisen.serialization`
- `Platform/` → `com.arisen.platform.desktop`

### Kernel Source Structure (Post-Migration)

```
Engine/
└── ArisenKernel/
    ├── ArisenKernel.csproj
    ├── Lifecycle/
    │   ├── EngineKernel.cs           (existing, refined)
    │   ├── EnginePhase.cs            (existing)
    │   ├── IEngineSubsystem.cs       (existing)
    │   ├── ITickableSubsystem.cs     (existing)
    │   ├── EngineConfig.cs           (existing)
    │   └── Time.cs                   (existing)
    ├── Packages/
    │   ├── PackageSubsystem.cs       (existing, enhanced)
    │   ├── PackageLoadContext.cs      (existing)
    │   ├── PackageManifest.cs         (extracted from inner class)
    │   ├── IPackageResolver.cs        (existing)
    │   ├── DefaultPackageResolver.cs  (existing)
    │   └── ArisenPackageInfo.cs       (existing)
    ├── Services/
    │   ├── IServiceRegistry.cs        (NEW)
    │   └── ServiceRegistry.cs         (NEW)
    ├── Diagnostics/
    │   ├── Logger.cs                  (existing, minimal)
    │   └── Profiler.cs                (existing, minimal)
    └── Memory/
        ├── FrameArena.cs              (existing)
        └── NativeArray.cs             (existing)
```

---

## 5. Package Anatomy — The Multi-DLL Question

> *"My RHI implementation contains C++ and C#. In package view, we don't need to care about how this package is implemented. As RHI Package, it may contain several DLLs. How to manage those DLLs?"*

### The Answer: Bundle Pattern

A package is a **self-contained directory** containing everything it needs — managed DLLs, native DLLs, shaders, assets, and metadata. The consumer never sees the internals.

```
com.arisen.rhi.vulkan/
├── package.json              ← manifest (entry point, deps, native runtimes)
├── lib/
│   └── net9.0/
│       ├── Arisen.RHI.Vulkan.dll          ← C# entry assembly
│       ├── Arisen.RHI.Vulkan.pdb
│       └── Arisen.AutoBinding.RHI.dll     ← Generated PInvoke bindings
├── runtimes/
│   ├── win-x64/
│   │   └── native/
│   │       ├── Core.Foundation.dll        ← C++ native DLLs
│   │       ├── Core.HAL.dll
│   │       ├── Core.Diagnostic.dll
│   │       ├── Core.RHI.dll
│   │       ├── RHI.Vulkan.dll
│   │       └── TracyClient.dll
│   ├── linux-x64/
│   │   └── native/
│   │       ├── libCore.Foundation.so
│   │       ├── ...
│   │       └── libRHI.Vulkan.so
│   └── osx-arm64/
│       └── native/
│           └── ...
├── shaders/                  ← Package-specific shader files
│   ├── sky.vert.spv
│   └── sky.frag.spv
└── docs/
    └── README.md
```

### How Native DLLs Are Loaded

Your existing `PackageLoadContext` **already handles this**:

```csharp
// PackageLoadContext.cs (existing code)
protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
{
    // AssemblyDependencyResolver automatically searches runtimes/{rid}/native/
    string? libraryPath = m_Resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
    if (libraryPath != null)
    {
        return LoadUnmanagedDllFromPath(libraryPath);
    }
    return IntPtr.Zero;
}
```

The `AssemblyDependencyResolver` in .NET automatically probes the `runtimes/{rid}/native/` directory if the package follows the NuGet runtime layout convention. **No extra code needed.**

### Native DLL Sharing Across Packages

When multiple packages depend on the same native DLL (e.g., both `com.arisen.rhi.vulkan` and `com.arisen.shader-compiler` need `Core.Foundation.dll`):

**Strategy: Shared Native Core Package**

```
com.arisen.core.native/               ← "Platform Abstraction Layer" package
├── package.json
├── runtimes/
│   └── win-x64/native/
│       ├── Core.Foundation.dll
│       ├── Core.HAL.dll
│       ├── Core.Diagnostic.dll
│       └── TracyClient.dll
└── lib/net9.0/
    └── Arisen.Core.Native.dll        ← Thin C# wrapper with PInvoke for foundation APIs
```

Then RHI packages declare a **dependency** on `com.arisen.core.native`:

```json
{
    "id": "com.arisen.rhi.vulkan",
    "dependencies": {
        "com.arisen.core.native": ">=1.0.0"
    }
}
```

The `PackageSubsystem` loads `com.arisen.core.native` first (topological sort), so its native DLLs are available when `com.arisen.rhi.vulkan` loads.

### Summary: Package DLL Management Rules

| Rule | Detail |
|---|---|
| **1. All native DLLs go in `runtimes/{rid}/native/`** | Follows NuGet convention. `AssemblyDependencyResolver` finds them automatically. |
| **2. Shared natives = separate package** | If multiple packages need `Core.Foundation.dll`, put it in `com.arisen.core.native` and depend on it. |
| **3. The C# entry assembly is the public API** | Consumers only see the C# types. PInvoke details are internal. |
| **4. AutoBinding per package** | Each package that wraps C++ generates its own AutoBinding DLL, shipped inside `lib/`. |
| **5. Platform-specific builds** | The build system produces per-RID native directories. Only the matching platform's natives are loaded. |

---

## 6. Package Manifest v2 Specification

Evolve the current `package.json` to support the new requirements:

```json
{
    "$schema": "https://arisen.dev/schemas/package-v2.json",
    "id": "com.arisen.rhi.vulkan",
    "name": "Arisen RHI — Vulkan Backend",
    "version": "1.2.0",
    "description": "Vulkan-based Rendering Hardware Interface for Arisen Engine.",
    "author": "Arisen Team",
    "license": "MIT",
    "tags": ["rhi", "vulkan", "graphics", "builtin"],

    "engine": {
        "minVersion": "0.2.0",
        "maxVersion": "1.*"
    },

    "entry": {
        "assembly": "Arisen.RHI.Vulkan.dll",
        "class": "ArisenEngine.RHI.Vulkan.VulkanRHIPackage"
    },

    "services": {
        "provides": [
            { "interface": "ArisenEngine.Core.RHI.IRHIDevice", "priority": 100 },
            { "interface": "ArisenEngine.Core.RHI.IRHIFactory", "priority": 100 }
        ],
        "requires": [
            "ArisenEngine.Core.Platform.IWindowProvider"
        ]
    },

    "subsystems": [
        {
            "class": "ArisenEngine.RHI.Vulkan.VulkanSubsystem",
            "phase": "PreInit",
            "priority": 5
        }
    ],

    "dependencies": {
        "com.arisen.core.native": ">=1.0.0",
        "com.arisen.platform.desktop": ">=1.0.0"
    },

    "nativeRuntimes": {
        "win-x64": ["Core.RHI.dll", "RHI.Vulkan.dll"],
        "linux-x64": ["libCore.RHI.so", "libRHI.Vulkan.so"]
    },

    "category": "rhi",
    "repository": "https://github.com/ArisenEngine/rhi-vulkan"
}
```

### Key New Fields

| Field | Purpose |
|---|---|
| `services.provides` | Declares which interfaces this package implements. The kernel registers them in the Service Registry. |
| `services.requires` | Declares which interfaces this package needs. The kernel validates all requirements are satisfied before Init. |
| `subsystems[]` | Auto-registers subsystems into `EngineKernel` during package loading. No manual `RegisterSubsystem<T>()` calls needed. |
| `nativeRuntimes` | Explicit listing of native DLLs per platform. Enables the Launcher to show platform compatibility. |
| `category` | Classification for the Package Manager UI (rhi, rendering, physics, editor, tools, etc.). |
| `engine.minVersion` / `maxVersion` | Strict engine compatibility range. |

---

## 7. Interface Contracts (Service Provider Model)

Packages communicate through **interfaces**, not concrete types. The Kernel provides a `ServiceRegistry` that packages register into and query from.

### Core Interfaces (Shipped with the Kernel)

These live in the Kernel DLL and define the contract that packages implement:

```
ArisenKernel/
└── Contracts/
    ├── IRHIDevice.cs              ← "Give me a GPU device"
    ├── IRHIFactory.cs             ← "Create textures, buffers, pipelines"
    ├── IRenderPipeline.cs         ← "Execute a frame's render passes"
    ├── IWindowProvider.cs         ← "Give me a native window handle"
    ├── IEntityManager.cs          ← "Create/destroy/query entities"
    ├── IAssetDatabase.cs          ← "Load/save/query assets"
    ├── IEditorShell.cs            ← "The editor UI host"
    └── IInputProvider.cs          ← "Poll keyboard/mouse/gamepad"
```

### How It Works

```csharp
// 1. Kernel boots and creates the registry
var registry = new ServiceRegistry();

// 2. RHI.Vulkan package loads, its entry class registers services:
public class VulkanRHIPackage : IPackageEntry
{
    public void OnLoad(IServiceRegistry services)
    {
        services.Register<IRHIDevice>(new VulkanDevice());
        services.Register<IRHIFactory>(new VulkanFactory());
    }
}

// 3. Rendering package queries the RHI (doesn't know it's Vulkan):
public class ForwardRenderPipeline : IRenderPipeline
{
    public void Initialize(IServiceRegistry services)
    {
        var device = services.Get<IRHIDevice>();  // Gets VulkanDevice
        var factory = services.Get<IRHIFactory>(); // Gets VulkanFactory
    }
}
```

### Swapping Implementations

To switch from Vulkan to DX12, the user changes one line in their project manifest:

```diff
"packages": [
-   { "id": "com.arisen.rhi.vulkan", "version": "1.2.0" },
+   { "id": "com.arisen.rhi.dx12", "version": "1.0.0" },
    { "id": "com.arisen.rendering.forward", "version": "1.0.0" }
]
```

The rendering package doesn't change — it only talks to `IRHIDevice`.

---

## 8. Migration Plan — Current Modules to Packages

### Phase Overview

```
Current Monolith              →        Package-Based
─────────────────                     ────────────────
ArisenEngine.dll (everything)   →     ArisenKernel.dll (shell only)
                                      + com.arisen.core.native
                                      + com.arisen.ecs
                                      + com.arisen.rhi.vulkan
                                      + com.arisen.rendering.core
                                      + com.arisen.rendering.forward
                                      + com.arisen.platform.desktop
                                      + com.arisen.assets
                                      + com.arisen.serialization
                                      + com.arisen.editor.default
```

### Detailed Module Mapping

#### Package 1: `com.arisen.core.native` (Foundation)
**From:** `Core/Core.Foundation` + `Core/Core.HAL` + `Core/Core.Diagnostic` + `AutoBinding/{Diagnostics,HALWindowAPI}`

| Component | Source | Destination |
|---|---|---|
| `Core.Foundation.dll` (C++) | `Core/Core.Foundation/` | `runtimes/win-x64/native/Core.Foundation.dll` |
| `Core.HAL.dll` (C++) | `Core/Core.HAL/` | `runtimes/win-x64/native/Core.HAL.dll` |
| `Core.Diagnostic.dll` (C++) | `Core/Core.Diagnostic/` | `runtimes/win-x64/native/Core.Diagnostic.dll` |
| `TracyClient.dll` (C++) | `3rdparty/tracy/` | `runtimes/win-x64/native/TracyClient.dll` |
| PInvoke wrappers (C#) | `AutoBinding/Diagnostics/`, `HALWindowAPI.cs` | `lib/net9.0/Arisen.Core.Native.dll` |

#### Package 2: `com.arisen.rhi.vulkan`
**From:** `Core/Core.RHI` + `Core/RHI.Vulkan` + `AutoBinding/RHI/` + `Engine/Core/RHI/`

| Component | Source | Destination |
|---|---|---|
| `Core.RHI.dll` (C++) | `Core/Core.RHI/` | `runtimes/win-x64/native/Core.RHI.dll` |
| `RHI.Vulkan.dll` (C++) | `Core/RHI.Vulkan/` | `runtimes/win-x64/native/RHI.Vulkan.dll` |
| C# RHI wrappers | `Engine/Core/RHI/*.cs` | `lib/net9.0/Arisen.RHI.Vulkan.dll` |
| AutoBinding RHI | `AutoBinding/RHI/` | `lib/net9.0/Arisen.RHI.Vulkan.dll` (compiled in) |
| **Depends on** | | `com.arisen.core.native` |

#### Package 3: `com.arisen.rhi.dx12`
**From:** `Core/Core.RHI` + `Core/RHI.DX12`

Same structure as Vulkan but with DX12 native DLLs. Shares `Core.RHI.dll`.

#### Package 4: `com.arisen.ecs`
**From:** `Engine/Core/ECS/`

| Component | Source | Destination |
|---|---|---|
| `Entity.cs`, `EntityManager.cs` | `Engine/Core/ECS/` | `lib/net9.0/Arisen.ECS.dll` |
| `ComponentPool.cs`, `IComponent.cs` | `Engine/Core/ECS/` | `lib/net9.0/Arisen.ECS.dll` |
| All component types | `Engine/Core/ECS/*.cs` | `lib/net9.0/Arisen.ECS.dll` |
| Systems | `Engine/Core/ECS/Systems/` | `lib/net9.0/Arisen.ECS.dll` |
| **Depends on** | | `com.arisen.core.native` (for NativeArray) |

#### Package 5: `com.arisen.platform.desktop`
**From:** `Engine/Platform/`

| Component | Source | Destination |
|---|---|---|
| `PlatformSubsystem.cs` | `Engine/Platform/` | `lib/net9.0/Arisen.Platform.Desktop.dll` |
| `WindowProcessor.cs` | `Engine/Platform/` | Same |
| Desktop-specific code | `Engine/Platform/Desktop/` | Same |
| **Depends on** | | `com.arisen.core.native` |
| **Provides** | | `IWindowProvider` |

#### Package 6: `com.arisen.rendering.core`
**From:** `Engine/Rendering/` (minus pipeline implementations)

| Component | Source | Destination |
|---|---|---|
| `RenderSubsystem.cs` | `Engine/Rendering/` | `lib/net9.0/Arisen.Rendering.Core.dll` |
| `RenderContext.cs`, `Camera.cs` | `Engine/Rendering/` | Same |
| `RenderPipelineManager.cs` | `Engine/Rendering/` | Same |
| `RenderSurface.cs` | `Engine/Rendering/` | Same |
| **Depends on** | | `com.arisen.ecs`, RHI (any provider of `IRHIDevice`) |

#### Package 7: `com.arisen.rendering.forward` (Already Exists!)
**From:** `Packages/Builtin/ForwardRP/`

Already a package. Just needs to declare `services.provides` in manifest v2.

#### Package 8: `com.arisen.assets`
**From:** `Engine/Core/Assets/`

#### Package 9: `com.arisen.serialization`
**From:** `Engine/Core/Serialization/`

#### Package 10: `com.arisen.editor.default`
**From:** `Editor/ArisenEditor/`

| Component | Source | Destination |
|---|---|---|
| Views, ViewModels | `Editor/ArisenEditor/Core/` | `lib/net9.0/Arisen.Editor.Default.dll` |
| Commands | `Editor/ArisenEditor/Core/Commands/` | Same |
| Themes, Templates | `Editor/ArisenEditor/Themes/` | Same |
| **Depends on** | | `com.arisen.editor.framework`, `com.arisen.ecs`, `com.arisen.rendering.core` |
| **Provides** | | `IEditorShell` |

---

## 9. The Launcher & Package Manager UI

The Launcher evolves from a simple project opener into the **central hub** for engine management.

### Current Launcher Capabilities
- Engine version management (discovery, add, select)
- Project creation via `NewProjectViewModel`
- Project launching via `LauncherProcessService`
- Recent project tracking

### New Capabilities Needed

#### 9.1 Package Manager Window
A new window/panel accessible from the Launcher and from within the Editor:

```
┌──────────────────────────────────────────────────────┐
│  Package Manager                              [x]    │
├──────────────────────────────────────────────────────┤
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ │
│ │ Installed │ │ Registry │ │ Updates  │ │  Local   │ │
│ └──────────┘ └──────────┘ └──────────┘ └──────────┘ │
├──────────────────────────────────────────────────────┤
│ Search: [________________________] [Category ▼]      │
├──────────────────────────────────────────────────────┤
│                                                      │
│ ┌──────────────────────────────────────────────────┐ │
│ │ 🎮 com.arisen.rhi.vulkan           v1.2.0  [✓]  │ │
│ │    Vulkan-based RHI backend                      │ │
│ │    ⚡ Native: Core.RHI, RHI.Vulkan              │ │
│ ├──────────────────────────────────────────────────┤ │
│ │ 🎮 com.arisen.rhi.dx12           v1.0.0  [ ]    │ │
│ │    DirectX 12 RHI backend                        │ │
│ ├──────────────────────────────────────────────────┤ │
│ │ 🧩 com.arisen.ecs                v1.0.0  [✓]    │ │
│ │    Entity Component System                       │ │
│ ├──────────────────────────────────────────────────┤ │
│ │ 🎨 com.arisen.rendering.forward  v1.0.0  [✓]    │ │
│ │    Forward Rendering Pipeline                    │ │
│ ├──────────────────────────────────────────────────┤ │
│ │ 🛠️  com.arisen.editor.default     v1.0.0  [✓]    │ │
│ │    Default Arisen Editor                         │ │
│ └──────────────────────────────────────────────────┘ │
│                                                      │
│         [Apply Changes]  [Resolve Dependencies]      │
└──────────────────────────────────────────────────────┘
```

#### 9.2 Project Template System
When creating a new project, the Launcher presents templates that are themselves package presets:

| Template | Pre-installed Packages |
|---|---|
| **3D Game (Vulkan)** | `core.native`, `ecs`, `rhi.vulkan`, `rendering.core`, `rendering.forward`, `platform.desktop`, `assets`, `editor.default` |
| **3D Game (DX12)** | Same but `rhi.dx12` instead of `rhi.vulkan` |
| **2D Game** | `core.native`, `ecs`, `platform.desktop`, `rendering.2d`, `assets`, `editor.default` |
| **Headless Simulation** | `core.native`, `ecs` (no RHI, no Editor) |
| **Custom** | Empty manifest — user picks everything |

#### 9.3 Manifest Editor
A visual editor for `project.arisen` that shows:
- Installed packages with versions
- Dependency graph visualization (using ArisenDAG!)
- Conflict detection (two RHI backends, version mismatches)
- One-click add/remove/update

---

## 10. Build System Evolution

### Current Build Flow
```
CMake → Build C++ (7 modules) → BindingGenerator → AutoBinding → C# Engine → Builtin Packages → Editor
```

### Target Build Flow
```
CMake → Build C++ per-package → BindingGenerator per-package → Package Assembly
         ↓                                                       ↓
    com.arisen.core.native/runtimes/win-x64/native/        lib/net9.0/*.dll
         ↓
    Package Layout (package.json + lib/ + runtimes/)
```

### Key Build System Changes

1. **CMakeLists.txt per C++ module stays** — C++ modules are still built with CMake.
2. **Package Assembly Step (NEW)** — A post-build step that copies outputs into the package directory layout.
3. **Per-package AutoBinding** — The `BindingGenerator` runs per C++ package, not globally.
4. **Package `.csproj` references** — Each package's C# project references the Kernel and its C++ native outputs.

### Example: Building `com.arisen.rhi.vulkan`

```
1. CMake builds Core.Foundation.dll, Core.HAL.dll, Core.RHI.dll, RHI.Vulkan.dll
2. BindingGenerator scans Core.RHI + RHI.Vulkan headers → generates PInvoke code
3. dotnet build Arisen.RHI.Vulkan.csproj (references ArisenKernel, includes generated PInvoke)
4. PackageAssembly copies:
   - Arisen.RHI.Vulkan.dll → com.arisen.rhi.vulkan/lib/net9.0/
   - Core.RHI.dll, RHI.Vulkan.dll → com.arisen.rhi.vulkan/runtimes/win-x64/native/
   - package.json is already in the package root
```

---

## 11. Phased Roadmap

### Phase A: Define Contracts & Extract Kernel *(Week 1-2)*

> **Goal:** Create `ArisenKernel.csproj` with service registry and interface contracts. No existing code breaks.

- [ ] Create `ArisenKernel/` project with lifecycle, packages, memory, and diagnostics code
- [ ] Define core interface contracts (`IRHIDevice`, `IRenderPipeline`, `IWindowProvider`, `IEntityManager`, etc.)
- [ ] Implement `ServiceRegistry` (register, get, has, getAll)
- [ ] Enhance `PackageManifest` v2 (services.provides, services.requires, subsystems auto-registration)
- [ ] Enhance `IPackageEntry` interface with `OnLoad(IServiceRegistry)` lifecycle
- [ ] Keep existing `ArisenEngine.csproj` working — Kernel is additive

### Phase B: Extract First Package — ECS *(Week 2-3)*

> **Goal:** Prove the architecture by extracting the simplest domain module.

- [ ] Create `Packages/com.arisen.ecs/` directory and `package.json`
- [ ] Move `Engine/Core/ECS/` into the package's C# project
- [ ] Package implements `IEntityManager` and registers it via `ServiceRegistry`
- [ ] Update `ArisenEngine.csproj` to reference the ECS package (or load it dynamically)
- [ ] All existing tests pass

### Phase C: Extract RHI Packages *(Week 3-5)*

> **Goal:** Solve the hard problem — multi-DLL native packages.

- [ ] Create `Packages/com.arisen.core.native/` with all foundation C++ DLLs
- [ ] Create `Packages/com.arisen.rhi.vulkan/` with Vulkan-specific C++ + C# code
- [ ] Define `IRHIDevice`, `IRHIFactory` contracts in Kernel
- [ ] Split `AutoBinding/` into per-package generated code
- [ ] Update CMake to produce package directory layouts
- [ ] RHI package provides `IRHIDevice` via `ServiceRegistry`
- [ ] Rendering code queries `IRHIDevice` instead of concrete VulkanDevice

### Phase D: Extract Remaining Packages *(Week 5-7)*

- [ ] Extract `com.arisen.platform.desktop`
- [ ] Extract `com.arisen.rendering.core`
- [ ] Extract `com.arisen.assets`
- [ ] Extract `com.arisen.serialization`
- [ ] Update `com.arisen.rendering.forward` to v2 manifest
- [ ] Delete `ArisenEngine.csproj` — replaced by `ArisenKernel.csproj` + packages

### Phase E: Editor as Package *(Week 7-9)*

- [ ] Extract `com.arisen.editor.framework`
- [ ] Extract `com.arisen.editor.default`
- [ ] Editor discovers available packages and shows Package Manager UI
- [ ] Users can run the engine without editor (headless mode with just Kernel + packages)

### Phase F: Launcher Evolution & Package Registry *(Week 9-12)*

- [ ] Add Package Manager window to Launcher
- [ ] Implement local package registry (scan `Packages/` directories)
- [ ] Implement remote package registry (HTTP-based, similar to npm)
- [ ] Project templates based on package presets
- [ ] Dependency conflict detection and resolution UI

---

## 12. FAQ & Design Decisions

### Q: Do we need to change the C++ build at all?
**A: Minimally.** The C++ modules (`Core.Foundation`, `Core.RHI`, `RHI.Vulkan`, etc.) are still built by CMake exactly as they are today. The only change is a **post-build copy step** that places the compiled DLLs into the package's `runtimes/{rid}/native/` directory. The C++ code itself doesn't change.

### Q: Can users write packages in C++ too?
**A: Yes.** A user package could contain C++ native DLLs with a C# PInvoke wrapper, exactly like the RHI package. They could even use the `BindingGenerator` to auto-generate bindings for their own C++ code.

### Q: What about hot-reloading?
**A: The `PackageLoadContext` is already `isCollectible: true`.** This means packages loaded in isolated contexts can be unloaded and reloaded. For the Editor, this enables:
- Hot-reload user game script packages during development
- Swap rendering pipelines without restarting

### Q: How does this affect build times?
**A: It improves them.** Instead of rebuilding the entire monolithic `ArisenEngine.dll`, you only rebuild the specific package you changed. The Kernel rarely changes. C++ modules are only rebuilt when their source changes.

### Q: Can a user build their own Editor?
**A: Absolutely.** That's the point. The user creates a package that provides `IEditorShell` and registers it instead of `com.arisen.editor.default`. The Kernel doesn't care who provides the editor — it just needs *something* that implements the interface, or nothing at all (headless mode).

### Q: What if no package provides a required service?
**A: The Kernel validates before entering `Running` phase.** After all packages are loaded, the kernel checks that every `services.requires` declaration from every loaded package has a matching `services.provides`. If not, it logs an error and refuses to start, showing the user exactly which interfaces are unsatisfied.

### Q: How do packages discover each other's types?
**A: Through the Kernel's contracts only.** Package A should never directly reference Package B's assembly. Instead:
1. Both reference `ArisenKernel.dll` which contains the shared interface
2. Package B registers its implementation
3. Package A queries the `ServiceRegistry` for the interface

This ensures packages are truly decoupled and swappable.

---

*This document is the architectural north star for the "Everything is a Package" evolution of Arisen Engine. It should be updated as design decisions are refined during implementation.*
