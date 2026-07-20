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
- `.meta` files are sidecars, not source assets. Runtime indexing, editor import scans, and generated asset refs skip metadata files themselves so the pipeline never creates or consumes `.meta.meta` assets.
- Imported dependency sidecars such as glTF `.bin` buffers are indexed as `AssetType: AssetDependency` with importer `GltfBufferDependency`. They keep stable `.meta` GUIDs for editor/runtime diagnostics and file-watch events, but `ArisenBuildTool` skips dependency-only assets when generating package asset refs so user code cannot accidentally load them as first-class runtime assets.
- Generated/imported child assets can carry an optional `Generated` metadata block in their `.meta` sidecar. That block records the source GUID, source package id, child kind, child key, and importer that produced the child. Child GUIDs are derived deterministically by `GeneratedAssetIdentity` from `(sourceGuid, packageId, childKind, childKey)`, so reimport preserves child identity as long as the source asset identity and child key are preserved.
- C# source files are ignored even if a package still has a legacy `Assets` namespace folder for asset-system code.
- Duplicate GUIDs are fatal during indexing.
- The editor importer writes the runtime metadata shape (`Guid`, `AssetType`, `Importer`) and tracks importer/package ownership in the editor asset registry cache.
- Editor rename/move handling preserves asset identity as a small registry/sidecar transaction: the GUID registered at the old path is authoritative when present, the old `.meta` sidecar moves with the source and replaces a racing destination sidecar, the destination registry row is verified, and only then is a `Renamed` event published. Transient sidecar move failures flow back to `AssetImportScheduler` for retry. Because generated model child GUIDs derive from the stable root GUID rather than its path, renaming or moving a `.arismodel` root does not change generated scene, mesh, material, or texture identity.
- `ArisenBuildTool` scans package `Assets/**/*.meta` sidecars during managed project generation and emits disposable generated asset constants into `.arisen/Projects/{Profile}/{Package}/Generated/*AssetRefs.g.cs` for real source assets, skipping metadata files themselves. Packages that can reference `com.arisen.core` get typed `AssetRef<T>` constants alongside legacy `Guid` constants; packages without that dependency still get plain GUID constants. For authored `.arismaterial` assets, the same generated file also exposes nested texture slot and typed property-name constants derived from the material source.

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

The first implemented cooked asset path was the generic render pipeline smoke shader:

- Source: `com.arisen.generic-renderpipeline/Assets/Shaders/SmokeTriangle.hlsl`
- Stable asset GUID: `98433827-04d5-4d19-8712-8d21596ed9ad`
- Cooked variants: `vulkan1.3.VSMain.spv` and `vulkan1.3.PSMain.spv`
- Output root: `.arisen/Cache/CookedAssets/9843382704d54d1987128d21596ed9ad/`

Shader resource setup may cook the shader when a pipeline/material is prepared or when the source is newer than the cooked output. Pass recording does not perform asset discovery, source parsing, or shader compilation.

The current shader path now uses a first production-facing `ShaderAsset` model and supports both plain HLSL and the first ShaderLab authoring slice:

- `ShaderAsset` describes the source GUID, stages, entry points, backend, target environment, shader model, optimization mode, optional always-on defines/includes, and an optional active compile-time keyword set.
- `ShaderVariantKey` generates deterministic cooked variants such as backend + target environment + shader model + optimization + active keyword set + entry point. Empty keyword sets intentionally omit the keyword suffix, so existing no-keyword smoke shaders keep stable variant names.
- `ShaderAssetCooker` owns HLSL -> cooked bytecode, cooked manifest registration, and `CookedAssetHandle` loading.
- `.shader` sources are parsed through `ShaderLabSource`, which selects the first supported SubShader/Pass, derives stage entry points from pragmas, derives render state and material contracts, writes per-stage HLSL intermediates, then feeds those intermediates into the same DXC/cooked artifact path.
- Plain `.hlsl` sources remain supported and continue to use lightweight `@arisen.material.*` annotations for shader-owned material contracts.
- Render passes consume cooked stage handles during setup and keep pass recording free of source asset work.

Shader variant policy is split deliberately:

- `Defines` are always-on compiler defines authored by the material or setup code.
- `Keywords` are the active compile-time variant keyword set. They become compiler defines and are encoded into the cooked shader artifact name and pipeline variant identity.
- ShaderLab `#pragma multi_compile` and `#pragma shader_feature` declarations advertise valid compile-time keywords; they do not automatically enable every keyword as a define. Materials select the active set explicitly through `Shader.Keywords`.
- Runtime specialization constants are reserved for small values that do not change shader interface, descriptor layout, push-constant layout, vertex input, render state, or pipeline compatibility. They are not the primary variant system.
- Future ShaderGraph should generate the same readable ShaderLab/HLSL, material contract, and keyword metadata artifacts rather than adding a parallel runtime shader path.

The first ShaderLab source asset used by the static mesh smoke path is:

- Source: `com.arisen.generic-renderpipeline/Assets/Shaders/SmokeStaticMesh.shader`
- Stable asset GUID: `54bdea4a-0af3-4cd5-aa4f-fcfb15e65117`
- Source importer: `ShaderLab`
- Cooked variants: `vulkan1.3.VSMain.spv` and `vulkan1.3.PSMain.spv`

The remaining production shader asset work should build on this layer and add:

- Full variant matrix generation for compile-time keyword sets.
- Serialized fields/editor pickers that store generated `AssetRef<ShaderSourceAsset>` values so user code never hardcodes GUID strings manually.

The second implemented cooked asset path is a minimal Texture2D slice:

- Source: `com.arisen.generic-renderpipeline/Assets/Textures/SmokeChecker.ppm`
- Stable asset GUID: `c320bf66-0495-4e70-8f27-d54e90dd6c8d`
- Source importer: `PpmTextureImporter`
- Cooked variant: `r8g8b8a8unorm.srgb.nomips`
- Output root: `.arisen/Cache/CookedAssets/c320bf6604954e708f27d54e90dd6c8d/`

The current Texture2D path uses:

- `Texture2DAsset` for the stable GUID, asset name, source format, and cooked variant.
- `Texture2DVariantKey` for deterministic cooked payload variants.
- `Texture2DAssetCooker` for source-time `.ppm` parsing or PNG/JPEG image decode, cooked binary header/payload emission, cooked manifest registration, and `CookedAssetHandle` loading.
- `RHITexture2DResource` for setup-time GPU upload through a staging buffer, image layout transitions, `CopyBufferToImage`, image view creation, sampler creation, and bindless image/sampler registration.

This slice proves non-shader cooked asset data, runtime loading by GUID, conversion into a backend-owned RHI image, and visible shader sampling through the global bindless descriptor set. RenderGraph pass recording still never parses source texture data, queries the asset database, or performs upload work.

The first environment-texture asset path builds on Texture2D source identity without treating a panorama as an ordinary material binding:

- `.arienvironment` files index as `AssetType: EnvironmentTexture`, use importer `ArisenEnvironmentTextureImporter`, and generate `AssetRef<EnvironmentTextureSourceAsset>` helpers.
- The source schema contains `Version`, `Name`, a typed `SourceTexture` GUID/package reference, `Layout`, `SourceColorSpace`, `RuntimeFormat`, `RotationDegrees`, and `Intensity`.
- The first supported source layout is a strict 2:1 `LatLong` image. The visible-sky runtime format is linear `R16G16B16A16SFloat` with one mip; image-based-lighting derivatives are stored in a separate cooked artifact so the authored panorama identity and direct sky upload remain stable.
- PNG/JPEG/PPM inputs decode through the shared Texture2D source decoder. `.hdr` files index as `Texture2D` through `HdrTextureImporter` and decode through `StbImageSharp.ImageResultFloat`, preserving source values before half-float conversion.
- `EnvironmentTextureAssetCooker` writes a versioned `latlong.r16g16b16a16sfloat.nomips` payload. Its dependency stamp and recook time include both the `.arienvironment` descriptor/meta and referenced Texture2D source/meta.
- `RHIEnvironmentTextureResource` consumes only the cooked payload during setup, uploads it through the shared `RHITexture2DAllocation`, and exposes prepared bindless image/sampler indices plus rotation/intensity metadata. The lat-long sampler repeats U and clamps V.
- `EnvironmentLightingAssetCooker` consumes the cooked linear panorama and writes `ibl.latlong.rgba16f.v1` for the same environment GUID. The payload contains tightly packed half-float diffuse irradiance, GGX-prefiltered specular mips, and a split-sum BRDF integration LUT. Cooked-registry presence plus the combined descriptor/source dependency time controls cache reuse; invalidating the environment GUID invalidates both visible-sky and IBL variants.
- `RHIEnvironmentLightingResource` uploads all three payloads during setup and owns their bindless image/sampler registrations. The shared 2D uploader validates packed mip sizes, copies each mip subresource, configures the sampler's full LOD range, and leaves every mip shader-readable before frame recording.

The package-owned first asset is `com.arisen.packagegame/Assets/Environments/BlueHour.arienvironment`, backed by the package-authored `BlueHourPanorama.ppm` source. `LanternShowcaseScene.arisenscene` stores the environment GUID; scenes without a valid optional reference continue to use the procedural sky. Diffuse irradiance, roughness-aware specular prefiltering, and the BRDF integration LUT are now prepared and cached; StandardLit consumption is the next rendering slice.

The third implemented cooked asset path is a minimal static mesh slice:

- Source: `com.arisen.generic-renderpipeline/Assets/Meshes/SmokeTriangle.armesh`
- Stable asset GUID: `95a9e255-5ed4-48cb-ac65-7673a1002f9e`
- Source importer: `ArisenTextMeshImporter`
- Cooked variant: `staticmesh.uint32`
- Output root: `.arisen/Cache/CookedAssets/95a9e2555ed448cbac657673a1002f9e/`

The current Mesh path uses:

- `MeshAsset` for the stable GUID, asset name, source format, and cooked variant.
- `MeshVariantKey` for deterministic cooked payload variants.
- `MeshAssetCooker` for source-time `.armesh` or Wavefront `.obj` parsing, cooked binary header/payload emission, cooked manifest registration, and `CookedAssetHandle` loading.
- `RHIStaticMeshResource` for setup-time staging upload into device-local vertex/index buffers.

This slice proves mesh data can follow the same GUID -> cooked payload -> RHI resource -> RenderGraph draw path as shaders and textures. `RHIStaticMeshResource` maps upload-memory staging buffers, records a one-shot `CopyBuffer` command into GPU-only vertex/index buffers, emits a transfer-to-vertex-input buffer barrier, waits for completion, and releases staging buffers. `StaticMeshPass` declares the static mesh vertex layout in pipeline state, binds cooked or ECS-submitted vertex/index buffers through `RenderCommandList`, and records indexed draws. RenderGraph pass recording still never parses source mesh data, queries the asset database, or performs buffer upload work.

Cooked static mesh payload version 4 adds production-facing metadata and a stable first static vertex layout while preserving the same setup-time loading boundary:

- The fixed 80-byte header stores vertex/index counts, payload sizes, bounds min/max, submesh count, and submesh payload size.
- Vertex data uses the current fixed static mesh layout: position, normal, tangent, UV0, color0.
- Index data remains `UInt32`.
- The submesh table stores compact 16-byte entries: `FirstIndex`, `IndexCount`, `VertexOffset`, and `MaterialSlot`.
- `MeshAssetCooker` validates submesh ranges when reading cooked data and forces recook for older cooked mesh versions.
- `.armesh` can declare explicit `submesh`/`s` entries, and the OBJ importer creates submesh ranges from `usemtl` boundaries with material slots assigned by encounter order.
- `RHIStaticMeshResource` exposes bounds and submesh spans after setup, so scene extraction can emit submesh-aware draw commands without reparsing source assets.
- `RHIStaticMeshResource.CreateDrawCommands` expands a prepared mesh into one draw command per selected submesh using caller-owned spans and cooked material-slot offsets. `CreateDrawCommandsWithMaterialOverride` uses the caller's exact material id for every selected submesh, which is the scene-renderer path for generated glTF primitive material bindings. This keeps variable submesh expansion at setup/extraction boundaries instead of inside RenderGraph command recording.

The first real mesh source asset is:

- Source: `com.arisen.generic-renderpipeline/Assets/Meshes/TexturedQuad.obj`
- Stable asset GUID: `f1701283-e63a-4748-9a86-1583d6e774a9`
- Source importer: `ObjMeshImporter`
- Cooked variant: `staticmesh.uint32`

The initial OBJ importer supports positions, UV0, `f v/vt/vn` style face tokens, negative OBJ indices, vertex deduplication, fan triangulation, and `usemtl` boundaries for submesh/material-slot metadata. The first glTF static mesh importer scope supports `.gltf` JSON sources with external or base64 data-URI buffers plus `.glb` binary containers with embedded BIN chunks, triangle primitives, POSITION plus optional NORMAL/TANGENT/TEXCOORD_0/COLOR_0 attributes, unsigned-byte/unsigned-short/unsigned-int indices, synthesized indices for non-indexed triangle streams, selected-scene node traversal, matrix/TRS node transforms baked into cooked static vertices, compact material slots from primitive material indices, and external buffer write-time checks for `.gltf` recook decisions. Missing tangents are stored as default `(1,0,0,1)` tangent data, OBJ uses generated default tangents for now, and unsupported extra glTF channels such as `TEXCOORD_1` or `COLOR_1` fail with explicit diagnostics instead of being silently dropped. Importer diagnostics are source-path aware: OBJ failures include malformed line/token context, glTF missing-property failures include array/property paths such as `accessors[0].bufferView`, missing external buffers include the originating `buffers[n].uri` context, and invalid node graphs report node indices or transform context. External `.bin` buffer files are dependency-only assets: they may have stable `.meta` files and editor/runtime registry rows, but no generated user-facing `AssetRef` constants. Generated glTF/GLB scene emission maps each primitive to a `MeshRenderer` submesh range with an exact material reference, while the generated mesh child source strips node transforms to avoid double-applying transforms at scene load. Full production model roots, skins, animation, morph targets, and broader PBR material/texture graph import remain later scopes. The render path now combines frame-snapshot camera view/projection data with entity-driven `MeshDrawCommand.LocalToWorld` transforms, submesh-aware `FirstIndex`/`VertexOffset` draw ranges, and the runtime smoke scene creates two overlapping multi-submesh mesh instances to exercise depth testing and setup-time submesh expansion. General renderable scene components remain future production hardening.

The first material binding slice lifts the smoke texture convention out of the pass:

- `MaterialAsset` describes a shader reference, named Texture2D references, typed material properties, and a small authored render-state block.
- `RHIMaterialResource` resolves material Texture2D refs during setup, owns the uploaded texture resources, and caches bindless image/sampler constants, typed material property values, and render-state intent.
- `StaticMeshPass` consumes the prepared material and mesh resources, builds setup-time material pipeline batches, then records only cached pipelines, bindless constants, typed material constants, MVP constants, vertex/index buffers, and draw commands.
- `IRenderMaterialLibrary` is the setup-time registry between authored material GUIDs and compact render `MaterialID` values.
- ECS mesh draw commands carry a compact `MaterialID`; `StaticMeshPass` resolves that id against setup-time prepared material slots and falls back to the package smoke material when a slot is missing.

The first authored material source is:

- Source: `com.arisen.generic-renderpipeline/Assets/Materials/SmokeMaterial.arismaterial`
- Stable material GUID: `a9ad3898-1805-428d-a26b-3a4859e5f13c`
- Source importer: `ArisenMaterialImporter`
- Cooked variant: `material.runtime`

`MaterialAssetCooker` converts the authored YAML material into a deterministic cooked binary payload and registers it in the cooked asset manifest. Runtime setup loads that cooked payload by GUID into the existing `MaterialAsset` model, then `RHIMaterialResource` prepares shader/texture GPU resources and typed material parameters from the cooked material data. The generic render pipeline now consumes BuildTool-generated package asset constants from `GenericRenderPipelineAssetRefs` instead of keeping GUID literals in the frame setup class.

The first production-facing standard material contract is now the active generic render-pipeline default material:

- Shader source: `com.arisen.generic-renderpipeline/Assets/Shaders/StandardLit.shader`
- Stable shader GUID: `72b6d255-0f54-46e5-9d05-8e0486d4f875`
- Material source: `com.arisen.generic-renderpipeline/Assets/Materials/StandardLitMaterial.arismaterial`
- Stable material GUID: `4ac21c64-e984-4ed0-9e21-93878de5249e`
- Default normal texture: `com.arisen.generic-renderpipeline/Assets/Textures/DefaultNormal.ppm`
- Stable texture GUID: `ead642b0-cde6-4ae0-8bdc-03472fb3d5aa`

`StandardLit.shader` uses a ShaderLab `MaterialContract` requiring `BaseColor`, `Normal`, `MetallicFactor`, `RoughnessFactor`, `BaseColorFactor`, and `EmissiveFactor`, with explicit `USE_NORMAL_MAP`, `ALPHA_TEST`, and `USE_TRIPLANAR` compile-time keyword variants. `StandardLitMaterial` selects `USE_NORMAL_MAP`, binds the smoke checker as base color, binds the flat default normal texture in linear color space, provides metallic/roughness/base-color/emissive factors plus deterministic occlusion-strength/alpha-cutoff defaults, and explicitly sets `RenderState.CullMode: None` so the current faceted fallback remains fully visible while front-face policy is still being hardened. Package-game showcase materials select `USE_TRIPLANAR` to project a CC0 marble base-color texture from world position while retaining the required flat normal binding. `StaticMeshPass` resolves optional emissive, metallic-roughness, and occlusion bindings plus their scalar factors during setup and writes compact presence flags, bindless indices, and PBR parameters into each draw's object-buffer record so the already packed push constants do not grow. StandardLit multiplies roughness from packed green and metallic from packed blue, reads occlusion from red, applies occlusion only to indirect/ambient lighting, and clips the explicit `ALPHA_TEST` variant against authored `AlphaCutoff`. `GenericRenderPipelinePackage` registers `StandardLitMaterial` as the default material.

The first generated typed asset-reference slice adds `AssetRef<T>` in `com.arisen.core` and marker types for authored source assets:

- `AssetRef<MaterialSourceAsset>`
- `AssetRef<MeshSourceAsset>`
- `AssetRef<ModelSourceAsset>`
- `AssetRef<Texture2DSourceAsset>`
- `AssetRef<ShaderSourceAsset>`
- `AssetRef<SceneSourceAsset>`

Generated refs preserve the old `Guid` constants for compatibility and add typed fields when the package can reference `com.arisen.core`, for example:

- `GenericRenderPipelineAssetRefs.SmokeMaterialGuid`
- `GenericRenderPipelineAssetRefs.SmokeMaterialRef`
- `GenericRenderPipelineAssetRefs.SmokeMaterial.Ref`
- `GenericRenderPipelineAssetRefs.TexturedQuadMesh.Ref`

Runtime loading still resolves by `Guid`; the typed wrapper is an authoring/codegen boundary that makes user code and package setup code harder to mix up. `IRenderMaterialLibrary` accepts `AssetRef<MaterialSourceAsset>` for material registration while internally storing compact material ids and GUID-backed resource state.

The current package-owned visual sample mesh is:

- Source: `com.arisen.generic-renderpipeline/Assets/Meshes/FacetedCrystal.obj`
- Stable asset GUID: `9f57d9cc-2db6-4c85-ae7b-544338806e2c`
- Source importer: `ObjMeshImporter`
- Cooked variant: `staticmesh.uint32`

`FacetedCrystal.obj` remains a deterministic low-poly fallback owned by the generic render pipeline package. The default authored application scene now uses `LanternShowcaseScene.arisenscene`, which frames package-owned generated children from the CC0 Khronos Lantern GLB model root and selects the package-authored Blue Hour lat-long environment. `Assets/Models/Lantern/Lantern.arismodel` is the stable `ModelSourceAsset` identity, while `Assets/Generated/Lantern` contains deterministic generated scene, mesh, material, and texture children derived from that root. The previous Utah teapot, pedestal, and ground-plane scene remains package-owned secondary/fallback content; its third-party notices record three.js generation provenance and the ambientCG Marble 021 CC0 texture source.

The first model identity slice adds `ModelSourceAsset` for production model import roots. `.arismodel` / `.model` source assets index as `AssetType: Model` and generate typed refs, but raw `.gltf` / `.glb` sources intentionally remain `AssetType: Mesh` in the current static mesh cooker path. The intended production importer shape is:

- `ModelSourceAsset`: stable authoring/import root and reimport settings.
- generated `SceneSourceAsset`: optional package-owned placement hierarchy for multi-node model scenes.
- generated `MeshSourceAsset`, `MaterialSourceAsset`, and `Texture2DSourceAsset` children: deterministic child assets derived from glTF meshes, materials, and images.

The first generated-child identity contract is now implemented in `com.arisen.core`:

- `GeneratedAssetIdentity.CreateChildGuid` derives stable, package-aware child GUIDs from `(sourceGuid, packageId, childKind, childKey)`.
- `GeneratedAssetIdentity.CreateChildMetadata` produces `.meta` metadata with `Generated` provenance for generated scene, mesh, material, and texture assets.
- `ModelSourceAssetLoader` is the first `.arismodel` source-schema boundary. It parses the stable model root, resolves the source glTF/GLB path relative to the model asset, resolves package/workspace `Assets` output roots, preserves selected scene index, unit scale, root transform defaults, material shader target, and texture-emission settings, then feeds the model root GUID into the glTF planner so generated child identity is derived from the production model root rather than the raw `.glb` file.
- `GltfModelImportPlanner` is the first planning boundary for model import. It reads glTF/GLB JSON, plans deterministic generated scene/mesh/material child identities plus image-backed `Texture2D` child identities for supported material bindings, and extracts base color, normal, emissive, packed metallic-roughness, and occlusion texture refs. Material planning also preserves factors, occlusion strength, `KHR_materials_emissive_strength`, glTF sampler settings, `KHR_texture_transform`, alpha mode, and alpha cutoff. Nonzero UV sets are preserved but warned because the current mesh/shader path only consumes `TEXCOORD_0`; unsupported skins, animations, morph targets, unknown alpha modes, and `BLEND` remain explicit diagnostics.
- `GltfModelImportEmitter` is the first generated-child emission boundary. It writes package-owned generated `.arisenscene`, single-mesh `.gltf` / `.glb`, and `.arismaterial` files with deterministic `.meta` sidecars, and can copy supported external `.ppm`, `.png`, `.jpg`, and `.jpeg` image references into generated `Texture2D` child assets with generated provenance. It also extracts embedded PNG/JPEG image payloads from glTF data URIs and `bufferView + mimeType` sources, including GLB BIN chunks. Generated scenes map glTF primitives to single-submesh mesh renderer entities and bind each primitive's exact generated material GUID; generated mesh child sources keep the primitive/submesh data but remove scene/node transforms. Materials bind base-color, normal, emissive, metallic-roughness, and occlusion refs plus their binding-local sampler/transform metadata. Base-color and emissive variants are sRGB; normal, metallic-roughness, and occlusion variants are linear. Shared glTF images emit one physical generated child while retaining every material binding and variant. An emitted normal binding selects `USE_NORMAL_MAP`; `alphaMode: MASK` selects `ALPHA_TEST` and emits `AlphaCutoff`; `alphaMode: BLEND` emits straight-alpha `SrcAlpha` / `OneMinusSrcAlpha` render state for the transparent queue. Keyword choices participate in normalized cooked shader identity, while blend state participates in material/pipeline identity.
- `ModelSourceReimporter` is the explicit editor reimport boundary. It validates that a model output root stays under the same package/workspace `Assets` root as the selected `.arismodel`, rejects any `.arisen` output path, scans generated metadata before writing, refuses foreign generated metadata from another source GUID, and reports stale same-source generated children as orphans instead of deleting them. It emits through `GltfModelImportEmitter`, using the final `OutputRoot` segment as the generated output name so `Assets/Generated/Lantern` writes directly into that folder.
- The editor mesh inspector uses the planner for raw `.gltf` / `.glb` mesh sources, while the editor model inspector uses `ModelSourceAssetLoader` plus the planner for `.arismodel` roots. Both show import diagnostics before generation: planned scene/mesh/material/texture child counts, texture-reference counts, generated child GUIDs/provenance, material factor and alpha previews, sampler/transform-aware texture refs, and unsupported feature warnings.
- The editor model inspector also exposes an explicit `Reimport` action. A successful reimport refreshes the runtime asset index for emitted children when the concrete runtime database is available, invalidates cooked artifacts for the model root and planned generated children, publishes coarse asset-change events, and leaves watcher-originated file registration to the existing `AssetImportScheduler`.
- `ModelReimportValidationFixture_ReimportsIndexesAndLoadsGeneratedScene` is the bounded integration check for this workflow. It creates a temporary package-owned `.arismodel` plus external-buffer glTF source, explicitly reimports one scene/mesh/material and three physical textures, indexes the emitted `.meta` sidecars, resolves all four generated material bindings through typed `Texture2D` lookups, and loads the generated scene into a real `EntityManager`. A second reimport must preserve child GUIDs and sidecar provenance exactly; every emitted source must remain under the selected `Assets/Generated` root and outside `.arisen`; foreign-source generated metadata must block reimport before emission.

Generated glTF/GLB child source emission stays in the model planning/import boundary rather than hidden inside the static mesh cooker. The cooker remains responsible for a single mesh source and its compact submesh/material-slot table.

The first scene source asset slice adds `.arisenscene` / `.scene` files as `Scene` assets:

- `SceneSourceAsset` generated refs let package setup load scenes without raw GUID literals.
- `SceneAssetLoader` parses the first YAML scene source format during setup/package startup and validates referenced mesh/material GUIDs through `IAssetDatabase`.
- `SceneAssetLoader.InspectScene` exposes the same parser and reference validation as a read-only tooling boundary for the editor inspector. It reports scene/entity/component/reference data and diagnostics without spawning ECS entities or mutating source files.
- Editor Hierarchy mirrors the current `IEditorSceneDocumentService` inspection, which is keyed by the same scene GUID/package/source identity published by `IRuntimeSceneService`. Selecting another asset does not replace Hierarchy with an unrelated scene; double-click scene opening goes through the document service and the frame-boundary runtime activation request.
- Source-scene authoring is staged. `SceneAssetLoader.UpdateEntityTransformSource` edits an in-memory YAML source string, and undoable editor commands replace the document's working source without writing the asset. Each revision is validated and queued as an immutable `SceneSourceSnapshot`; `RuntimeSceneService` loads it into a candidate `EntityManager` at `EngineKernel.OnFrameEnd` and only replaces the active world on success.
- Explicit Save is the only editor source write. It validates the current snapshot, rejects generated or non-`Assets` paths, compares the current disk bytes with the document's saved baseline to prevent overwriting external changes, and atomically replaces UTF-8 source through a same-directory temporary file while preserving BOM policy. Undo restores the exact prior working source, so returning to the saved revision clears dirty state without depending on YAML formatting equivalence.
- Scene switching and editor shutdown use Save/Discard/Cancel resolution when the document is dirty. External watcher changes reload a clean document through the same frame boundary; concurrent disk and editor changes mark a conflict and block Save. The retired `.arisen` serializer, editor scene singleton, last-opened-scene setting, and direct live-ECS hierarchy commands are not part of the scene asset path.
- The loader spawns `NameComponent`, `TransformComponent`, `CameraComponent`, and `MeshRendererComponent` data into the active ECS world. Render extraction and RenderGraph recording continue to consume ECS snapshots and prepared resources, not scene source files.
- `manifest.json` selects the startup scene by stable GUID and package id. `ProjectSceneBootstrapSubsystem` resolves that selection during `PostInit` and loads it through `IRuntimeSceneService`; product code no longer names the Lantern scene or creates a hidden code-only fallback world.
- `RuntimeSceneService` parses into a temporary `EntityManager` and activates it through `SceneSubsystem` only after the complete scene validates. A failed load leaves the previous active world untouched. The service publishes the active scene identity for tooling, and editor Hierarchy automatically inspects that same source scene instead of synthesizing a separate legacy `SampleScene`.
- A future scene cooker should move the source parsing step behind a cooked scene payload once the format broadens beyond this first authoring slice.

The first render-pipeline settings asset adds `.arisrenderpipeline` files as `RenderPipelineSettings` assets:

- `RenderPipelineSettingsSourceAsset` generated refs preserve typed package-owned identity.
- `GenericRenderPipelineSettingsLoader` parses and validates version 1 Generic RP YAML during provider activation. It rejects unsupported providers, non-power-of-two shadow sizes outside `256..8192`, non-finite/out-of-range bias and strength values, and PCF radii outside `0..3`.
- `manifest.json` stores the selected settings GUID and owning provider package under `RenderPipeline`. `RenderSubsystem` activates the matching `IRenderPipelineProvider`; the Generic RP package no longer installs an implicit in-memory default asset from `OnLoad`.
- Project Settings lists indexed settings assets from base workspace packages and atomically applies startup-scene and render-pipeline references through the comment/BOM/newline-preserving manifest patcher. Selection changes take effect on the next launch.
- `DefaultGenericRP.arisrenderpipeline` is the package-owned baseline. Its values flow through setup into shadow target allocation and compact draw constants; render-pass recording never parses the YAML or consults project settings.

The first typed material property slices add `ScalarProperties` and `Vector4Properties` to `.arismaterial` and `material.runtime`. The smoke material currently authors scalar `MetallicFactor`/`RoughnessFactor` defaults and a Vector4 `BaseColorFactor`. `RHIMaterialResource` caches both scalar and Vector4 properties during setup; `StaticMeshPass` currently consumes `BaseColorFactor` and pushes it alongside the bindless texture/sampler indices. This keeps parameter lookup and defaulting in setup code; RenderGraph command recording only consumes compact unmanaged constants.

The shared PBR material convention adds `MetallicRoughness` and `Occlusion` Texture2D slots plus `OcclusionStrength` and `AlphaCutoff` scalar properties. Packed data follows the glTF channel convention: occlusion is red, roughness is green, and metallic is blue. Missing occlusion strength defaults to `1.0`; missing alpha cutoff defaults to `0.5`. These names and values live in `MaterialTextureSlots`, `MaterialPropertySlots`, `MaterialPbrTextureChannels`, and `MaterialPbrDefaults` so import, setup, tests, and shader preparation share one contract.

Each `Texture2DRefs` entry may also author binding-local `Sampler` metadata (`MinFilter`, `MagFilter`, `MipmapMode`, `WrapU`, `WrapV`) and `Transform` metadata (`Offset`, `Scale`, `Rotation`, `TexCoord`). The same image asset may therefore be reused by bindings with different sampling intent without making sampler state part of pixel cooking. Omitted metadata resolves to linear min/mag filtering, nearest mip selection, repeat wrapping, zero offset, unit scale, zero rotation, and UV set zero. `MaterialAssetLoader` validates finite transform data, version 6 `material.runtime` payloads preserve the resolved values, `RHITexture2DResource` creates the resolved sampler during setup, and `RHIMaterialResource` caches the transform beside the bindless binding for later shader preparation. Texture mip generation remains a separate texture-cooking concern.

The first material render-state slice adds a `RenderState` section to `.arismaterial`; active shader keywords entered the cooked format in version 5, and version 6 adds sampler/texture-transform bindings. The initial render-state fields are culling mode, front-face winding, and color-blend intent. Older cooked material payloads still load with deterministic compatibility defaults where possible, while the current cooker forces recook when the version changes. `StaticMeshPass` builds graphics pipelines from prepared material state and includes shader GUID, shader dependency stamp, shader variant identity, color/depth formats, and render state in its pipeline reuse key. Material slots in one pass may use different compatible states or keyword variants because setup-time batching buckets draw commands by material pipeline key before command recording.

The first typed generated material-reference slice extends package `*AssetRefs.g.cs` output with nested material metadata:

- `GenericRenderPipelineAssetRefs.SmokeMaterial.Guid`
- `GenericRenderPipelineAssetRefs.SmokeMaterial.Texture2DSlots.BaseColor`
- `GenericRenderPipelineAssetRefs.SmokeMaterial.ScalarProperties.MetallicFactor`
- `GenericRenderPipelineAssetRefs.SmokeMaterial.ScalarProperties.RoughnessFactor`
- `GenericRenderPipelineAssetRefs.SmokeMaterial.Vector4Properties.BaseColorFactor`

These generated names are still disposable build outputs. The authored `.arismaterial` source remains the source of truth for material values, and package-local shader-source `@arisen.material.*` annotations can contribute required material slot/property names through the material's `Shader.Guid`. Runtime pass setup may consume these constants when it owns the authored material contract; shared package-facing conventions such as `MaterialTextureSlots` and `MaterialPropertySlots` remain available for reusable material schemas.

The first material shader-contract slices add setup-time material binding validation to authored `.arismaterial` files. The preferred explicit form is `Shader.Contract`, where the shader reference declares required `Texture2DRefs`, `ScalarProperties`, and `Vector4Properties` by name. The compatibility material-level `ShaderContract` remains supported as an extension path. Shader source can now also declare the same requirements with lightweight annotations such as `@arisen.material.texture2d BaseColor` and `@arisen.material.vector4 BaseColorFactor`. `MaterialAssetLoader` validates the union of shader-source annotations, `Shader.Contract`, and material-level `ShaderContract`. It rejects duplicate or empty material names, duplicate or empty contract names, and missing material bindings before a material reaches runtime resource setup. This is an authoring/cook/setup boundary check; RenderGraph command recording still consumes only already-prepared bindless constants and unmanaged material values.

The first material-editor slice makes existing authored bindings editable from the Inspector:

- Texture2D rows use a dropdown populated from indexed `Texture2D` assets with supported `.ppm`, `.png`, `.jpg`, or `.jpeg` sources. Reassignment changes only the nested texture GUID/name/source format; the binding's slot, color-space/cooked variant, sampler, and UV-transform metadata remain intact.
- Scalar rows use the numeric property editor, and Vector4 rows use a four-component numeric editor. Every change executes through `ICommandManager`, so execute, undo, and redo write the same deterministic source value.
- `MaterialSourceAssetEditor` mutates the YAML representation tree by binding name and writes atomically. It refuses missing/duplicate names before writing and preserves unrelated or forward-extension mappings instead of rebuilding the document from the runtime material model. `MaterialAssetLoader` ignores unknown extension mappings while continuing to validate every consumed structure and shader-contract requirement.
- A successful command invalidates the material's cooked payload and publishes an `AssetChangeKind.Changed` event. Render-side reload remains setup-owned through the existing asset-change/reload queue; Inspector editing does not parse or mutate material source during RenderGraph recording.
- Source editability is derived from the indexed `.meta` sidecar. Authored materials under workspace/package `Assets` roots are editable; any material carrying `Generated` provenance is read-only and must be changed through its source model reimport. The command rechecks this policy at execution time so stale UI cannot overwrite generated output.
- `MaterialAssetLoader.InspectSource` returns all missing Texture2D/scalar/Vector4 contract bindings as structured diagnostics while retaining the inspectable material values. The Inspector presents each missing binding explicitly. Runtime/cooking callers continue to use `LoadSource`, which rejects the same diagnostics before GPU setup.

Material cooking compares the cooked payload against the material source/meta files plus shader/material dependency stamps. This matters for shader-source annotations: editing a shader material-contract comment can force material recook and validation even if the `.arismaterial` file itself did not change.

`ArisenBuildTool` mirrors the same authoring direction for generated material refs. During project generation it scans a material's authored value sections and, when the referenced shader GUID is package-local, scans that shader source for `@arisen.material.*` annotations and ShaderLab `MaterialContract` blocks. The generated nested material constants therefore include shader-required names even when they are not repeated as hand-written contract metadata in the material YAML.

The first dependency invalidation slice is polling-based and setup-time only:

- `AssetDependencyTracker` combines source and `.meta` timestamps into an `AssetDependencyStamp`.
- Shader stamps include the shader source, shader `.meta`, and explicitly declared `ShaderAsset.Includes`.
- Material stamps include the material source, material `.meta`, shader dependency stamp, and referenced Texture2D source/meta stamps.
- `RHIMaterialResource`, `RHITexture2DResource`, and `RHIStaticMeshResource` cache dependency stamps when they prepare GPU resources.
- `GenericRenderPipeline.SetupGraph` checks those stamps before recording and disposes/recreates stale material or mesh resources outside RenderGraph pass command recording.
- `StaticMeshPass` includes the shader dependency stamp in its graphics pipeline cache key so shader source/include edits rebuild the pipeline before the next recorded frame.

The first evented invalidation slice connects editor file-watch/import notifications to runtime cooked asset invalidation:

- `AssetImporter` publishes created, changed, deleted, and renamed events after the editor SQLite registry has been updated.
- `AssetImportScheduler` sits between `FileSystemWatcher` and import/database mutation. It debounces duplicate watcher events, coalesces simple event bursts, retries locked files, and serializes import work so watcher callbacks stay lightweight.
- `.meta` file edits are treated as changes to their paired source asset, so importer settings can invalidate cooked artifacts through the same GUID path.
- `AssetDatabaseService` forwards those events to the runtime `IAssetDatabase` service and asks it to invalidate cooked artifacts for the changed GUID.
- `IAssetDatabase.AssetChanged` is the coarse-grained notification boundary. It is suitable for setup/resource managers to schedule reload work, but render pass recording must not subscribe and rebuild resources directly.
- `IAssetDatabase.InvalidateCookedAssets` releases currently loaded cooked handles for the GUID, removes matching cooked manifest records, and emits a `CookedInvalidated` event for diagnostics and subscribers. Cookers treat the missing registry record as authoritative invalidation and recook even if an older artifact file still exists with a newer timestamp.
- `RenderResourceReloadQueue` is the render-side consumer boundary for those events. It coalesces dirty GUIDs from `IAssetDatabase.AssetChanged`, then render pipelines drain the queue during setup before RenderGraph pass recording.
- `GenericRenderPipeline` currently invalidates prepared material resources when a changed GUID matches a material, material shader, or material Texture2D dependency. It also invalidates the fallback `FacetedCrystal.obj` mesh when its GUID changes. Invalidated GPU resources are detached during setup, recreated before pass recording, and disposed through `DeferredRenderResourceDisposalQueue` only after the previous submitted render ticket has completed. Existing dependency stamp polling remains as a fallback for source dependencies that are not represented by direct asset GUID edges yet; composite stamps include each dependency's write time and file length instead of collapsing the graph to only its newest timestamp.
- Explicit model reimport refreshes emitted child records, then calls `ModelSourceReimporter.InvalidateCookedOutputs`. That boundary invalidates and publishes changes for the stable model root GUID and every deterministic generated child GUID, allowing generated material and texture changes to reach both cooked payloads and live GPU resource replacement.

Material setup still keeps source parsing out of RenderGraph pass recording. `MaterialAssetCooker` may inspect `.arismaterial` and shader source annotations during setup to validate contracts and decide whether the cooked payload is stale; once resources are prepared, frame recording consumes only cooked material data, cached bindless constants, and unmanaged property values. Later material-authoring work can add material creation, insertion/removal of contract bindings, dedicated color controls, and sampler/transform editing on top of this first existing-property editor.
