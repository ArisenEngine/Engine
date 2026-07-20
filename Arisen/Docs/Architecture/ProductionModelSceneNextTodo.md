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
- `GltfModelImportEmitter` emits packed and occlusion bindings as linear variants, keeps sampler/transform metadata binding-local, and deduplicates the physical generated image when several bindings reference the same glTF image. `MASK` materials select the explicit `ALPHA_TEST` variant and emit the authored/default cutoff; `BLEND` materials now emit straight-alpha render state for the explicit transparent pass.
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

## Milestone 5 - Project Composition And Active Scene

**Goal:** Replace development-only scene/pipeline bootstrap with project-selected assets shared by Editor and runtime.

### TODO

- [x] Add workspace startup-scene selection.
  - [x] Store a stable scene GUID and owning package id in `manifest.json`.
  - [x] Validate incomplete startup-scene references in `ArisenBuildTool`.
  - [x] Remove the hardcoded `LanternShowcaseScene` reference from package code.
- [x] Add one runtime scene activation boundary.
  - [x] Load and validate into a temporary ECS world.
  - [x] Replace `SceneSubsystem.ActiveEntityManager` only after successful loading.
  - [x] Publish active scene identity through `IRuntimeSceneService`.
- [x] Unify initial Editor and runtime scene ownership.
  - [x] Load the selected startup scene from a `PostInit` composition subsystem before frame zero.
  - [x] Stop editor boot from synthesizing/loading an unrelated legacy `SampleScene`.
  - [x] Make Hierarchy inspect the same active `.arisenscene` rendered by the viewport.
- [x] Complete project-facing scene workflow.
  - [x] Route editor scene switching through a frame-boundary activation request.
  - [x] Make Project Settings edit the startup scene reference without rewriting unrelated manifest fields/comments.
  - [x] Define source-scene save/dirty policy and retire the conflicting legacy `.arisen` active-scene path.
- [x] Make render-pipeline selection asset-driven.
  - [x] Define a serialized Generic RP settings asset and loader.
  - [x] Move shadow size/bias/PCF and other project-level quality settings into that asset.
  - [x] Register a render-pipeline provider/factory instead of auto-assigning `GenericRenderPipelineAsset` during package load.
  - [x] Let Project Settings select the render-pipeline settings asset.

### Progress Notes

- `manifest.json` now selects `LanternShowcaseScene.arisenscene` through `StartupScene.Guid` plus `StartupScene.PackageId`; workspace validation rejects partial references.
- `RuntimeSceneService` owns macro-level scene loading. It parses into a candidate `EntityManager`, preserves the previous world on failure, atomically activates successful worlds through `SceneSubsystem`, and publishes immutable active-scene metadata for tooling.
- `ProjectSceneBootstrapSubsystem` replaces the first-frame `MeshRenderTest` callback. It registers `MeshSystem` and activates the configured startup scene during `PostInit`, before Editor handoff or standalone frame zero.
- Editor `ProjectSynthesisStep` no longer creates or loads `Assets/Scenes/SampleScene.arisen`. Hierarchy initializes from `IRuntimeSceneService.ActiveScene` and uses `SceneAssetLoader.InspectScene`, so the hierarchy and viewport share one scene identity without exposing live ECS mutation to the UI thread.
- `IRuntimeSceneService.RequestSceneLoad` now provides a coalesced cross-thread request boundary drained by `ResourcesPackage` from `EngineKernel.OnFrameEnd`. Active-scene transform edits, undo/redo, and editor `.arisenscene` opening use this path; loading still occurs into a candidate world, failed reloads preserve the current world, and successful replacement refreshes Hierarchy while preserving the selected source entity by scene path and entity index.
- Editor shared-texture presentation now receives an explicit host-specific wakeup after each finalized render output. The viewport coalesces those notifications onto Avalonia's UI thread, preventing producer pacing from freezing after its initial image burst and making frame-boundary scene edits visible without switching Scene/Game tabs.
- The cross-cutting simulation scheduler now distinguishes one-shot frame graphs from reusable compiled ECS schedules. `SceneSubsystem` consumes the package-owned shared worker executor, systems run on every frame without creating a private worker pool, worker failures propagate to the engine thread, and failed frames discard deferred structural commands.
- Editor Project Settings now lists indexed package-owned Scene assets, supports explicit Apply/Revert and Use Active Scene actions, and updates the workspace `StartupScene` reference for the next launch. Its structured UTF-8 patcher validates that the owner is a base workspace package and changes only the top-level startup-scene value (or inserts it before `Packages`), preserving comments, trailing commas, BOM/newline style, unknown fields, and unrelated bytes. Focused coverage includes replacement, insertion, BOM/CRLF/comment preservation, profile-only package rejection without mutation, duplicate-property rejection, and the no-op case.
- `IEditorSceneDocumentService` now owns saved and working source for the active `.arisenscene`. Transform commands edit the working YAML, mark the document dirty, and queue immutable revisioned snapshots for frame-boundary preview without touching disk. Runtime loading still uses a candidate world and publishes a completion report; failed snapshots preserve the previous world.
- Save validates the staged snapshot, detects external source changes against the saved baseline, and atomically replaces UTF-8 source only on explicit user action. Undo/redo restores exact working-source revisions, dirty scene switching and editor close use Save/Discard/Cancel resolution, and title/Hierarchy state expose dirty or external-conflict status.
- Hierarchy now follows only the active editor document and preserves node identity, selection, and fold state across previews. The legacy `SceneManagerService`, `SceneService`, `.arisen` open/save path, reflection serializer, last-opened-scene setting, editor camera registry, and direct live-ECS entity commands have been removed.
- Focused scene coverage includes snapshot success/failure isolation, staged preview, atomic Save, exact-source undo/redo, external-write conflict handling, dirty-switch policy, and pending-activation queue protection.
- `DefaultGenericRP.arisrenderpipeline` is the first stable `RenderPipelineSettings` asset. It stores fallback clear color and directional-shadow enabled state, map size, receiver depth/slope bias, strength, and PCF radius; strict loader validation rejects unsupported versions/providers and unsafe ranges.
- `com.arisen.generic-renderpipeline` now provides `IRenderPipelineProvider` instead of assigning an in-memory default from package load. `RenderSubsystem.Initialize` asks the single composition-selected provider to activate `manifest.json`'s `RenderPipeline` GUID/package identity; the referenced asset may be owned by the game package.
- Project Settings lists eligible package-owned pipeline settings beside startup scenes. Apply patches both references in one atomic write while preserving comments, trailing commas, BOM/newline style, unknown fields, and unrelated bytes.
- Runtime asset indexing reads package ownership from each selected `package.json` instead of deriving it from a package-root path; empty owner identities are rejected before assets enter the registry.
- Focused settings/provider/manifest tests pass, BuildTool emits typed `AssetRef<RenderPipelineSettingsSourceAsset>` helpers, and the generated Editor profile builds with zero errors.
- Full Debug runtime validation passes for `Editor`, `Development`, `Production`, and `RHIVulkanTesting` with one scene-smoke frame each, zero skips/fallbacks, and empty active `vk_validation.log` files. Milestone 5 implementation is complete.

### Acceptance Criteria

- Editor Hierarchy and viewport identify the same scene immediately after boot.
- Runtime and Editor profiles activate the same manifest-selected startup scene before frame zero.
- Editing or undoing an active source-scene transform updates the viewport after the next frame boundary without restarting the editor.
- Failed scene parsing/reference validation cannot replace or partially mutate the active world.
- Product startup contains no hardcoded showcase scene reference or hidden code-created fallback.
- Render-pipeline implementation remains code-defined while project-facing quality settings and selection become serialized assets.

---

## Milestone 6 - Shadow And Visibility Quality

**Goal:** Replace the showcase-fixed directional shadow slice with a scene-aware shadow policy.

### TODO

- [x] Fit the directional shadow camera from scene/camera bounds.
  - [x] Compute visible caster/receiver bounds during setup.
  - [x] Use stable snapping to reduce shimmering.
  - [x] Fall back to the fixed showcase slice when bounds are unavailable.
- [x] Add shadow quality controls.
  - [x] Depth bias and slope bias.
  - [x] PCF radius/quality settings.
  - [x] Shadow map size as a pipeline/scene setting.
- [x] Add shadow culling.
  - [x] Separate camera-visible draw span from shadow-caster draw span.
  - [x] Keep shadow culling output compact and setup-owned.
- [x] Document cascade policy.
  - [x] Keep single-map shadows for this milestone.
  - [x] Define the first cascade plan for a later large-scene milestone.

### Progress Notes

- `StaticMeshFrustumCuller` now exposes conservative world-AABB extraction shared by primary-camera and light-frustum tests. Authored bounds remain preferred, cooked mesh bounds remain the fallback, and unusable bounds stay conservatively visible.
- `DirectionalShadowFitter` unions bounded camera-visible receivers, fits a padded square orthographic slice in a stable light basis, and snaps its XY center to the selected map's world-space texel increment. The previous showcase matrix remains the deterministic no-bounds fallback.
- Generic RP owns separate reusable camera and shadow draw arrays. The shadow traversal considers all valid static scene items against the fitted light frustum and expands only accepted submeshes into the pass span; no culling or scene access moved into command recording.
- Tracy and throttled setup diagnostics expose receiver, caster, culled-caster, camera-draw, shadow-draw, fitted/fallback, diameter/depth, and world-units-per-texel data.
- This milestone intentionally keeps one shadow map. The first deferred cascade design uses four practical-split camera slices, independently fitted/snapped projections, per-cascade compact caster spans, a depth-array target, maximum-distance fade, and setup-prepared selection data.
- Focused rendering coverage passes all 135 tests. Full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU scene-smoke runs, zero skips or CPU fallbacks, and empty active `vk_validation.log` files. Milestone 6 is complete.

### Acceptance Criteria

- Directional shadows still work after replacing the teapot scene.
- Shadow coverage follows the current sample model instead of hardcoded showcase constants.
- Shadow draw count is visible in Tracy and culling diagnostics.

---

## Milestone 7 - Transparent And Alpha Queue Policy

**Goal:** Make imported alpha content predictable instead of only classifying it into a late deterministic queue.

### TODO

- [x] Implement an explicit transparent pass.
  - [x] Keep opaque and alpha-test in depth-writing static mesh pass.
  - [x] Draw transparent materials after opaque lighting.
  - [x] Sort transparent draws back-to-front by camera-space depth.
- [x] Define transparent depth policy.
  - [x] Depth test on.
  - [x] Depth write off.
  - [x] Blend state from material render state.
- [x] Map glTF alpha modes.
  - [x] `OPAQUE` to opaque queue.
  - [x] `MASK` to alpha-test queue and cutoff.
  - [x] `BLEND` to transparent queue.
- [x] Add diagnostics.
  - [x] Plot opaque, alpha-test, transparent, and skipped alpha draw counts.
  - [x] Retire the temporary `BLEND` unsupported warning once the pass is available while preserving unknown-mode diagnostics.

### Progress Notes

- Generic RP partitions camera-visible draw commands into reusable depth-writing and transparent arrays during setup. Material queue lookup, validation, and sorting stay outside command recording.
- `TransparentDrawOrdering` sorts by descending camera-space depth from each draw's transformed local origin and uses source draw order as the stable tie-breaker. `StaticMeshPass` preserves that prepared order while still splitting adjacent pipeline batches into bounded worker-recording ranges; RenderGraph submits those ranges in index order.
- The opaque and transparent `StaticMeshPass` instances share one concrete depth target. Opaque and alpha-test pipelines clear and write depth; the transparent pass loads the same attachment, uses `LESS_OR_EQUAL` testing, disables depth writes in its pipeline key/state, and preserves material-authored blending.
- Transparent draws run after opaque HDR lighting and before tonemapping. They are excluded from the directional depth-only caster span; alpha-test draws remain depth-writing and shadow eligible.
- glTF `OPAQUE` keeps default opaque state, `MASK` selects `ALPHA_TEST` plus cutoff, and `BLEND` emits `SrcAlpha` / `OneMinusSrcAlpha` straight-alpha render state. Unknown alpha modes still produce import diagnostics.
- Tracy exposes setup-level opaque, alpha-test, transparent, and skipped-alpha counts plus transparent pass draw/batch/work-item counters. Focused rendering coverage passes all 146 tests, including deterministic off-axis depth ordering and generated `BLEND` material loading.
- Full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU scene-smoke runs, zero skips or CPU fallbacks, and all six discovered `vk_validation.log` files empty. Milestone 7 is complete.

### Acceptance Criteria

- Alpha-masked and alpha-blended imported model parts are handled intentionally.
- Transparent draw ordering is deterministic and camera-aware.
- Opaque pass performance and hot-path rules are preserved.

---

## Milestone 8 - RenderGraph Resource Ownership Hardening

**Goal:** Continue moving pass-owned resources into graph-declared resources with graph-owned barriers and lifetime.

### TODO

- [x] Move frame depth into graph-owned transient allocation.
  - [x] Declare depth usage from all passes.
  - [x] Remove pass-owned depth image transitions from `StaticMeshPass`.
  - [x] Preserve resize and deferred disposal safety.
- [x] Move directional shadow map allocation/barriers into the graph resource planner.
  - [x] Remove `DirectionalShadowTarget`; `RenderGraphTexture` is the sole allocation owner.
  - [x] Let pass declarations drive depth write and shader-read transitions.
- [x] Extend resource diagnostics.
  - [x] Log depth/shadow access chains.
  - [x] Validate invalid load/read/write combinations.
  - [x] Preserve pass-culling behavior for side-effect and output passes.
- [x] Prepare for future aliasing.
  - [x] Track lifetime intervals for transient textures.
  - [x] Do not implement aliasing until depth/shadow ownership is stable.

### Progress Notes

- `GenericRenderPipeline` now requests a depth-only `FORMAT_D32_SFLOAT` `FrameDepth` transient beside HDR `SceneColor` and binds its prepared view/format into both static-mesh passes.
- `StaticMeshPass` no longer creates, resizes, transitions, shares through a pass-to-pass binding, or disposes the frame depth image. Opaque/alpha-test work declares read/write attachment use and retains clear-then-load semantics; transparent work declares read-only attachment use and records against the read-only depth layout with writes disabled.
- `RenderGraphTextureDescriptor.DepthAttachment2D` requests only depth-attachment usage and a depth aspect, without an unnecessary sampler or bindless descriptors. Existing graph-owned resize replacement continues through `DeferredRenderResourceDisposalQueue` and the last submitted queue ticket.
- `DepthReadAttachment` gives the planner an explicit write-to-read-only transition with depth attachment read access. RenderGraph now preflights pass work-item counts and plans/persists states only for active passes, preventing a zero-draw transparent pass from claiming a transition it never records.
- Focused descriptor, transition-chain, inactive-pass, profiling, and source-ownership contracts pass in the `154/154` rendering suite. Full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU smoke runs, zero skips or CPU fallbacks, and all six discovered `vk_validation.log` files empty.
- `RenderGraphTextureDescriptor.DepthAttachmentSampled2D` gives `DirectionalShadowMap` depth-attachment and sampled usage, a depth aspect, and graph-owned bindless image/sampler registration. Generic RP requests it at the selected settings size and passes only its prepared view/format/dimensions and sampling indices to recorders.
- `DirectionalShadowTarget` and its expected-layout state are removed. `DirectionalShadowPass` no longer allocates, transitions, or disposes the image; its depth-write declaration followed by static-mesh shader reads produces the graph-planned write-to-read barrier. The focused rendering suite passes `157/157`, and a regenerated Development workspace builds successfully.
- Development runtime diagnostics show `GenericDirectionalShadowMap` as a 2048x2048 `FORMAT_D32_SFLOAT` texture with usage `0x24`, depth aspect, and valid bindless image/sampler indices. Its access chain is `DirectionalShadowPass[write:DepthAttachment] -> GenericStaticMeshPass[read:ShaderRead] -> GenericTransparentStaticMeshPass[read:ShaderRead]`, with planned `Unknown -> DepthAttachment -> ShaderRead` transitions. Full Debug runtime validation passes all four GPU profiles with zero skips or CPU fallbacks, and all six discovered `vk_validation.log` files are empty.
- Attachment operations now declare explicit setup-time `RenderAttachmentIntent` values. Directional shadow, environment sky, and tonemap declare `Clear/Store`; opaque color and transparent color declare `Load/Store`; opaque depth declares `ClearThenLoad/Store`; transparent depth declares `ReadOnlyLoad/Store`. Named access-chain diagnostics include both load and store intent, and topology caching includes the intent values.
- `RenderGraphResourcePlanner` rejects uninitialized loads, loads after any discarded attachment operation, clear without write access, plain clear plus read access, invalid load/read/write masks, writes through read-only depth, incomplete intent, non-attachment intent, and mismatched intent within one pass/resource. `StaticMeshPass` keeps a clear-only opaque work item when depth must be initialized but no eligible draw or fallback exists, while zero-draw transparent work remains inactive. `DirectionalShadowPass` still clears its graph-declared map when it has no casters.
- Pass culling is isolated in the pure compile-time `RenderGraphPassCullingPlanner`. Focused behavioral tests prove that output ownership retains its resource-producer chain, side-effect passes retain explicit predecessors, and unused producers are removed. Attachment validation, culling, empty-frame recording contracts, and generated Development compilation pass in the `169/169` focused rendering suite with zero generated-build errors.
- Runtime access diagnostics now show `SceneColor` as `Clear/Store -> Load/Store -> Load/Store -> ShaderRead`, `FrameDepth` as `ClearThenLoad/Store -> ReadOnlyLoad/Store`, `DirectionalShadowMap` as `Clear/Store -> ShaderRead`, and `FrameColor` tonemap output as `Clear/Store`. Full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU scene-smoke runs, zero skips or CPU fallbacks, and all six discovered `vk_validation.log` files empty.
- `RenderGraphResourceLifetimePlanner` now derives inclusive first/last compiled-pass intervals after pass culling and work-item preflight. It includes only active accesses to non-imported transient textures, validates that active passes are a unique ordered subset of the compiled order, and exposes interval count plus peak simultaneous live count through logs and Tracy counters. Focused tests cover overlapping and non-overlapping intervals, imported texture exclusion, culled accesses, zero-work passes, and invalid active-pass order.
- Current Development, Production, and RHIVulkanTesting diagnostics report three intervals with peak live count `3`: `SceneColor [2..5]`, `FrameDepth [3..3]`, and `DirectionalShadowMap [1..3]`. The focused rendering suite passes `179/179`; the regenerated Development workspace builds with zero errors; and full Debug runtime validation passes all four GPU profiles with zero skips/fallbacks and all six discovered `vk_validation.log` files empty. Physical image aliasing remains deliberately disabled, so Milestone 8 is complete without changing Vulkan allocation, descriptor, barrier, or deferred-disposal ownership.

### Acceptance Criteria

- Scene color, frame depth, and directional shadow map follow the same resource-planning model.
- Render passes contain fewer manual layout transitions.
- Active transient texture lifetimes are observable without enabling physical image aliasing.
- Full runtime validation remains green with empty active Vulkan validation logs.

---

## Milestone 9 - Automated Visual And Viewport Validation

**Goal:** Catch visual regressions such as Y-flip, blank first SceneView frame, viewport flicker, missing model children, and broken material import earlier.

### TODO

- [x] Add bounded screenshot or image-summary validation.
  - [x] Capture a deterministic standalone runtime smoke frame.
  - [x] Check nonblank final color output.
  - [x] Check nonblank depth output.
  - [x] Track coarse luminance/color statistics rather than brittle pixel-perfect output.
- [x] Add editor viewport smoke validation.
  - [x] Verify first SceneView frame presents without switching tabs.
  - [x] Verify SceneView and GameView orientation.
  - [x] Verify resize generation and shared-texture pacing state.
- [x] Add import/reimport validation fixtures.
  - [x] Generated model children are present.
  - [x] Generated material texture refs resolve.
  - [x] Generated scene loads through `SceneAssetLoader`.
- [x] Add profiler workflow checks.
  - [x] Keep `open_tracy_profiler.bat` documented.
  - [x] Add a short profiler-enabled manual run recipe for model-scene profiling.

### Completion Notes

- `--visual-summary` is an explicit scene-smoke option owned by `RuntimeSmokeOptions`; it captures the final effective smoke frame and turns a missing or failed capture into a nonzero smoke result.
- Shared rendering inserts `RenderOutputReadbackPass` after the active `FrameColor` and published primary `FrameDepth` producers and before `FinalOutputPass`. The pipeline publishes depth semantically rather than looking up the `"FrameDepth"` debug name, and capture-only processes add transfer-source usage to that graph-owned D32 image.
- The pass declares transfer-read access for both resources and records only the backend-neutral `RenderCommandList.CopyImageToBuffer2D` contract. Its explicit image-aspect argument reaches the shared native command stream; Vulkan implements the color and depth copies with `vkCmdCopyImageToBuffer` and invalidates non-coherent readback allocations before CPU mapping.
- One submission and one bounded buffer produce the atomic schema-2 JSON artifact. Existing final-color metadata and statistics remain at the root; the required nested D32 result records finite/normalized/written/clear depth coverage, extrema/average, a 16-bin histogram, a 4x4 spatial grid, and independent pass/fail checks. The combined capture limit remains 256 MiB.
- `validate_runtime.bat` requests fresh visual summaries for scene-mode Development and Production runs only, rejects stale/missing/mismatched/failed schema-2 artifacts, validates depth dimensions/format/counts/distribution shape, and records successful artifact paths in its machine-readable summary.
- `--editor-viewport-smoke` now opens a real Avalonia/Vulkan host with SceneView active, waits for its first accepted output, resizes the live window, waits for an advanced resize generation and changed physical output size, activates GameView, and waits for its first accepted output. The harness uses explicit presentation observations rather than timing sleeps and writes a version 1 JSON artifact.
- SceneView and GameView use distinct surface roles. Diagnostics are emitted only after Avalonia accepts the imported image, marks its ticket presented, and reports consumption. The smoke contract checks the Vulkan compositor reflection (`scaleY = -1` about the visual center), nonzero output, frame consumption, first-Scene-before-Game order, and resize generation/size changes.
- Shutdown now closes and detaches the smoke window before the desktop lifetime stops the engine. `RenderSurface` drains in-flight ticket waits before native device removal, preventing an Avalonia continuation from calling into a destroyed Vulkan device.
- Scene-mode `validate_runtime.bat` keeps the regular Editor kernel smoke and adds this host smoke with a 30-second harness timeout plus a 45-second process bound. Summary schema 4 records the viewport run count and per-profile artifact/log/exit/pass status.
- Focused `EditorViewportSmokeStateTests` pass `4/4`; D32 summary, graph-transition, descriptor, RHI source-contract, deterministic reimport, and profiler workflow coverage bring the complete rendering suite to `199/199`. A direct Development GPU smoke produces a passing 1280x720 schema-2 artifact with all `921600` depth values finite and normalized, `506252` written values, `415348` clear values, and an empty `vk_validation.log`.
- Full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU scene-smoke runs, one additional passing real-host Editor viewport smoke, zero skips/fallbacks, two passing 1280x720 schema-2 color/depth summaries, one passing viewport artifact, and all six discovered `vk_validation.log` files empty.
- `ModelReimportValidationFixture_ReimportsIndexesAndLoadsGeneratedScene` builds a temporary package-owned glTF workflow around a stable `.arismodel` root. It verifies one scene, one mesh, one material, and three physical generated textures are emitted and indexed; four material bindings resolve through typed `Texture2D` lookup because metallic-roughness and occlusion share the packed texture; and `SceneAssetLoader` both inspects and loads the generated mesh/material refs into ECS.
- The same fixture reimports twice and requires identical child GUID order plus byte-identical `.meta` sidecars, keeps every generated source under its `Assets/Generated` output root and outside `.arisen`, and confirms foreign-source generated metadata blocks a subsequent reimport before output is touched.
- `Profiling.md` now documents the engine-bundled viewer command, build/log/output locations, an unbounded Development Lantern-scene capture recipe, the separate Editor reimport workflow, and the concrete zone/plot groups to inspect. It explicitly distinguishes already-generated runtime scene loading from Editor-only glTF reimport.
- Model planning, emission, reimport/invalidation, runtime scene activation, and scene source loading now expose coarse Tracy zones outside render recording and per-entity loops. `ModelImport.*` and `SceneLoad.*` plots report bounded generated-child, warning, invalidation, entity, renderer, light, and environment counts.
- `open_tracy_profiler.bat --config Release --no-pause` was verified against the bundled Tracy `0.11.2` source: the viewer rebuilt, launched responsively from `Arisen/Projects/TracyProfiler/Release/tracy-profiler.exe`, and was closed cleanly after verification. Profiling contract coverage passes `56/56`.
- Final full Debug runtime validation passes Editor, Development, Production, and RHIVulkanTesting with four GPU scene-smoke runs, one real-host Editor viewport smoke, zero skips/fallbacks, two passing schema-2 color/depth summaries, and all six discovered `vk_validation.log` files empty. Milestone 9 and this roadmap are complete.

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

## Roadmap Complete

All nine milestones in this production model-scene roadmap are complete.

The next roadmap should be derived from the current architecture and game goal rather than extending this completed checklist. Its first prioritization pass should revisit the deferred items alongside the broader requirements of a production open-world RPG: world partition/streaming, cooked scene data, asset build/deployment, gameplay-facing ECS authoring, animation/skinning, terrain/foliage, scalable lighting/shadows, and editor workflows.

Keep this file only until that successor roadmap is written and reviewed; then delete this completed roadmap so there is one active next-TODO source of truth.
