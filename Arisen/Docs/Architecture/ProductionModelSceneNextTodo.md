# Arisen Production Model Scene: Next TODO Roadmap

**Date:** 2026-07-15  
**Scope:** Roadmap after completing the production scene rendering vertical slice.  
**Primary goal:** Move from a hand-authored teapot showcase to a production-facing glTF/GLB model scene workflow with stronger material, lighting, editor, and RenderGraph quality.

---

## Current State Summary

The previous production scene roadmap is complete.

The engine can now render an authored scene with:

- package-driven boot and profile generation;
- editor and runtime render surfaces;
- Vulkan RHI startup, shared editor viewport output, and RenderGraph execution;
- task-graph command recording through `RenderCommandList`;
- authored scene loading through stable `AssetRef<SceneSourceAsset>` references;
- static mesh extraction from ECS into frame snapshots;
- StandardLit ShaderLab materials with base color, normal, metallic, roughness, emissive factor, optional emissive texture, and explicit keyword variants;
- PNG/JPEG/PPM texture cooking and setup-time GPU upload;
- OBJ, `.gltf`, and `.glb` static mesh cooking;
- generated glTF/GLB scene, mesh, material, and texture child emission with deterministic generated asset identity;
- directional, point, and spot lights;
- procedural fallback plus an optional cooked lat-long sky, HDR scene color, tonemapping, deterministic render queues, frustum culling, and first directional shadows;
- editor scene inspection, hierarchy mirroring, transform editing, and viewport selection feedback;
- Tracy diagnostics for render extraction, setup, RenderGraph, task graph work, and pass-level counts.

The default scene now uses the package-owned Khronos Lantern GLB sample through a stable `.arismodel` root, deterministic generated scene/mesh/material/texture children, and an authored wrapper scene that adds camera, lights, environment, and ground context. The previous Utah teapot, pedestal, and ground plane remain package-owned secondary/fallback showcase content. The next roadmap work should make model import/reimport an editor workflow, then harden material coverage, lighting, shadows, transparency, graph resources, and visual validation around real production content.

Recent validation baseline:

- `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1`
- Editor, Development, Production, and RHIVulkanTesting smoke runs pass.
- Active `vk_validation.log` files are empty.

---

## Guiding Rules

1. **Default visible content should exercise production paths.** The default sample scene should use a `ModelSourceAsset` / generated-child import path, not only hand-authored OBJ meshes.
2. **Generated children stay deterministic.** Reimport must preserve child GUIDs for stable source identity and child keys.
3. **Runtime still loads by GUID, not path.** User-facing code should use generated typed refs or editor-authored references, not raw file paths.
4. **Import and cooking stay outside render recording.** RenderGraph worker threads must not parse glTF, read source assets, inspect editor registry state, or generate child files.
5. **Improve visuals by closing real gaps.** Prefer material texture coverage, environment lighting, shadow fitting, and transparency policy over decorative one-off scene tricks.
6. **Editor workflow is part of production readiness.** If a feature requires command-line/manual file edits forever, it is not done.
7. **Every visible slice keeps bounded validation green.** Use full runtime validation for changes touching render output, package generation, RHI lifetime, scene loading, or editor viewport behavior.

---

## Milestone 1 - Production glTF/GLB Showcase Model

**Goal:** Replace or supplement the teapot showcase with a package-owned production glTF/GLB sample that exercises generated scene, mesh, material, and texture children.

### TODO

- [x] Choose a suitable sample model.
  - [x] Prefer CC0 or similarly permissive licensing.
  - [x] Prefer one static model with multiple nodes, primitives, materials, textures, and normals.
  - [x] Avoid skins, animation, morph targets, and unsupported extensions for this first default sample.
  - [x] Record source/license/provenance in package-local notices.
- [x] Add the model root as package-owned source content.
  - [x] Add a `.glb` or `.gltf` source under a package `Assets` root.
  - [x] Add a `.arismodel` root asset with stable `.meta` identity.
  - [x] Decide whether the teapot remains as fallback or secondary showcase content.
- [x] Generate model children through `GltfModelImportPlanner` / `GltfModelImportEmitter`.
  - [x] Emit generated `.arisenscene`, mesh, material, and texture child assets under a deterministic package-owned output folder.
  - [x] Keep generated metadata provenance intact.
  - [x] Verify reimport does not change child GUIDs when source identity and child keys stay stable.
- [x] Switch the default scene smoke path to the generated model scene.
  - [x] Load by generated typed ref.
  - [x] Preserve setup-time scene loading and render-safe frame snapshots.
  - [x] Keep the old code-created fallback for diagnostics only.
- [x] Add focused tests and runtime smoke coverage.
  - [x] Test generated scene inspection for the selected model.
  - [x] Test generated material and texture references.
  - [x] Run full runtime validation.

### Completion Notes

- The default visual path now loads `LanternShowcaseScene.arisenscene`, an authored wrapper around generated Khronos Lantern children.
- `Assets/Models/Lantern/Lantern.arismodel` is the stable model-root identity, and `Assets/Generated/Lantern` contains deterministic generated scene, mesh, material, and texture child assets.
- Generated normal texture variants use linear color space; base-color and emissive variants stay sRGB.
- `PackageLanternShowcaseScene_LoadsGeneratedModelChildren` covers generated scene inspection, material texture refs/color spaces, mesh cooking, and texture cooking.
- Full runtime validation passes for Editor, Development, Production, and RHIVulkanTesting.

### Acceptance Criteria

- Default scene smoke renders a production glTF/GLB-derived scene.
- Generated children are package-owned and stable across reimport.
- RenderGraph recording consumes only prepared mesh/material/light data.
- The teapot showcase is no longer the only attractive sample scene.

---

## Milestone 2 - Model Reimport And Editor Workflow

**Goal:** Make model import repeatable from the editor, not just from tests or ad hoc helper code.

### TODO

- [x] Define the first `.arismodel` source schema.
  - [x] Source glTF/GLB reference.
  - [x] Output folder/name.
  - [x] Selected glTF scene index.
  - [x] Unit scale and root transform.
  - [x] Target material shader.
  - [x] Texture emission enabled/disabled.
- [x] Add editor model inspection.
  - [x] Show planned generated scene/mesh/material/texture child counts.
  - [x] Show unsupported feature warnings.
  - [x] Show generated child GUIDs and provenance.
- [x] Add an explicit reimport command.
  - [x] Write generated children only under allowed package/workspace `Assets` roots.
  - [x] Refuse to overwrite generated metadata from a different source GUID.
  - [x] Report orphaned generated children instead of deleting blindly.
  - [x] Route file watcher changes through the import scheduler instead of doing work in watcher callbacks.
- [x] Add reimport validation.
  - [x] Same input produces same generated metadata.
  - [x] Source material/texture changes invalidate cooked outputs.
  - [x] Rename/move behavior keeps the root `.arismodel` identity stable.

### Progress Notes

- `ModelSourceAssetLoader` now parses the first `.arismodel` schema and resolves source/output paths from the package-owned model root.
- Selecting a `Model` asset in the editor Inspector shows model source settings, resolved glTF/GLB source, output root, root transform defaults, material shader target, generated child counts, generated child GUID/provenance, material previews, supported texture refs, and unsupported feature warnings.
- `GltfModelImportPlanner` now advertises generated `Texture2D` children only for supported imported material texture refs in the current slice, so planned generated children match what `GltfModelImportEmitter` can emit.
- `PackageLanternModelRoot_InspectsStableGeneratedChildren` verifies same-input planning preserves generated metadata for the Lantern model root.
- `ModelSourceReimporter` now owns explicit model reimport safety: output roots must resolve below the same package/workspace `Assets` root as the selected `.arismodel`, `.arisen` output is rejected, generated metadata from another source GUID blocks reimport, and same-source children no longer in the current plan are reported as orphans rather than deleted.
- The editor model inspector now exposes a `Reimport` workflow action, shows the last reimport status, scans generated output diagnostics, refreshes the runtime asset index for emitted children when available, and invalidates/notifies the model root plus generated child GUIDs after a successful explicit reimport.
- `ModelSourceReimporter.InvalidateCookedOutputs` is now the shared reimport invalidation boundary. It invalidates the model root and every planned generated child, publishes change events even when a newly emitted child has not been indexed yet, and feeds the render-side reload queue by GUID.
- Cookers now treat a missing cooked-registry record as a hard recook condition, so invalidation cannot accidentally reuse a newer orphaned artifact file. Material dependency stamps combine every material, shader/include, and texture file stamp instead of retaining only the newest timestamp.
- `ModelSourceReimporter_MaterialAndTextureChangesInvalidateCookedOutputs` changes generated material factors and texture pixels, preserves generated child GUIDs, forces old cooked files into the future, and verifies recooked payloads plus model/material/texture reload-queue GUIDs.
- Watcher-originated file changes continue through `AssetImportScheduler`, so generated file bursts are debounced and processed outside `FileSystemWatcher` callbacks.
- Editor rename/move processing now treats the old SQLite asset registration as authoritative, moves the source `.meta` sidecar with overwrite semantics, reconciles any racing destination sidecar to the preserved GUID, verifies destination registration, and only then publishes the rename event. `AssetImporter_RenameMovePreservesModelRootAndGeneratedChildIdentity` covers a model rename plus directory move and verifies that replanning/reimport keeps every generated child GUID and source-provenance reference stable.

### Acceptance Criteria

- A user can select a model root in the editor and reimport generated children safely.
- Reimport diagnostics are visible and actionable.
- Generated outputs are never written under `.arisen`.
- File watcher bursts do not cause partial or duplicate generated output.

---

## Milestone 3 - PBR Material Texture Coverage

**Goal:** Bring StandardLit closer to real glTF metallic-roughness content.

### TODO

- [x] Extend material slot/property conventions.
  - [x] Metallic-roughness packed texture.
  - [x] Occlusion texture and strength.
  - [x] Alpha cutoff for alpha-test materials.
  - [x] Optional sampler settings for wrap/filter.
  - [x] Optional texture coordinate transform metadata.
- [x] Update glTF material planning/emission.
  - [x] Import `pbrMetallicRoughness.metallicRoughnessTexture`.
  - [x] Import `occlusionTexture`.
  - [x] Map `alphaMode: MASK` to `ALPHA_TEST` and alpha cutoff.
  - [x] Preserve unsupported transparent/blend warnings until the transparent pass exists.
- [x] Update StandardLit.
  - [x] Sample packed metallic/roughness texture when present.
  - [x] Apply occlusion to indirect/environment contribution.
  - [x] Apply alpha-test clipping before depth/color output.
  - [x] Keep keyword variants explicit and cooked.
- [x] Add material editor coverage.
  - [x] Edit texture slots.
  - [x] Edit scalar/vector properties.
  - [x] Surface shader-contract validation errors.

### Progress Notes

- Shared PBR conventions now define `MetallicRoughness` and `Occlusion` texture slots, `OcclusionStrength` and `AlphaCutoff` scalar properties, packed channels `R=occlusion`, `G=roughness`, `B=metallic`, and deterministic defaults of `1.0` occlusion strength and `0.5` alpha cutoff.
- `MaterialTexture2DRef` now carries optional binding-local sampler settings plus offset/scale/rotation/UV-set transform metadata. Omitted metadata resolves to linear filtering, nearest mip selection, repeat wrapping, and the identity transform on `TEXCOORD_0`.
- Version 6 `material.runtime` payloads preserve resolved sampler and texture-transform data. Older payloads remain readable with deterministic defaults, while the current cooker recooks stale versions.
- `RHITexture2DResource` creates the authored setup-time sampler through per-axis RHI address modes, and `RHIMaterialResource` caches each resolved transform next to the prepared bindless binding. RenderGraph command recording remains unchanged; StandardLit consumption belongs to the later shader item.
- `MaterialConventions_DefinePbrBindingsAndDeterministicTextureDefaults` and `MaterialCooker_PreservesShaderRenderStateAndTextureBindingMetadata` cover the shared names/defaults and source-to-cooked round trip.
- `GltfModelImportPlanner` now imports metallic-roughness and occlusion texture bindings, occlusion strength, glTF sampler enums, and `KHR_texture_transform`. It preserves each resolved UV set in material metadata and reports nonzero UV sets because the current static mesh shader path still samples only `TEXCOORD_0`.
- `GltfModelImportEmitter` emits packed and occlusion bindings as linear variants, keeps sampler/transform metadata binding-local, and deduplicates the physical generated image when several bindings reference the same glTF image. `MASK` materials select the explicit `ALPHA_TEST` variant and emit the authored/default cutoff; `BLEND` remains unmodified and produces a transparent-pass warning.
- The package-owned Lantern generated material now includes its linear metallic-roughness image child. Planner/emitter tests cover shared-image bindings, sampler mapping, UV transforms, occlusion strength, alpha cutoff, `MASK`, and retained `BLEND` diagnostics.
- `StaticMeshPass` now resolves optional metallic-roughness/occlusion bindings, occlusion strength, and alpha cutoff during setup. The values travel in each draw's existing bindless object-buffer record, preserving the 124-byte draw push-constant contract and allocation-free command recording.
- StandardLit multiplies roughness from packed green and metallic from packed blue, reuses packed red for occlusion when image/sampler bindings match, and applies authored occlusion strength only to ambient diffuse/specular. Direct lights and emissive remain unoccluded. The explicit `ALPHA_TEST` variant clips sampled base-color alpha against the authored cutoff.
- Generated materials with a normal texture now select `USE_NORMAL_MAP`; masked materials continue to select `ALPHA_TEST`, and normalized keyword sets remain part of cooked shader/pipeline identity. The checked-in Lantern material selects the normal-map variant.
- The focused rendering suite passes all 87 tests. Full Editor, Development, Production, and RHIVulkanTesting GPU smoke validation passes, the StandardLit `USE_NORMAL_MAP` vertex/fragment SPIR-V artifacts are cooked, and every active `vk_validation.log` is empty.
- The material Inspector now edits existing Texture2D bindings through an indexed texture dropdown, scalar properties through numeric controls, and Vector4 properties through a four-component control. Each edit is an undoable command that atomically updates YAML, invalidates cooked material data, and publishes the material GUID through the runtime asset-change boundary.
- Texture reassignment preserves binding slot, cooked/color-space variant, sampler, and UV-transform metadata. Source format follows the selected texture extension. Importer-generated material children are read-only based on `.meta` `Generated` provenance and remain owned by model reimport.
- `MaterialAssetLoader.InspectSource` exposes every missing Texture2D/scalar/Vector4 shader-contract binding as structured diagnostics, allowing the Inspector to retain existing property rows while showing actionable errors. `LoadSource` remains strict for runtime/cooking use.
- Material source edits preserve unrelated YAML mappings and refuse unknown or duplicate property names without writing. Focused coverage includes source round trips, metadata preservation, contract diagnostics, deterministic execute/undo, and generated-source rejection.

### Acceptance Criteria

- A real glTF material using base color, normal, metallic-roughness, occlusion, and alpha mask can render correctly.
- Missing optional textures use deterministic defaults.
- Material import still validates before GPU resource setup.
- Command recording receives only prepared unmanaged constants and bindless indices.

---

## Milestone 4 - Environment Lighting And Reflections

**Goal:** Move from colored ambient plus procedural sky to a first image-based lighting path.

### TODO

- [x] Add skybox/environment texture assets.
  - [x] Define source metadata for environment textures.
  - [x] Cook cubemap or lat-long environment input into runtime-ready data.
  - [x] Keep default procedural sky as fallback.
- [x] Add first IBL resources.
  - [x] Diffuse irradiance placeholder or generated irradiance texture.
  - [x] Prefiltered specular environment texture.
  - [x] BRDF integration LUT.
- [x] Extend StandardLit.
  - [x] Add indirect diffuse from environment.
  - [x] Add roughness-aware specular reflection.
  - [x] Keep direct lights and emissive in linear HDR.
- [x] Add exposure calibration.
  - [x] Keep fixed exposure as baseline.
  - [x] Add optional authored exposure on scene environment.
  - [x] Defer auto exposure unless sample content needs it.

### Progress Notes

- `.arienvironment` is now the stable authoring descriptor for environment textures. It references a typed Texture2D GUID and records `LatLong` layout, source color space, runtime format, rotation, and intensity; editor/runtime importers also recognize float `.hdr` sources.
- `EnvironmentTextureAssetCooker` validates strict 2:1 input, decodes LDR or float HDR data, converts RGB to linear when authored as sRGB, and emits a one-mip `R16G16B16A16SFloat` cooked payload. The variant is `latlong.r16g16b16a16sfloat.nomips`, and descriptor plus source metadata participate in recook stamps.
- `RHIEnvironmentTextureResource` performs setup-time staging upload and bindless registration through the shared 2D texture allocation owner. Replacement follows the existing deferred disposal queue, including descriptor/source hot reload.
- `SceneEnvironmentComponent` and its frame snapshot carry an optional environment GUID. `EnvironmentSkyPass` reconstructs camera-relative world directions, samples the lat-long map into linear HDR scene color, and keeps the previous procedural gradient whenever no valid prepared map exists.
- `LanternShowcaseScene.arisenscene` selects the package-owned `BlueHour.arienvironment`; `SmokeScene.arisenscene` intentionally omits an environment texture and remains fallback coverage.
- RenderDoc verified that the Lantern color pass binds one `D32_SFLOAT` target, enables depth test/write with `LessEqual`, writes the body/chain/head silhouette, and preserves that depth while drawing the ground. The first blue brace patch was an overly strong authored rim light and is now a warm `Lantern Downlight`, but the remaining incorrect occlusion was a separate back-face-culling regression: Vulkan static pipelines inverted `EFrontFace` after already adopting the RHI negative-height viewport. The source GLB's winding matches its normals, and `Cull None` / opposite-front-face A/B frames restored the near shell. Static pipeline creation now uses the same direct `EFrontFace` to `VkFrontFace` mapping as dynamic state, and the Lantern remains authored as `Cull Back` with counter-clockwise fronts.
- Focused coverage now includes descriptor/importer validation, generated typed refs, 2:1/half-float cooking, source dependency recooking, scene reference extraction/inspection, shader fallback/sample branches, and the checked-in Lantern environment.
- The focused rendering suite passes all 90 tests. `validate_fast.bat` and full Editor, Development, Production, and RHIVulkanTesting scene-smoke validation pass; every active `vk_validation.log` is empty.
- `EnvironmentLightingAssetCooker` derives a 32x16 cosine-convolved diffuse-radiance map, a 128x64 GGX-prefiltered lat-long texture with eight roughness mips, and a 128x128 split-sum BRDF integration LUT. All outputs are deterministic linear `R16G16B16A16SFloat` data in the versioned `ibl.latlong.rgba16f.v1` cooked artifact; descriptor/source timestamps and cooked-registry invalidation control cache reuse and recooking.
- The common 2D allocation path now validates tightly packed mip payloads, uploads every mip subresource, creates a sampler whose LOD range covers the full chain, and transitions the whole mip range through the Vulkan convenience barrier. Single-mip material/environment textures retain the same API and behavior.
- `RHIEnvironmentLightingResource` owns the three setup-time allocations and exposes prepared bindless image/sampler indices, specular max LOD, rotation, and intensity. `GenericRenderPipeline` creates and replaces it beside the visible environment resource, reports IBL readiness through logs/Tracy, and defers disposal behind the last submitted ticket. A failed IBL preparation retains the authored sky and ambient fallback.
- Focused IBL coverage validates constant-radiance convolution, complete packed mip sizing, nontrivial BRDF output, cache reuse, and source-driven recooking. The rendering suite now passes all 91 tests, and the regenerated Development native/managed workspace builds successfully.
- `validate_fast.bat` and the full Editor, Development, Production, and RHIVulkanTesting GPU scene-smoke gate pass with no skips or CPU fallbacks. Development generated the 222,648-byte IBL artifact and all runtime rendering profiles uploaded its three resources without an IBL fallback warning; every active `vk_validation.log` is empty.
- `StaticMeshEnvironmentLightingConstants` converts a valid setup-owned `RHIEnvironmentLightingResource` into three aligned object-buffer vectors: irradiance/specular indices, BRDF indices plus max LOD/enabled state, and rotation/intensity parameters. Every mesh record receives those values, local-light records receive the disabled form, and `SmokeStaticMesh` now declares the complete matching structured-buffer stride. The 124-byte draw push-constant contract is unchanged.
- StandardLit maps world normal and reflection directions with the same lat-long convention as the visible sky. It samples diffuse irradiance at LOD 0, the prefiltered specular map at `roughness * maxLod`, and the split-sum BRDF LUT at `(NdotV, roughness)`. Roughness-aware Schlick Fresnel controls indirect energy weighting; environment asset intensity and scene ambient intensity scale the result, and material occlusion affects only indirect lighting. Direct directional/point/spot lights and emissive remain unchanged in linear HDR.
- Missing or failed IBL keeps the previous colored ambient diffuse/specular formula exactly. All environment work remains in setup; command recording reads only prepared unmanaged constants and bindless indices.
- Focused rendering coverage remains 91/91. The regenerated Development workspace builds with zero errors, updated StandardLit `USE_NORMAL_MAP` and `USE_TRIPLANAR` SPIR-V artifacts cook successfully, and full Editor, Development, Production, and RHIVulkanTesting GPU scene-smoke validation passes with zero skips/fallbacks. Every active `vk_validation.log` is empty.
- Scene-authored exposure is a linear HDR multiplier with a deterministic `1.0` baseline. Scene YAML may omit `Exposure`; finite authored values are clamped to `[0, 64]`, and non-finite values resolve to the baseline.
- `SceneAssetLoader`, editor scene/entity inspection, `SceneEnvironmentComponent`, and the immutable render snapshot now preserve exposure through setup. `GenericRenderPipeline` passes the prepared value to `TonemapPass`, and Tracy exposes both `Render.SceneExposure` and `TonemapPass.Exposure` without adding source lookup or luminance work to command recording.
- `LanternShowcaseScene.arisenscene` authors `Exposure: 1.15`. Focused coverage verifies authored/default/fallback values, normalization, snapshot transfer, Inspector visibility, and source wiring that rejects restoring the old hardcoded pipeline value.
- The focused rendering suite passes all 93 tests. Full Editor, Development, Production, and RHIVulkanTesting GPU scene-smoke validation passes with zero skips or CPU fallbacks, and every active `vk_validation.log` is empty.
- Milestone 4 is complete. Automatic exposure remains explicitly deferred until a later roadmap defines histogram, adaptation, and temporal policy.

### Acceptance Criteria

- Metallic and rough materials visibly respond to environment lighting.
- The sample scene no longer depends only on direct lights for visual depth.
- IBL resources are prepared during setup and never generated during pass recording.
- Authored exposure calibrates HDR output while omitted values preserve the previous `1.0` result.

---

## Milestone 5 - Shadow And Visibility Quality

**Goal:** Replace the showcase-fixed directional shadow slice with a scene-aware shadow policy.

### TODO

- [ ] Fit the directional shadow camera from scene/camera bounds.
  - [ ] Compute visible caster/receiver bounds during setup.
  - [ ] Use stable snapping to reduce shimmering.
  - [ ] Fall back to the fixed showcase slice when bounds are unavailable.
- [ ] Add shadow quality controls.
  - [ ] Depth bias and slope bias.
  - [ ] PCF radius/quality settings.
  - [ ] Shadow map size as a pipeline/scene setting.
- [ ] Add shadow culling.
  - [ ] Separate camera-visible draw span from shadow-caster draw span.
  - [ ] Keep shadow culling output compact and setup-owned.
- [ ] Document cascade policy.
  - [ ] Keep single-map shadows for this milestone.
  - [ ] Define the first cascade plan for a later large-scene milestone.

### Acceptance Criteria

- Directional shadows still work after replacing the teapot scene.
- Shadow coverage follows the current sample model instead of hardcoded showcase constants.
- Shadow draw count is visible in Tracy and culling diagnostics.

---

## Milestone 6 - Transparent And Alpha Queue Policy

**Goal:** Make imported alpha content predictable instead of only classifying it into a late deterministic queue.

### TODO

- [ ] Implement an explicit transparent pass.
  - [ ] Keep opaque and alpha-test in depth-writing static mesh pass.
  - [ ] Draw transparent materials after opaque lighting.
  - [ ] Sort transparent draws back-to-front by camera distance.
- [ ] Define transparent depth policy.
  - [ ] Depth test on.
  - [ ] Depth write off.
  - [ ] Blend state from material render state.
- [ ] Map glTF alpha modes.
  - [ ] `OPAQUE` to opaque queue.
  - [ ] `MASK` to alpha-test queue and cutoff.
  - [ ] `BLEND` to transparent queue.
- [ ] Add diagnostics.
  - [ ] Plot opaque, alpha-test, transparent, and skipped alpha draw counts.
  - [ ] Warn when a material requests transparency before the pass is available.

### Acceptance Criteria

- Alpha-masked and alpha-blended imported model parts are handled intentionally.
- Transparent draw ordering is deterministic and camera-aware.
- Opaque pass performance and hot-path rules are preserved.

---

## Milestone 7 - RenderGraph Resource Ownership Hardening

**Goal:** Continue moving pass-owned resources into graph-declared resources with graph-owned barriers and lifetime.

### TODO

- [ ] Move frame depth into graph-owned transient allocation.
  - [ ] Declare depth usage from all passes.
  - [ ] Remove pass-owned depth image transitions from `StaticMeshPass`.
  - [ ] Preserve resize and deferred disposal safety.
- [ ] Move directional shadow map allocation/barriers into the graph resource planner.
  - [ ] Keep `DirectionalShadowTarget` as a high-level owner only if needed.
  - [ ] Let pass declarations drive depth write and shader-read transitions.
- [ ] Extend resource diagnostics.
  - [ ] Log depth/shadow access chains.
  - [ ] Validate invalid load/read/write combinations.
  - [ ] Preserve pass-culling behavior for side-effect and output passes.
- [ ] Prepare for future aliasing.
  - [ ] Track lifetime intervals for transient textures.
  - [ ] Do not implement aliasing until depth/shadow ownership is stable.

### Acceptance Criteria

- Scene color, frame depth, and directional shadow map follow the same resource-planning model.
- Render passes contain fewer manual layout transitions.
- Full runtime validation remains green with empty active Vulkan validation logs.

---

## Milestone 8 - Automated Visual And Viewport Validation

**Goal:** Catch visual regressions such as Y-flip, blank first SceneView frame, viewport flicker, missing model children, and broken material import earlier.

### TODO

- [ ] Add bounded screenshot or image-summary validation.
  - [ ] Capture a deterministic runtime smoke frame.
  - [ ] Check nonblank color/depth output.
  - [ ] Track coarse luminance/color histogram rather than brittle pixel-perfect output.
- [ ] Add editor viewport smoke validation.
  - [ ] Verify first SceneView frame presents without switching tabs.
  - [ ] Verify SceneView and GameView orientation.
  - [ ] Verify resize generation and shared-texture pacing state.
- [ ] Add import/reimport validation fixtures.
  - [ ] Generated model children are present.
  - [ ] Generated material texture refs resolve.
  - [ ] Generated scene loads through `SceneAssetLoader`.
- [ ] Add profiler workflow checks.
  - [ ] Keep `open_tracy_profiler.bat` documented.
  - [ ] Add a short profiler-enabled manual run recipe for model-scene profiling.

### Acceptance Criteria

- The common rendering regressions from this roadmap are checked without relying only on manual screenshots.
- Automated checks stay bounded enough for local validation.
- Longer visual/profiler sessions remain manual and explicit.

---

## Deferred Later

These are important, but should not block the next production model scene roadmap:

- skeletal animation, skins, morph targets, and animation clips;
- full material graph or ShaderGraph authoring;
- full automatic shader variant matrix build/caching;
- DX12 or Metal backend packages;
- clustered or tiled lighting;
- cascaded shadows for large outdoor scenes;
- full in-editor profiler timeline UI;
- cooked binary scene payloads replacing the current source-YAML scene parsing path.

---

## Recommended Immediate Sprint

Begin **Milestone 5** with scene-aware directional shadow camera fitting.

The fastest useful next step is:

1. Compute a setup-owned world-space bound for visible directional-shadow casters and receivers from the prepared static-mesh items.
2. Fit a single orthographic directional-light camera to that bound while retaining the current fixed showcase slice when no usable bound exists.
3. Stabilize the fitted projection by snapping its light-space center to shadow-map texel increments so camera motion does not introduce avoidable shimmer.
4. Keep command recording unchanged: pass only the prepared light view-projection and compact caster span to `DirectionalShadowPass` and `StaticMeshPass`.
5. Add focused bounds/fallback/stability tests, Tracy diagnostics, and full runtime smoke validation.

Why this order:

- The current fixed orthographic slice was suitable for the teapot showcase but is not derived from the imported Lantern scene or active camera.
- Fitting and stable snapping establish the shadow-space contract before adding bias, PCF quality, map-size controls, or separate shadow-caster culling.
- A single-map scene-aware policy improves the current sample without prematurely introducing cascades.
