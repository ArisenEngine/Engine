# Architecture Spec: Asset Pipeline & Resource Management

**Status**: Draft / Active  
**Module**: `com.arisen.core` contract + `com.arisen.resources` provider

In a Data-Oriented engine, the way assets (Textures, Meshes, Audio, Scenes) are loaded from disk into memory is paramount. Source parsing and unbounded allocation must stay outside frame-critical work, while runtime payload validation and resource setup remain explicit and measurable.

Arisen Engine is converging on a strict **Cooked Binary Asset Pipeline**. Shader, texture, environment, material, mesh, scene, world-descriptor, terrain, and vegetation species/biome/cluster/instance-page formats now have implemented cooked slices. The package-neutral closed-set cooking contract, real package-owned cooker providers, generated Production cook host, relocatable incremental deployment, cooked-default runtime selection, explicit source diagnostics, and copied-output validation are in place.

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
- Editor rename/move handling preserves asset identity as a registry/sidecar transaction. The GUID registered at the old path is authoritative when present. A destination sidecar candidate is published first, then one SQLite transaction validates destination ownership and moves the existing registration. A failed database commit restores the prior destination sidecar and leaves the old registration intact; only a committed and revalidated destination publishes `Renamed`. Because generated model child GUIDs derive from the stable root GUID rather than its path, renaming or moving an `.arismodel` root does not change generated scene, mesh, material, or texture identity.
- The Editor owns one non-pooled SQLite connection for its source registry. Every query and mutation is serialized through the connection gate, and multi-statement registration/rename changes use SQL transactions. Callers never concurrently operate the shared connection or retain a reader outside that gate.
- File watcher handlers are attached before `EnableRaisingEvents`. Events received while the initial scan is running are journaled and replayed through `AssetImportScheduler` before the importer enters `Running`, so the scan/event boundary cannot lose a source change.
- Import scheduling has explicit `Accepting -> StopRequested -> Completed -> Disposed` ownership. Shutdown stops new work, cancels queued work, waits for the active worker to terminate, and only then disposes its cancellation/signal primitives. Debounce and retry delays are file-event coalescing and transient-I/O policy, not lifecycle correctness mechanisms; deterministic idle/completion tasks expose actual state transitions. Exhausted and non-retryable failures remain queryable and are reported to observers instead of being logged and discarded.
- `ArisenBuildTool` scans package `Assets/**/*.meta` sidecars during managed project generation and emits disposable generated asset constants into `.arisen/Projects/{Profile}/{Package}/Generated/*AssetRefs.g.cs` for real source assets, skipping metadata files themselves. Packages that can reference `com.arisen.core` get typed `AssetRef<T>` constants alongside legacy `Guid` constants; packages without that dependency still get plain GUID constants. For authored `.arismaterial` assets, the same generated file also exposes nested texture slot and typed property-name constants derived from the material source.

---

## 3. The Cooking Pipeline And Validated Loading

Arisen **does not** load raw `.png`, `.fbx`, YAML scene, or other authoring files during an ordinary runtime launch. Parsing source content during the game loop is forbidden. Editor authoring and the build-stage cook host retain explicit source access; Development and test runtime launches default to cooked artifacts.

Instead, we use a **Cooking Pipeline**:
1. During the Build process (or Editor import), the engine reads the raw source asset and the `.meta` file's settings.
2. It compiles ("cooks") the data into an optimized, validated binary payload inside a database-owned staging transaction.
3. A successful transaction publishes the payload under `.arisen/Cache/CookedAssets/{Guid}/` and replaces the mutable cache manifest as one serialized generation change.

### Transactional Mutable-Cache Publication

First-party cookers never choose or overwrite a committed artifact path. They call `IAssetDatabase.BeginCookedArtifactWrite(Guid, variant, extension)`, write only to the returned `.staging/{transaction-id}/artifact{extension}` path, and call `Commit(assetType)` after the payload is closed and complete. Disposing an uncommitted write discards its private staging directory.

`AssetDatabase` owns one cooked-registry writer gate. A changed commit moves the staged payload to an immutable generation-qualified path of the form `{Guid}/{variant}.g{registry-generation}.{transaction-id}{extension}`. It then builds a complete `FrozenDictionary` candidate, writes a temporary `AssetManifest.json`, atomically replaces the prior manifest, supersedes the current loaded-handle key, and publishes the candidate snapshot with `Volatile.Write`. A failure before manifest replacement leaves the prior manifest, snapshot, loaded mapping, and committed payload untouched and restores the candidate to its staging transaction for deterministic disposal or retry.

Byte-identical recooks reuse the current immutable artifact and do not advance the registry generation. A changed successful recook leaves already-acquired handles on their immutable old bytes while new acquisitions resolve the new artifact. Once no registry key references the superseded cache file, post-commit cleanup moves it through the cache-owned `.remove` quarantine and deletes it. Failure to clean physical garbage is reported but cannot make the unreferenced file visible through the registry. Explicit invalidation and multi-artifact removal use the same manifest-before-snapshot publication rule.

Runtime lookup captures one immutable registry snapshot, performs file I/O outside the loaded-handle lock, and verifies that the same snapshot is still current before publishing loaded bytes. If publication changes while the read is in flight, lookup discards that attempt and resolves the new generation. Runtime readers therefore observe either the complete old registry or the complete new registry, never a dictionary being mutated in place or a partially registered artifact.

### Validated Cooked Loading

The current `IAssetDatabase` reads a cooked artifact into managed memory and returns a generation-checked `CookedAssetHandle`. Loaded bytes are immutable for that handle's lifetime. Publishing a changed artifact removes only the old slot's current-key mapping: existing owners may finish reading the retained old generation, while every subsequent acquisition is routed to a fresh slot containing the replacement bytes. Releasing an old handle cannot remove the replacement mapping. Each format validates its own magic, version, counts, offsets, sizes, and semantic constraints before setup consumes the payload.

Cooked-handle slot acquisition/release is synchronized for background streaming. Concurrent requests for the same `(GUID, variant)` converge on one slot and increment its reference count; bytes are read outside the slot lock and a racing second read is discarded after the winner is observed. `IRuntimeAssetResidencyService` adds owner-level sharing above those handles so persistent scenes and additive cells retain one CPU payload until the final owner releases it.

Cooked bytes are not assumed to match CLR or native object layout. GPU resources still require explicit backend-owned upload/setup, and cooked scenes decode a sectioned payload into an immutable staging representation before deterministic ECS instantiation. Memory mapping and direct typed views may be introduced later for formats whose schema, alignment, lifetime, and platform portability make that safe; they are not a blanket current guarantee.

### Source Access Policy

`IAssetDatabase.SourceAccessMode` separates source indexing from permission to select source payloads:

- `Disabled`: ordinary Development/test runtime and all read-only deployed runtime selection; world, scene, pipeline, mesh, material, texture, shader, and environment loaders use cooked variants;
- `EditorAuthoring`: compile-owned `ARISEN_ENGINE_EDITOR` behavior for working documents and validated source snapshots;
- `Diagnostic`: explicit non-Editor runtime opt-in through `--diagnostic-source-assets`;
- `RuntimeAssetCook`: package-only build-stage access used by `RuntimeAssetCookHost`.

Workspace mode may still index source records while access is `Disabled`, because descriptors and explicit cooking use that registry. `TryGetAsset` and all high-level runtime selectors nevertheless fail closed to cooked data. The diagnostic CLI option is rejected for Editor, Production, and every deployed launch; it is intended only for bounded Development or test diagnosis, never product behavior.

### Relocatable Runtime Catalog

`ArisenEngine.Core.Assets.RuntimeAssetCatalog` defines the deployable `runtime-assets.json` contract independently of the mutable workspace cache registry:

- schema version and target profile are explicit;
- named roots such as `startupScene`, `startupWorld`, and `renderPipeline` use typed package/GUID/variant identities;
- each artifact stores its owning package, asset type, variant, content-root-relative path, byte size, lowercase SHA-256, payload format version, and typed required/optional dependencies;
- roots, artifacts, and dependencies are sorted before serialization, and the writer emits compact strict JSON with one trailing LF;
- creation and parsing reject empty identities, unsupported/unknown JSON fields, duplicate root names, duplicate `(GUID, variant)` identities, duplicate dependency identities, and output paths that collide on case-insensitive filesystems;
- artifact paths must use forward slashes and cannot be rooted, contain drive/URI prefixes, backslashes, empty segments, `.`/`..`, or Windows-ambiguous trailing dots/spaces;
- every root and dependency must resolve to a catalog artifact with the same package and asset type;
- deployment validation resolves beneath a supplied content root, rejects symbolic-link/reparse traversal, then verifies every artifact's existence, exact byte size, and SHA-256.

The catalog contains no timestamps, source paths, cache paths, or machine-rooted values. Moving the complete content root therefore does not change catalog bytes or asset lookup. The generated Production cook host writes the same canonical catalog to both `.arisen/Intermediate/Cook/{Profile}/{Configuration}/runtime-assets.json` and the launch output described below.

`RuntimeAssetClosurePlanner` now owns the package-neutral reachability step between cooker metadata and a catalog. It accepts named roots plus candidate `RuntimeAssetCatalogArtifact` records, traverses dependencies in canonical identity order with an explicit iterative stack, and passes only reachable artifacts to the strict catalog constructor. Shared dependencies are included once, unrelated cooked candidates are omitted, and cyclic references close normally without recursion or infinite traversal. Missing transitive metadata and package/type mismatches fail with the complete named-root dependency chain. The planner never reads YAML, shader, material, model, or scene source formats.

`IRuntimeAssetCooker` is the package-owned provider contract. A provider declares the asset types it understands and receives profile/configuration/runtime context plus a typed package/GUID/type/variant request. It returns exactly one artifact record with no pre-resolved catalog dependencies, one fully qualified machine-local source file for later deployment, and typed dependency cook requests. Source parsing and format-specific default-variant selection therefore stay in the owning package. Current registrations are:

- `com.arisen.resources`: `Scene`, including typed mesh, material, and environment dependencies extracted from the validated staged scene; and `World`, including every persistent/cell scene referenced by the validated world descriptor;
- `com.arisen.rendering`: `Mesh`, `Material`, `Texture2D`, `ShaderSource`, and `EnvironmentTexture`;
- `com.arisen.generic-renderpipeline`: `RenderPipelineSettings`, including the default material, always-prepared fallback mesh, and code-owned directional-shadow, environment-sky, outdoor-atmosphere, and tonemap shader-stage variants;
- `com.arisen.vegetation`: `VegetationSpecies`, including each LOD's exact mesh/material dependencies; `VegetationBiome`, including every referenced vegetation species; generated `VegetationCluster`, including its exact biome, canonical species union, and instance pages; and generated `VegetationInstancePage`, including its canonical species dependencies.

Materials register exact stage recipes before requesting shader artifacts. `RuntimeShaderCookRecipeRegistry` keys recipes by shader GUID plus cooked variant and rejects two owners that hide different stage, variant, define, include, or keyword inputs behind the same identity. Equivalent registrations are idempotent. Environment payload cooking requests its IBL artifact as a dependency, and forced builds invalidate the requested mutable cache record before recooking.

`com.arisen.core` provides `IRuntimeAssetCookerRegistry` as a coarse setup-time service. The registry rejects empty or duplicate type ownership before mutating registration state. `RuntimeAssetCookCoordinator` consumes named root requests through that registry in deterministic identity order, resolves empty variants from provider outputs, deduplicates shared requests and artifacts, closes transitive and cyclic dependency graphs, verifies each produced file's exact size and SHA-256, converts dependency requests to resolved catalog identities, and delegates final reachability/canonicalization to `RuntimeAssetClosurePlanner`. The result keeps absolute source paths in an in-memory deployment list only; serialized `runtime-assets.json` receives catalog-relative paths.

`RuntimeAssetCookHost` is the package-aware build-stage host. Generated entry projects compile-reference `com.arisen.core` so their thin `Program.cs` can dispatch `--arisen-cook-runtime-assets` before normal engine boot; other package references remain runtime-loaded. The command resolves the selected profile's package graph, mounts package entries without entering subsystem phases, reads `StartupScene`, optional `StartupWorld`, and `RenderPipeline` as named roots, runs `RuntimeAssetCookCoordinator`, writes the intermediate catalog, and optionally deploys the result when `--output-root` is supplied. It does not create a runtime window, initialize an RHI device, or activate a scene.

Generated Production entry projects run that command from an `ArisenCookRuntimeAssets` `AfterTargets="Build"` target after package assemblies and native payloads exist, passing `$(TargetDir)` as the deployment output. The cook host reads `.arisen/Projects/{Profile}/manifest.source.resolved.json`, which remains a build-stage artifact and preserves source package roots even after the launch output has been finalized for deployment. `ArisenSkipAssetCook=true` suppresses the automatic target for diagnostic build isolation; Editor and Development entry hosts retain manual dispatch but do not cook automatically. The generated apphost executable is invoked directly so native resolution and writable log placement use the actual workspace output rather than the shared `dotnet` host directory.

After cooking succeeds, the same target invokes `ArisenBuildTool deploy-runtime-metadata`. The build tool revalidates the effective package graph, transactionally replaces `Packages/<package-id>/package.json` with source-independent runtime descriptors, emits a sanitized runtime `manifest.json`, rewrites `manifest.resolved.json` to `file://Packages/...` URLs, and marks `launch.config.json` as `Mode: Deployed`. A deployed player roots project/package metadata beside its executable and rejects `--workspace`; ordinary Production boot therefore has no metadata path back to package `Assets` or the development cache.

`RuntimeAssetDeployment` publishes one complete cook result as:

```text
.arisen/bin/{Profile}/{Configuration}/
|- manifest.resolved.json
|- launch.config.json
|- runtime-assets.json
|- Packages/{package-id}/package.json
`- Content/{catalog-relative-artifact-path}
```

Before touching the active deployment it requires exactly one cooked source mapping for every catalog artifact and validates each source size/SHA-256. A prior artifact is reusable only when target profile, GUID/variant identity, payload format version, size, SHA-256, and the existing deployed bytes all match. Reusable files are hard-linked into the sibling transaction stage; changed, corrupt, or unsupported-link cases copy from the verified cook output. The complete staged tree is revalidated through the new catalog. Commit renames the previous owned `Content` tree and catalog to a backup, installs the staged pair, and restores the previous pair if a filesystem operation fails. Replacing the complete owned tree still removes catalog-stale and untracked files deterministically while unchanged payloads preserve filesystem identity.

`ArisenBuildTool` remains source-format agnostic: it generates this host boundary and output-root argument but does not parse assets or reflection-load engine packages into its own load context. Focused tests cover coordinator closure/integrity, real provider dependencies and variant policy, package-only mount/unload, generated Production/manual host dispatch, incremental reuse and format-version invalidation, corrupt deployed-file repair, stale cleanup, tamper preservation, explicit source selection, read-only catalog mounting, deterministic runtime metadata, stale package-descriptor removal, and relocation. `validate_relocated_production.ps1` copies the complete output outside the workspace, verifies that no authoring/cache files or source-root metadata are present, boots the cooked scene, and requires graceful SHA-256 and missing-artifact failures after mutating the copy.

---

## 4. The Asset Database & Lifecycle

The `IAssetDatabase` (provided by a core Service) acts as the global librarian.

1. **Development Cache Registry**: During local cooking, the Asset Pipeline generates `.arisen/Cache/CookedAssets/AssetManifest.json`, which maps `Guid + Variant` to immutable, generation-qualified workspace artifacts and persists the current registry generation. The mutable part is the atomically replaced registry, not the bytes at a published path. This manifest may contain local paths/timestamps and is not deployable runtime metadata. Production cooking creates and deploys the separate validated `runtime-assets.json` above; ordinary Production boot mounts that catalog read-only and does not index workspace source or consume the development cache registry.
2. **Runtime Loading**: When a Guid is requested, the Database captures the current immutable cooked-registry snapshot. If the identity is not loaded, the current implementation reads the snapshot's file and hands back a generation-checked `CookedAssetHandle` only if that snapshot is still current.
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
- `Texture2DAssetCooker` for source-time `.ppm` parsing or PNG/JPEG image decode, optional complete packed mip-chain generation, cooked-v2 binary header/payload emission, cooked manifest registration, and `CookedAssetHandle` loading. Linear variants use channel-space box filtering; sRGB variants filter RGB in linear light and alpha linearly; normal-map variants decode, average, renormalize, and re-encode each generated mip. Mip filter semantics participate in the cooked variant identity.
- `RHITexture2DResource` for setup-time GPU upload through a staging buffer, one `CopyBufferToImage` region per packed mip, image layout transitions, full-chain image view/sampler creation, and bindless image/sampler registration.

This slice proves non-shader cooked asset data, runtime loading by GUID, conversion into a backend-owned RHI image, and visible shader sampling through the global bindless descriptor set. RenderGraph pass recording still never parses source texture data, queries the asset database, or performs upload work.

The first environment-texture asset path builds on Texture2D source identity without treating a panorama as an ordinary material binding:

- `.arienvironment` files index as `AssetType: EnvironmentTexture`, use importer `ArisenEnvironmentTextureImporter`, and generate `AssetRef<EnvironmentTextureSourceAsset>` helpers.
- Source version 1 contains `Version`, `Name`, a typed `SourceTexture` GUID/package reference, `Layout`, `SourceColorSpace`, `RuntimeFormat`, `RotationDegrees`, and `Intensity`. Source version 2 optionally adds `Outdoor`: `SkyMode`, sun/sky coupling and disc/glow response, horizon/zenith exponents, aerial start/distance/strength, height-fog enable/base/density/falloff, and `Scene` or `Fixed` exposure policy. Values are finite and bounded; a version-1 source or omitted profile resolves to panorama sky, disabled atmosphere, and scene exposure.
- The first supported source layout is a strict 2:1 `LatLong` image. The visible-sky runtime format is linear `R16G16B16A16SFloat` with one mip; image-based-lighting derivatives are stored in a separate cooked artifact so the authored panorama identity and direct sky upload remain stable.
- PNG/JPEG/PPM inputs decode through the shared Texture2D source decoder. `.hdr` files index as `Texture2D` through `HdrTextureImporter` and decode through `StbImageSharp.ImageResultFloat`, preserving source values before half-float conversion.
- `EnvironmentTextureAssetCooker` writes `latlong.r16g16b16a16sfloat.nomips` cooked format 2. Its fixed 144-byte little-endian header embeds the validated outdoor profile before the half-float pixel payload. The format-1 64-byte header remains readable with disabled-profile defaults, but source-enabled cooking treats it as stale even when its timestamp is newer. Pixel access validates and uses the version-specific payload offset. Dependency stamps include both the `.arienvironment` descriptor/meta and referenced Texture2D source/meta.
- `RHIEnvironmentTextureResource` consumes only the cooked payload during setup, uploads it through the shared `RHITexture2DAllocation`, and exposes prepared bindless image/sampler indices, rotation/intensity metadata, and the immutable outdoor profile. The lat-long sampler repeats U and clamps V.
- `EnvironmentLightingAssetCooker` consumes the cooked linear panorama and writes `ibl.latlong.rgba16f.v1` for the same environment GUID. The payload contains tightly packed half-float diffuse irradiance, GGX-prefiltered specular mips, and a split-sum BRDF integration LUT. Cooked-registry presence plus the combined descriptor/source dependency time controls cache reuse; invalidating the environment GUID invalidates both visible-sky and IBL variants.
- `RHIEnvironmentLightingResource` uploads all three payloads during setup and owns their bindless image/sampler registrations. The shared 2D uploader validates packed mip sizes, copies each mip subresource, configures the sampler's full LOD range, and leaves every mip shader-readable before frame recording.

The package-owned first asset is `com.arisen.packagegame/Assets/Environments/BlueHour.arienvironment`, backed by the package-authored `BlueHourPanorama.ppm` source. Its version-2 profile selects procedural outdoor visible sky, directional-light-coupled sun, aerial perspective, height fog, and scene exposure while retaining the panorama for diffuse irradiance, roughness-aware specular prefiltering, and the BRDF integration LUT. `LanternShowcaseScene.arisenscene` stores the environment GUID; scenes without a valid optional reference use the deterministic procedural fallback with atmosphere disabled.

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
- `GltfModelImportEmitter` is the first generated-child emission boundary. It writes package-owned generated `.arisenscene`, single-mesh `.gltf` / `.glb`, and `.arismaterial` files with deterministic `.meta` sidecars, and can copy supported external `.ppm`, `.png`, `.jpg`, and `.jpeg` image references into generated `Texture2D` child assets with generated provenance. It also extracts embedded PNG/JPEG image payloads from glTF data URIs and `bufferView + mimeType` sources, including GLB BIN chunks. Generated scenes map glTF primitives to single-submesh mesh renderer entities and bind each primitive's exact generated material GUID; each emitted entity GUID derives from the model source GUID plus `scene/node/primitive` child key, so reimport preserves entity identity. Generated mesh child sources keep the primitive/submesh data but remove scene/node transforms. Materials bind base-color, normal, emissive, metallic-roughness, and occlusion refs plus their binding-local sampler/transform metadata. Base-color and emissive variants are sRGB; normal, metallic-roughness, and occlusion variants are linear. Missing glTF sampler metadata resolves to trilinear filtering with a complete mip chain. Explicit non-mip minification filters `9728`/`9729` retain a one-level variant, while `9984` through `9987` request a mip chain and preserve nearest/linear level selection. Shared glTF images emit one physical generated child while retaining every material binding and variant. An emitted normal binding selects `USE_NORMAL_MAP`; `alphaMode: MASK` selects `ALPHA_TEST` and emits `AlphaCutoff`; `alphaMode: BLEND` emits straight-alpha `SrcAlpha` / `OneMinusSrcAlpha` render state for the transparent queue. Keyword choices participate in normalized cooked shader identity, while blend state participates in material/pipeline identity.
- `ModelSourceReimporter` is the explicit editor reimport boundary. It validates that a model output root stays under the same package/workspace `Assets` root as the selected `.arismodel`, rejects any `.arisen` output path, scans generated metadata before writing, refuses foreign generated metadata from another source GUID, and reports stale same-source generated children as orphans instead of deleting them. It emits through `GltfModelImportEmitter`, using the final `OutputRoot` segment as the generated output name so `Assets/Generated/Lantern` writes directly into that folder.
- The editor mesh inspector uses the planner for raw `.gltf` / `.glb` mesh sources, while the editor model inspector uses `ModelSourceAssetLoader` plus the planner for `.arismodel` roots. Both show import diagnostics before generation: planned scene/mesh/material/texture child counts, texture-reference counts, generated child GUIDs/provenance, material factor and alpha previews, sampler/transform-aware texture refs, and unsupported feature warnings.
- The editor model inspector also exposes an explicit `Reimport` action. A successful reimport refreshes the runtime asset index for emitted children when the concrete runtime database is available, invalidates cooked artifacts for the model root and planned generated children, publishes coarse asset-change events, and leaves watcher-originated file registration to the existing `AssetImportScheduler`.
- `ModelReimportValidationFixture_ReimportsIndexesAndLoadsGeneratedScene` is the bounded integration check for this workflow. It creates a temporary package-owned `.arismodel` plus external-buffer glTF source, explicitly reimports one scene/mesh/material and three physical textures, indexes the emitted `.meta` sidecars, resolves all four generated material bindings through typed `Texture2D` lookups, and loads the generated scene into a real `EntityManager`. A second reimport must preserve child GUIDs and sidecar provenance exactly; every emitted source must remain under the selected `Assets/Generated` root and outside `.arisen`; foreign-source generated metadata must block reimport before emission.

Generated glTF/GLB child source emission stays in the model planning/import boundary rather than hidden inside the static mesh cooker. The cooker remains responsible for a single mesh source and its compact submesh/material-slot table.

The first scene source asset slice adds `.arisenscene` / `.scene` files as `Scene` assets:

- `SceneSourceAsset` generated refs let package setup load scenes without raw GUID literals.
- `SceneAssetLoader` parses YAML scene source schema version 2 for Editor authoring and explicit diagnostic/build-stage source access. Every entity has a non-empty persistent `Guid`; optional `Parent` references carry an entity GUID and may repeat the current scene GUID. Missing/duplicate IDs, missing local parents, cycles, and external-scene parents fail before ECS mutation. External references are reserved for the world-reference policy rather than becoming dangling dense handles.
- `SceneComponentSchemas` owns the built-in source/cooked codecs, while `ISceneComponentExtensionRegistry` lets a selected package register a high-range stable type ID without moving its domain types into `com.arisen.resources`. Extensions parse their own YAML, emit and validate bounded canonical payloads in the generic aligned extension-component section, contribute explicit runtime dependencies/variants, instantiate their own ECS struct, and may declare one exclusive stable ownership identity. A required extension fails when its provider package/codec is absent; unknown or newer optional extensions remain skippable. Built-in and extension declarations share canonical TypeId ordering, version rules, and corruption checks.
- `SceneAssetLoader.InspectScene` exposes the same identity, schema, hierarchy, and reference validation as a read-only tooling boundary for the editor inspector. It reports scene/entity/component/reference data and diagnostics without spawning ECS entities or mutating source files.
- Editor Hierarchy mirrors the current `IEditorSceneDocumentService` inspection, which is keyed by the same scene GUID/package/source identity published by `IRuntimeSceneService`. Selecting another asset does not replace Hierarchy with an unrelated scene; double-click scene opening goes through the document service and the frame-boundary runtime activation request.
- Source-scene authoring is staged. `SceneAssetLoader.UpdateEntityTransformSource` targets the persistent entity GUID in an in-memory YAML source string; inspector commands, undo/redo, hierarchy node reuse, expansion state, and selection therefore do not depend on source-list position. Each revision is validated and queued as an immutable `SceneSourceSnapshot`; `RuntimeSceneService` validates the complete snapshot before applying its deterministic entity/component batch at `EngineKernel.OnFrameEnd`.
- Explicit Save is the only editor source write. It validates the current snapshot, rejects generated or non-`Assets` paths, compares the current disk bytes with the document's saved baseline to prevent overwriting external changes, and atomically replaces UTF-8 source through a same-directory temporary file while preserving BOM policy. Undo restores the exact prior working source, so returning to the saved revision clears dirty state without depending on YAML formatting equivalence.
- Scene switching and editor shutdown use Save/Discard/Cancel resolution when the document is dirty. External watcher changes reload a clean document through the same frame boundary; concurrent disk and editor changes mark a conflict and block Save. The retired `.arisen` serializer, editor scene singleton, last-opened-scene setting, and direct live-ECS hierarchy commands are not part of the scene asset path.
- The loader canonicalizes staged entities by authoring GUID, creates every dense runtime entity first, then resolves local parent/child/sibling components. `SceneAuthoringEntityMap` retains a sorted setup/editor/gameplay lookup from GUID to dense `Entity`; GUIDs never become ECS indices or render-loop lookup keys. The loader also spawns the existing name, transform, camera, mesh, lighting, and environment component data. Render extraction and RenderGraph recording continue to consume contiguous ECS snapshots and prepared resources, not scene source files or identity maps.
- `manifest.json` selects the startup scene by stable GUID and package id. `ProjectSceneBootstrapSubsystem` resolves that selection during `PostInit` and loads it through `IRuntimeSceneService`; product code no longer names the Lantern scene or creates a hidden code-only fallback world.
- `SceneSubsystem` creates one `EntityManager` during initialization and never replaces it. Runtime `Entity` is an eight-byte `(slot, generation)` value; `EntityManager.IsAlive` and generation-aware sparse-set membership reject stale handles after slot reuse. Structural add/get/remove calls validate liveness, while contiguous component arrays and dense entity arrays remain the per-frame iteration path. Single removal stays swap-dense; scene-sized bulk removal uses stable compaction so surviving extraction order is deterministic.
- `RuntimeSceneService` owns persistent/additive scene-instance identity above that one ECS world. Each internal instance tracks its scene asset identity, lifecycle state, owned entity array, compact authoring map, canonical cooked dependencies, source revision, and diagnostics. Public snapshots expose counts and immutable dependency views rather than mutable ownership collections; coarse lookup can resolve an authoring GUID or an entity owner outside ECS/render loops.
- Workspace source access stages through `SceneAssetLoader`; read-only runtime mode stages through `SceneAssetCooker.TryLoadCookedStaging`. Both paths complete validation before ECS mutation, and instantiation rolls back any entities created by an unexpected activation failure. The startup-only synchronous `LoadScene` compatibility path and queued editor replacement both activate a new persistent instance and then bulk-unload prior scene ownership without replacing the manager. A validation failure leaves every active instance untouched.
- `RequestAdditiveSceneLoad` and `RequestSceneUnload` enqueue deterministic operations processed by the resources package at `EngineKernel.OnFrameEnd`. Activation publishes entities to ECS only at that boundary; unload destroys exactly the target instance's owned entities and leaves persistent, additive, and non-scene-owned entities intact. Local hierarchy links are reconstructed within one instance. A parent/child/sibling handle crossing an unload ownership boundary rejects unload with an instance diagnostic instead of leaving a dangling handle.
- `IRuntimeSceneService` exposes queued load/unload, active-instance snapshots, bounded terminal/diagnostic history, authoring/entity-owner lookup, and isolated lifecycle/load events. Event handlers run outside ECS/render hot loops and observer exceptions cannot invalidate completed structural mutation. `ActiveScene` remains the persistent-scene compatibility view used by current editor documents.
- `SceneAssetCooker` keeps runtime variant `runtime.scene.v1` and extension `.ariscene`, while payload/container format version 2 carries the source-v2 identity contract. Explicit `Cook` writes and registers the artifact through `IAssetDatabase`; explicit `LoadCooked` acquires the cooked handle, validates all bytes and dependencies, stages the complete scene, instantiates ECS, and releases the handle.
- Source and cooked loading share `SceneStagingData` plus one `InstantiateStagedScene` path. Source schema v2 is normalized and fully staged before source loading mutates ECS, and remains available for Editor working-source preview. The shared instantiator creates every handle first, applies components and hierarchy deterministically, and rolls back its created handles on failure. Legacy source v1 never auto-loads: `MigrateLegacySceneSource` / `MigrateLegacySceneFile` provide an explicit one-time operation that assigns random persistent IDs and writes current component declarations. Cooked loading has no dependency on YAML types or the source file.
- The cooked container has a fixed 96-byte little-endian header with `ARISCENE` magic, container/source versions, canonical source GUID, byte-order marker, exact file size, section count, and SHA-256 over the directory/payload. Thirteen 32-byte directory entries describe aligned metadata, canonical UTF-8 strings, 32-byte GUID-bearing entity records, parent-index hierarchy records, typed asset references, per-component streams, and a required component-schema table.
- Core sections are explicitly required; component payload sections remain optional. The component-schema table stores stable type ID, version, required/optional flag, and owning section type. Unknown optional sections/schemas and newer optional schemas can be skipped; unknown/newer required data fails. Hierarchy records are canonical by child index and resolve back to authoring GUIDs before the shared hierarchy validator runs.
- Entity/component records use fixed serialized widths and explicit little-endian fields rather than CLR struct layout. Asset references are canonicalized by kind/GUID/package, preserve required/optional intent, and are resolved for type and package identity before entity creation.
- The reader caps total size/counts/string lengths, validates directory alignment, duplicate types, integer overflow, section overlap, strides, exact payload consumption, canonical ordering, finite component data, GUID identity, source version, and content hash before exposing staging data. Failed cooked loads cannot partially mutate the destination `EntityManager`.
- Focused tests prove byte-identical recooking and source-reorder cooking, artifact registration, source/cooked ECS and GUID-map parity after deleting YAML, parent/sibling reconstruction, explicit legacy migration, duplicate/missing/cross-scene diagnostics, component migration and required/optional policy, malformed/truncated/overflowed/overlapping/non-finite rejection, unsupported source-version rejection, deterministic importer IDs, GUID-based editor edits after reorder, successful cooking/loading of the package-owned Lantern showcase scene, stale-generation rejection, stable bulk compaction, persistent plus additive extraction, exact unload, cross-instance hierarchy rejection, controlled replacement, and bounded repeated instance cycles.

The first world descriptor slice adds `.arisenworld` files as `World` assets:

- `WorldSourceAsset` generated refs and optional workspace `StartupWorld` selection preserve package/GUID identity. When selected, `StartupWorld` is both the `startupWorld` Production cook root and the runtime activation root: its persistent scene is activated during `PostInit`, while streamable cells wait for a camera source or explicit pin. `StartupScene` is the compatibility fallback only when no world is selected.
- Source schema version 2 stores the world GUID/name, persistent scene, double-precision partition origin/cell size, load radius, unload hysteresis, active-cell limit, canonical layers, explicit cells, bounds, optional world-space cell `FocusBounds`, scene references, dependencies, residency estimates, and stable entity references. Version-1 worlds without focus metadata migrate in memory. Focus bounds must be finite, ordered, and contained by their cell. The declared world GUID must equal the `.meta` GUID. Unknown or duplicate YAML fields fail instead of being ignored.
- `WorldCellIdentity` derives an RFC-4122-shaped deterministic GUID from `world GUID + integer XYZ coordinate + canonical layer`. Layer text is lowercase ASCII identity data. IDs therefore do not depend on source/deployment paths, source list order, or runtime request order.
- Cell bounds must be finite, ordered, and non-overlapping within one layer. Cell keys, IDs, scene package/type identities, explicit dependencies, and referenced authoring entity GUIDs are all validated before cooking. Neighbor IDs are the existing six axis-adjacent keys in the same layer and both neighbor/dependency arrays are serialized in stable ID order.
- Raw runtime `Entity` handles are never serialized. A world reference stores source cell ID/entity GUID plus either a persistent target entity GUID or target cell ID/entity GUID. Optional references remain unresolved until the target is active, clear when it unloads, and may late-resolve after reload. Required cell references additionally create a load dependency. Version 1 rejects dependency cycles; optional soft-reference cycles do not create dependency edges.
- `WorldAssetCooker` emits `runtime.world.v1` / `.ariworld` format version 2. Its fixed 96-byte little-endian `ARIWORLD` header carries byte order, source/format versions, world identity, counts, exact size, and payload SHA-256. The canonical payload stores policy/partition data, layers, cell scene identities, optional focus bounds, exact cooked-scene hash/size, CPU/GPU estimates, neighbors, dependencies, and entity references using explicit field widths and big-endian GUID bytes.
- World cooking invokes the existing scene cooker for each unique persistent/cell scene, records the exact resulting scene hashes, and returns those scenes as required runtime-catalog dependencies. `RuntimeAssetCookCoordinator` then closes each scene's transitive mesh/material/texture/shader dependencies normally. Cooked world loading validates header, hash, counts, canonical order, identities, bounds, and exact consumption, then verifies every recorded scene artifact's catalog identity, size, and SHA-256 before exposing the descriptor.
- Focused tests prove byte-identical output after cell reorder and world-source move, stable IDs, cooked-only loading, complete Production closure, neighbor/dependency order, reference policy, and fail-closed handling for overlapping bounds, duplicate cells, undeclared dependencies, cycles, unresolved entities, corruption, truncation, and unsupported versions.

The first terrain authoring-source slice is owned by `com.arisen.terrain`:

- `TerrainRoot` (`.aristerrain`) source schemas 1-2 bind the declared `TerrainGuid` to the `.meta` GUID and store double-precision world placement, positive X/Z sample spacing, a finite height range, one height source, a `2^n + 1` tile resolution, `SharedEdgeSamples` border policy, signed tile origin, a package/GUID layer-set reference, and persisted generated tile records. Schema 2 may additionally name one strict `.ariweights` `Rgba8Hex` raster whose dimensions must equal the height raster. Height dimensions must be exact shared-edge multiples of the tile resolution.
- `TerrainLayerSet` (`.ariterrainlayers`) source schemas 1-2 bind `LayerSetGuid` to metadata and preserve an ordered, uniquely named list of one to four layers. Every layer carries required package/GUID `Texture2D` references for albedo, normal, and packed ORM inputs. Schema 2 adds finite tint, roughness/metallic multipliers, normal strength, and positive X/Z world tiling; schema-1 layers receive compatible defaults. Ordering is authored material meaning and is not alphabetically normalized.
- `TerrainTileIdentity` derives each generated tile GUID through `GeneratedAssetIdentity` from `(terrain root GUID, owning package ID, "terrain-tile", "x=<signed X>;z=<signed Z>")`. Coordinates are canonical row-major Z/X records; height sample changes do not alter tile identity, while a changed signed source coordinate does. Persisted records with missing, duplicate, out-of-grid, or stale GUIDs fail before cooking.
- The only accepted height input is binary grayscale PGM `P5` with maximum value exactly `65535`. Two-byte samples are decoded big-endian as scalar height codes, never as color, and source row zero maps to local Z row zero. P2, P6, 8-bit P5, non-ASCII or oversized headers, invalid dimensions, truncation, extra separators, and trailing payload all fail. Source/header/sample limits are checked before sample allocation.
- `TerrainImportPlanner` is the package-neutral terrain creation boundary. Given one indexed layer set, strict PGM source, package `Assets` root, output folder/name, double-precision world bounds, shared-edge tile resolution, and signed origin, it derives sample spacing, canonical generated paths, stable child GUIDs, and optional active-world cell ownership/intersections. Planning is read-only: it validates every input and existing owned output, returns diagnostics plus a content/state fingerprint, and never creates the destination directory or metadata.
- `TerrainImportEmitter.Commit` always replans and compares that fingerprint before writing. Root-identity replacement, tile-grid changes, and world-layout changes require an explicit destructive-regeneration option. All targets are confined below the selected package `Assets` root and reject `.arisen` segments; existing generated outputs must carry matching generated-child provenance. The emitter stages every source/meta payload, moves all affected existing files into a same-root backup, installs the new set, and restores prior bytes on failure. Rollback failures are aggregated with the original commit error instead of masking it. Unchanged tile coordinates preserve GUIDs, confirmed shrink removes only owned stale outputs, and the importer migrates its legacy flat generated-tile layout to root-scoped paths without identity churn.
- `IAssetSourceIndex` is an Editor/setup-only reconciliation contract implemented by `AssetDatabase`. After a successful terrain filesystem commit, the terrain editor refreshes only the affected source directory under a lock; the in-memory source registry rolls back if rescanning fails. The editor then invalidates cooked data and publishes coarse asset-change events for created/changed/deleted root and tile identities. Runtime consumers continue to use `IAssetDatabase`; read-only runtime catalogs reject source refresh and never scan package source. The source-index refresh is deliberately outside RenderGraph recording and live ECS mutation.
- `TerrainRootAssetCooker` emits `runtime.terrain-root.v2` / `.ariterrainroot` cooked format 2; `TerrainTileAssetCooker` emits independently deployable `runtime.terrain-tile.v1` / `.ariterraintile` artifacts. Both formats use fixed-width little-endian headers, `ARITROOT` / `ARITTILE` magic, source/container versions, an endian marker, exact file size, aligned 32-byte section descriptors, zero padding, and SHA-256 over the directory and payload.
- A cooked root embeds finite double-precision placement/range/sampling metadata, the ordered layer set, canonical texture package/GUID identities, all layer material parameters, and canonical Z/X tile records. Each tile record stores deterministic identity, four axis-neighbor GUIDs, min/max height, exact payload size, and a SHA-256 of the complete tile artifact. The package-owned runtime cooker returns each tile plus each unique albedo `r8g8b8a8unorm.srgb.mips`, normal `r8g8b8a8unorm.linear.mips.normalmap`, and ORM `r8g8b8a8unorm.linear.mips` texture as required dependencies; the layer source itself is not needed at runtime because its ordered contract is embedded.
- A cooked tile embeds one full shared-edge sample grid as little-endian `R16`, explicit height offset/scale, world placement, sample spacing, source-grid offset, min/max bounds, four normalized `UNorm8` layer-weight channels, and deterministic level-wise geometric error. Source tile identity remains independent from error/LOD records and contains no RHI/backend handles. Authored channels are normalized with an exact largest-remainder allocation whose byte sum is 255; zero input and schema-1 roots fall back deterministically to channel zero.
- Terrain readers bound file/section/string/sample counts; validate magic, versions, size, hashes, alignment, overlap, padding, required/unknown section policy, deterministic GUIDs, canonical tile/neighborhood order, finite dimensions/ranges, exact quantized min/max, normalized weights, and recomputed monotonic error values before publishing data. Root cooking also compares every adjacent duplicated height/weight edge. Byte-identical recooks do not replace existing files, preserving unchanged artifact timestamps.
- `TerrainSourceAssetTests` and `TerrainCookedAssetTests` cover signed identity, strict source parsing, lossless height decode, source reimport stability, root/tile byte determinism, shared borders, runtime cooker variants, unchanged-file reuse, oversized section ranges, missing texture-cooker dependency chains, and fail-closed handling for header/hash/truncation/directory overlap, unknown required sections, malformed dimensions, stale identity, invalid neighbors, weights, and errors. The canonical `33x33` `ShowcaseValley.pgm` fixture deterministically cooks four `17x17` tiles in a two-by-two grid; integration tests reload all four independent artifacts, verify reciprocal neighbor identities and exact world placement, and compare every duplicated height and four-channel weight sample on shared X and Z borders. Its terrain scene components contribute required root/tile runtime variants, and Development, Production, plus copied cooked-only Production all prove positive terrain draws without parsing terrain YAML or PGM. A shrink test proves deterministic closure, unchanged retained-tile timestamps, persistent removal of obsolete generated tiles from the mutable cache, and transactional removal of obsolete terrain tile files/catalog rows from deployed output.

The first vegetation source-to-cooked slice is owned by `com.arisen.vegetation`:

- `VegetationSpecies` authoring files use `.arivegetationspecies` and importer `ArisenVegetationSpeciesImporter`. Source schema 1 binds `SpeciesGuid` to the indexed `.meta` GUID and package owner, then preserves an ordered list of one to eight mesh/material LODs, shadow policy, finite scale/yaw/tilt ranges, collision-promotion policy with bounded capsule parameters when enabled, and bounded wind response. Both maximum distance and maximum screen error must increase strictly across the authored LOD order.
- `VegetationBiome` authoring files use `.arivegetationbiome` and importer `ArisenVegetationBiomeImporter`. Source schema 1 binds `BiomeGuid` to the indexed `.meta` GUID and package owner, then preserves the authored entry order. Each uniquely identified entry carries one typed species reference, density, global plus entry seed data, finite altitude/slope ranges, unique bounded terrain-layer weight rules, minimum spacing, cluster-size intent, and exclusion policy. Runtime quality multipliers are not part of this stable source contract.
- Species cooking emits `runtime.vegetation-species.v1` with `ARIVSPEC` magic; biome cooking emits `runtime.vegetation-biome.v1` with `ARIVBIOM` magic. Both currently retain their source extension for the cooked artifact (`.arivegetationspecies` or `.arivegetationbiome`), but the runtime variant and catalog identity distinguish the binary payload from authoring YAML.
- Both format-1 containers use a fixed 128-byte little-endian header with an endian marker, cooked/source versions, canonical asset GUID, bounded record counts, exact file size, reserved-zero fields, and SHA-256 over the section directory and payload. Their sorted 32-byte section descriptors require eight-byte offsets, bounded counts/strides, non-overlapping ranges, and zero alignment/trailing padding. Readers reject malformed ranges, unsupported flags, and unknown required sections before publishing data.
- Species payloads contain required metadata, canonical sorted UTF-8 strings, and fixed-width LOD sections. Biome payloads contain required metadata, canonical sorted UTF-8 strings, fixed-width authored-order entry records, and fixed-width layer-weight-rule records. Readers revalidate identity, finite ranges, enums, ordering, uniqueness, and exact section consumption instead of treating cooked bytes as CLR layout.
- Dependency ownership is checked against `IAssetDatabase` before a staging transaction is opened and again when cooked data is loaded. A species requires each exact package/GUID `Mesh` as `staticmesh.uint32` and `Material` as `material.runtime`; a biome requires each exact package/GUID `VegetationSpecies` as `runtime.vegetation-species.v1`. `VegetationRuntimeAssetCooker` also rejects a request whose package/type owner or explicit variant does not match the indexed descriptor, and returns those typed required requests to `RuntimeAssetCookCoordinator` for transitive closure.
- Canonical package fixtures `ValleyRock.arivegetationspecies` and `ShowcaseValley.arivegetationbiome` provide stable species/biome GUIDs and a real mesh/material/species dependency chain. Focused source and cooked tests cover strict identity/range/order validation, byte-identical repeated writes, round-trip fidelity, canonical dependency order, and fail-closed header, hash, directory, required-section, identity, and semantic corruption.
- Generated instance pages emit `runtime.vegetation-instance-page.v1` / `.arivegetationpage` with `ARIVPAGE` magic. Required metadata, canonical strings, species, and instance sections bound each page to one explicit non-empty page GUID, parent cluster GUID, package, generated-schema version, finite double-world origin, and bitwise-recomputed conservative AABB. A page contains at most `65,536` instances and `1,024` used species. Species are sorted by package/GUID and input indices are remapped; stable nonzero `ulong` instance keys are strictly increasing. Each 40-byte instance record stores the canonical species index, local `float3`, canonical normalized `SNORM16x4` quaternion, positive uniform scale, and positive conservative radius.
- Generated cluster roots emit `runtime.vegetation-cluster.v1` / `.arivegetationcluster` with `ARIVCLUS` magic. Required metadata, canonical strings, species, and page sections bind one logical cluster GUID to an exact package/GUID biome dependency, the canonical union of page species, and at most `4,096` canonically ordered page references containing no more than `1,048,576` total instances. The cluster package owns every page; the referenced biome may be owned by another exact package. Page stable keys are unique across the cluster, cluster species must be declared by the authored biome at publication and by the cooked biome at runtime load, and root bounds must bitwise equal the union of page bounds.
- A cluster page reference pins the complete page artifact by exact byte size and full-file SHA-256 in addition to GUID, package, count, origin, and bounds. Page dependencies contain only their species; cluster dependencies contain the biome, canonical species union, and pages, avoiding a page-to-cluster catalog cycle. Deployment cooking structurally snapshots existing generated roots and pages under their shared publication gate, validates catalog ownership, page pins, and current authored-biome membership, and emits dependency requests even when authored biome/species variants have not yet been cooked. Exact generated bytes are copied to content-addressed files below the cook staging root before the gate is released, so later mutable-cache replacement/removal cannot invalidate the returned deployment input. The coordinator then cooks and closes authored dependencies normally instead of requiring cache order or republishing generated bytes.
- Format-v1 instance-page GUIDs are immutable publication identities. Byte-identical recooks reuse the artifact path, timestamp, and registry generation; byte-different publication under the same GUID/variant is rejected. Changed page content must use a new explicit page GUID. Cluster GUIDs are logical aggregate identities and may advance to reference newly identified pages.
- Child pages are serialized and validated before publication, then the cluster root commits last under the shared setup-time publication gate. This is not transactional page-set replacement: stale page artifacts remain registered, and committed children are not rolled back if later root publication fails. Allocating derived page/cluster GUIDs, atomically switching and pruning a generated page set, and rollback remain terrain-aware scatter-baking work.

This slice does not define terrain-aware vegetation scatter output, generated GUID derivation, positive-border or world-cell ownership, transactional stale-page removal, scene codecs, residency, rendering, Editor authoring, or backend behavior. Those remain later vegetation milestones.

Terrain brush authoring is an in-memory transaction until explicit save. `TerrainAuthoringDocument` owns a validated root/tile working set, exact saved baselines, dirty tile identities, and deterministic brush commands. The first height brush applies signed quantized linear-falloff deltas; the first four-layer paint brush changes one selected channel while renormalizing all channels to an exact `UNorm8` sum of 255. Samples on shared edges and corners update every owning tile. Undo records contain only changed sample indices and exact before/after values, so undo/redo restores byte-identical data and clears dirty state when the working copy returns to its baseline.

Each successful brush command publishes immutable, content-hashed preview revisions through the bounded `ITerrainAuthoringPreviewService`. Revisions are self-contained across the document's bounded dirty tile set, so coalescing a later stroke cannot discard an earlier unsaved tile. The service retains the newest unsaved revision while cells unload or resident resources are rebuilt. This preview channel never writes source files, invalidates cooked artifacts, mutates ECS, or touches RHI state from the UI thread.

`TerrainAuthoringDocument` fingerprints root, height, and optional weight sources at open and reports those external changes separately from local dirty samples. Reimport requires an explicit `ReloadExternal` or `MergeLocalChanges` policy when both sides changed. Reload replaces the working baseline and clears commands that no longer describe it; merge reapplies exact local sample changes over the external data and republishes the complete dirty preview. Structural root-layout changes require closing and reopening the document. Legacy canonical height/weight metadata may be adopted only when its identity and path ownership validate.

Save installs all affected height/weight source payloads through one backup-backed filesystem transaction, restoring every prior byte if any source write fails. Incremental cooking runs only after that commit, reuses byte-identical artifacts, and includes dependency-invalidated or missing outputs in addition to directly changed tiles. A later cook/publication failure retains the affected coordinates as `sources saved | cook pending`, allowing an idempotent Save retry without another edit. Successful cooking refreshes the source index and publishes asset-change events; only then does the document accept the new saved baseline and clear retained preview dirtiness.

- `IAssetDatabase.RemoveCookedArtifacts` is the explicit destructive cache boundary. It accepts exact GUID/variant identities, rejects read-only runtime mode and any registered path outside `CookedRoot`, moves existing files into a transaction quarantine, atomically replaces `AssetManifest.json`, and restores both registry rows and original paths if the manifest commit fails. Successful removal releases loaded generations and prunes empty artifact directories. Package cookers must derive the removal set from a validated ownership record; terrain uses the previously published cooked root and never scans or guesses unrelated cache content.

Runtime world streaming is specified in `WorldStreaming.md`. `com.arisen.resources` uses the shared `IBackgroundTaskScheduler` to read/decode/validate immutable cell staging, then activates or unloads exact scene-instance ownership at `EngineKernel.OnFrameEnd`. Monotonic generations reject stale completions; camera radius, stable priority, dependency closure, hysteresis, explicit pins, active-cell limits, byte/staging/activation/unload budgets, explicit retry, bounded diagnostics, and Tracy telemetry are observable through `IRuntimeWorldStreamingService`. Workers do not mutate ECS or record RHI work.

- `AssetDatabase.InitializeRuntimeCatalog` is the Production runtime boundary. It parses `<output>/runtime-assets.json`, requires the expected target profile, validates every declared file under `<output>/Content` by relative-path policy, size, and SHA-256, then publishes source-independent GUID/package/type descriptors and cooked artifact lookup. It indexes no `Assets` directories, creates no `.meta` or cache files, and exposes an empty source `Assets` collection.
- A mounted runtime catalog is read-only. Cook-path allocation, artifact registration, invalidation, and source refresh throw instead of falling back to on-demand cooking. `TryLoadCookedAsset` validates expected type through catalog descriptors, so no synthetic `AssetRecord` or fake source path is required.
- Production scene, Generic RP settings, mesh, material, texture, shader-stage, environment, and IBL setup paths acquire their declared cooked variants directly. Editor working-source snapshots remain source-backed and are explicitly rejected in read-only runtime mode. Source timestamp/reimport checks remain confined to workspace mode.
- The Generic RP settings payload now has a strict cooked reader that validates magic, exact version/GUID, bounded UTF-8 name, canonical flags, finite/ranged values, power-of-two shadow size, complete consumption, and truncation before provider activation.

The first render-pipeline settings asset adds `.arisrenderpipeline` files as `RenderPipelineSettings` assets:

- `RenderPipelineSettingsSourceAsset` generated refs preserve typed package-owned identity.
- `GenericRenderPipelineSettingsLoader` parses and validates version 1 Generic RP YAML during workspace-mode provider activation and selects the versioned cooked settings payload in read-only runtime mode. Both paths reject non-power-of-two shadow sizes outside `256..8192`, non-finite/out-of-range bias and strength values, and PCF radii outside `0..3`.
- `manifest.json` stores the selected settings GUID and owning provider package under `RenderPipeline`. `RenderSubsystem` activates the matching `IRenderPipelineProvider`; the Generic RP package no longer installs an implicit in-memory default asset from `OnLoad`.
- Project Settings lists indexed settings assets from base workspace packages and atomically applies startup-scene and render-pipeline references through the comment/BOM/newline-preserving manifest patcher. Selection changes take effect on the next launch.
- `DefaultGenericRP.arisrenderpipeline` is the package-owned baseline. Its values flow through setup into shadow target allocation and compact draw constants; render-pass recording never parses the YAML or consults project settings.

The first typed material property slices add `ScalarProperties` and `Vector4Properties` to `.arismaterial` and `material.runtime`. The smoke material currently authors scalar `MetallicFactor`/`RoughnessFactor` defaults and a Vector4 `BaseColorFactor`. `RHIMaterialResource` caches both scalar and Vector4 properties during setup; `StaticMeshPass` currently consumes `BaseColorFactor` and pushes it alongside the bindless texture/sampler indices. This keeps parameter lookup and defaulting in setup code; RenderGraph command recording only consumes compact unmanaged constants.

The shared PBR material convention adds `MetallicRoughness` and `Occlusion` Texture2D slots plus `OcclusionStrength` and `AlphaCutoff` scalar properties. Packed data follows the glTF channel convention: occlusion is red, roughness is green, and metallic is blue. Missing occlusion strength defaults to `1.0`; missing alpha cutoff defaults to `0.5`. These names and values live in `MaterialTextureSlots`, `MaterialPropertySlots`, `MaterialPbrTextureChannels`, and `MaterialPbrDefaults` so import, setup, tests, and shader preparation share one contract.

Each `Texture2DRefs` entry may also author binding-local `Sampler` metadata (`MinFilter`, `MagFilter`, `MipmapMode`, `WrapU`, `WrapV`, `MaxAnisotropy`) and `Transform` metadata (`Offset`, `Scale`, `Rotation`, `TexCoord`). The same image asset may therefore be reused by bindings with different sampling intent without making sampler state part of pixel cooking. Omitted sampler metadata resolves to linear min/mag filtering, linear mip interpolation, repeat wrapping, and 1x anisotropy; omitted transform metadata resolves to zero offset, unit scale, zero rotation, and UV set zero. `GenerateMipMaps` remains part of the texture cooked variant, while the complete sampler record participates in prepared-resource identity. `MaterialAssetLoader` validates finite transform and 1x-16x anisotropy data, version 7 `material.runtime` payloads preserve the resolved values, `Texture2DAssetCooker` writes the requested packed chain, and `RHITexture2DResource` clamps requested anisotropy to `RHICapabilities.MaxSamplerAnisotropy` while creating the full-LOD sampler during setup. `RHIMaterialResource` caches the transform beside the bindless binding for later shader preparation.

The first material render-state slice adds a `RenderState` section to `.arismaterial`; active shader keywords entered the cooked format in version 5, version 6 adds sampler/texture-transform bindings, and version 7 preserves bounded maximum anisotropy as part of sampler identity. The initial render-state fields are culling mode, front-face winding, and color-blend intent. Older cooked material payloads still load with deterministic compatibility defaults where possible, while the current cooker forces recook when the version changes. `StaticMeshPass` builds graphics pipelines from prepared material state and includes shader GUID, shader dependency stamp, shader variant identity, color/depth formats, and render state in its pipeline reuse key. Material slots in one pass may use different compatible states or keyword variants because setup-time batching buckets draw commands by material pipeline key before command recording.

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
