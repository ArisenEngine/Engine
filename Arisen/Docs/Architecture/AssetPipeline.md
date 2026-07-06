# Architecture Spec: Asset Pipeline & Resource Management

**Status**: Draft / Active  
**Module**: `com.arisen.core` contract + `com.arisen.resources` provider

In a Data-Oriented, Zero-Overhead engine, the way assets (Textures, Meshes, Audio, Scenes) are loaded from disk into memory is paramount. String parsing, JSON deserialization, and large heap allocations during runtime loading will immediately destroy target frame rates ("hitches").

Arisen Engine completely solves this using a strict **Cooked Binary Asset Pipeline**.

---

## 1. Golden Rule: No String Paths at Runtime
**NEVER** load or reference an asset by its filesystem path at runtime.
```csharp
// FATAL ERROR IN CODE REVIEW:
var texture = AssetDatabase.Load<Texture>("Assets/Models/Player/Skin.png");
```

Files are renamed, moved to new folders, and renamed again. Hardcoded string paths guarantee broken game builds. 

In Arisen, **all assets are referenced via a `Guid`**.
```csharp
// CORRECT:
var texture = AssetDatabase.Load<Texture>(new Guid("A1B2C3D4-..."));
```

---

## 2. Source Assets vs. Meta Files

When a designer drags a raw asset (e.g., `Sword.fbx` or `Skin.png`) into a workspace or package `Assets/` folder, the Asset Importer immediately generates an adjacent `.meta` file written in YAML.

### The Meta File (`Skin.png.meta`)
```yaml
Guid: a1b2c3d4-e5f6-7890-1234-56789abcdef0
AssetType: Texture2D
Importer: TextureImporter
ImporterSettings:
  compression: DXT5
  generateMipMaps: true
```

1. **The Guid is Stable**: The Guid is generated once and forever belongs to that asset. If the user moves `Skin.png` to `Assets/Weapons/Skin.png`, the Editor simply moves the `.meta` file alongside it. Any Component wrapping that Guid structurally remains perfectly intact.
2. **YAML**: As defined in `ConfigurationFormats.md`, we use YAML here because human developers frequently need to merge `.meta` files in Git, and YAML handles line-by-line merges cleanly.

Current implementation notes:
- `ArisenEngine.Core.Assets.IAssetDatabase` is the foundation contract.
- `com.arisen.resources` provides the runtime service and indexes only `Assets/` folders in the workspace and mounted packages.
- Missing `.meta` files are generated only for files inside those `Assets/` folders, avoiding metadata churn across source code.
- C# source files are ignored even if a package still has a legacy `Assets` namespace folder for asset-system code.
- Duplicate GUIDs are fatal during indexing.

---

## 3. The Cooking Pipeline (Zero-Copy Loading)

Arisen Engine **does not** load raw `.png` or `.fbx` files at runtime. Parsing PNG headers or JSON models during the game loop is overwhelmingly slow.

Instead, we use a **Cooking Pipeline**:
1. During the Build process (or Editor import), the engine reads the raw source asset and the `.meta` file's settings.
2. It compiles ("cooks") the data into an optimized, memory-aligned **Binary Blob** (`.arisenasset`).
3. For example, a `.png` is decompressed, converted to raw GPU-ready DXT5 bytes, and saved directly under `.arisen/Cache/CookedAssets/{Guid}/`.

### Zero-Copy Loading
When the engine runs and a subsystem calls `AssetDatabase.Load(guid)`, the Engine does NOT parse data. 
It memory-maps (`mmap`) the cooked binary blob directly from the SSD into RAM. Because the binary blob was structurally laid out during the Cooking phase to exactly match the C# `struct` or C++ layout, the CPU does zero processing. The bytes are instantly recognizable.

---

## 4. The Asset Database & Lifecycle

The `IAssetDatabase` (provided by a core Service) acts as the global librarian.

1. **Manifest File**: During cooking, the Asset Pipeline generates `.arisen/Cache/CookedAssets/AssetManifest.json` (or a future binary equivalent) that contains a lookup table mapping `Guid + Variant` -> `CookedBinaryPath`.
2. **Runtime Loading**: When a Guid is requested, the Database checks its GUID index and cooked artifact manifest. If it isn't loaded, it finds the binary path from the manifest, maps or reads the cooked file, and hands back a generation-checked `CookedAssetHandle`.
3. **Reference Counting**: The Database tracks how many systems are currently holding a handle to an asset. When the reference count drops to 0, the AssetDatabase explicitly unloads the memory pointer to prevent RAM bloat.
4. **Setup-Time Consumption**: Rendering code resolves and reads cooked bytes during pipeline/resource setup. RenderGraph pass recording must only use already-created GPU/RHI resources, never source assets or the asset database.

## 5. First Vertical Slice

The first implemented cooked asset path is the generic render pipeline smoke triangle shader:

- Source: `com.arisen.generic-renderpipeline/Assets/Shaders/SmokeTriangle.hlsl`
- Stable asset GUID: `98433827-04d5-4d19-8712-8d21596ed9ad`
- Cooked variants: `vulkan1.3.VSMain.spv` and `vulkan1.3.PSMain.spv`
- Output root: `.arisen/Cache/CookedAssets/9843382704d54d1987128d21596ed9ad/`

`SmokeTrianglePass.Prepare()` may cook the shader when the pipeline is created or when the source is newer than the cooked output. Pass recording does not perform asset discovery, source parsing, or shader compilation.

The current shader path now uses a first production-facing `ShaderAsset` model:

- `ShaderAsset` describes the source GUID, stages, entry points, backend, target environment, shader model, optimization mode, and optional defines/includes.
- `ShaderVariantKey` generates deterministic cooked variants such as backend + target environment + shader model + optimization + entry point.
- `ShaderAssetCooker` owns HLSL -> cooked bytecode, cooked manifest registration, and `CookedAssetHandle` loading.
- Render passes consume cooked stage handles during setup and keep pass recording free of source asset work.

The remaining production shader asset work should build on this layer and add:

- Dependency tracking so includes and importer settings invalidate cooked artifacts.
- Editor import and hot-reload hooks.
- Generated `AssetRef<ShaderAsset>` constants or serialized fields so user code never hardcodes GUID strings manually.

The second implemented cooked asset path is a minimal Texture2D slice:

- Source: `com.arisen.generic-renderpipeline/Assets/Textures/SmokeChecker.ppm`
- Stable asset GUID: `c320bf66-0495-4e70-8f27-d54e90dd6c8d`
- Source importer: `PpmTextureImporter`
- Cooked variant: `r8g8b8a8unorm.srgb.nomips`
- Output root: `.arisen/Cache/CookedAssets/c320bf6604954e708f27d54e90dd6c8d/`

The current Texture2D path uses:

- `Texture2DAsset` for the stable GUID, asset name, source format, and cooked variant.
- `Texture2DVariantKey` for deterministic cooked payload variants.
- `Texture2DAssetCooker` for source-time PPM parsing, cooked binary header/payload emission, cooked manifest registration, and `CookedAssetHandle` loading.
- `RHITexture2DResource` for setup-time GPU upload through a staging buffer, image layout transitions, `CopyBufferToImage`, image view creation, sampler creation, and bindless image/sampler registration.

This slice proves non-shader cooked asset data, runtime loading by GUID, conversion into a backend-owned RHI image, and visible shader sampling through the global bindless descriptor set. `SmokeTrianglePass` caches the texture's bindless image and sampler indices during setup and pushes them as a tiny unmanaged constant block during command recording. RenderGraph pass recording still never parses source texture data, queries the asset database, or performs upload work.

The next production material step should lift this pass-specific convention into reusable shader/material binding metadata so user-authored materials can declare texture references without hardcoding pass-local push constants.

The third implemented cooked asset path is a minimal static mesh slice:

- Source: `com.arisen.generic-renderpipeline/Assets/Meshes/SmokeTriangle.armesh`
- Stable asset GUID: `95a9e255-5ed4-48cb-ac65-7673a1002f9e`
- Source importer: `ArisenTextMeshImporter`
- Cooked variant: `staticmesh.uint32`
- Output root: `.arisen/Cache/CookedAssets/95a9e2555ed448cbac657673a1002f9e/`

The current Mesh path uses:

- `MeshAsset` for the stable GUID, asset name, source format, and cooked variant.
- `MeshVariantKey` for deterministic cooked payload variants.
- `MeshAssetCooker` for source-time text mesh parsing, cooked binary header/payload emission, cooked manifest registration, and `CookedAssetHandle` loading.
- `RHIStaticMeshResource` for setup-time vertex/index buffer creation and payload upload from cooked bytes.

This slice proves mesh data can follow the same GUID -> cooked payload -> RHI resource -> RenderGraph draw path as shaders and textures. `SmokeTrianglePass` declares the static mesh vertex layout in pipeline state, binds the cooked vertex/index buffers through `RenderCommandList`, and records an indexed draw. RenderGraph pass recording still never parses source mesh data, queries the asset database, or performs buffer upload work.
