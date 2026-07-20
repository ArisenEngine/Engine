# Arisen Production Scene Rendering: Next TODO Roadmap

**Date:** 2026-07-14
**Scope:** Next implementation plan after completing the scene-rendering vertical slice.  
**Primary goal:** Move from correct static-mesh scene smoke rendering to attractive, authorable, production-facing scenes.

---

## Current State Summary

The renderer now has a solid vertical slice:

- Package-driven boot, profile generation, runtime/editor surfaces, Vulkan RHI startup, RenderGraph execution, task-graph command recording, and runtime smoke validation are working.
- Static mesh rendering uses scene-extracted ECS data, camera snapshots, depth, material pipeline-state batching, submesh ranges, material slots, setup-time GPU resource preparation, and pass-owned object-buffer uploads.
- ShaderLab, plain HLSL, Texture2D, Material, and StaticMesh assets load through stable GUIDs, cooked payloads, generated typed asset refs, and setup-time dependency invalidation.
- The first glTF mesh importer supports JSON `.gltf` triangle mesh data with external or embedded buffers, POSITION plus optional NORMAL/TANGENT/TEXCOORD_0/COLOR_0, unsigned indices, synthesized non-indexed streams, and material-slot extraction.
- Editor asset inspection can inspect scene, material, mesh, and shader assets without mutating generated `.arisen` files as source.
- Validation is repeatable with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1`.

The engine can now render a source-authored Utah teapot showcase with three explicit materials, a CC0 marble texture, emissive material factors, optional emissive texture import/sampling, camera-aware GGX direct lighting, authored directional/point/spot lighting, a first directional shadow map, a procedural environment, HDR scene color, explicit tonemapping, deterministic render queues, frustum culling, graph-owned scene-color allocation/barriers, and visual diagnostics. The remaining production-facing choice is selecting a broader glTF/GLB showcase model that exercises the completed generated-child scene path.

---

## Guiding Rules For This Roadmap

1. **Author scenes as assets, not code-only smoke setup.** Package game code may spawn test entities, but production scenes should come from source assets with stable GUIDs.
2. **Keep runtime references GUID-backed and typed at authoring boundaries.** User code and generated refs should avoid raw GUID literals where possible.
3. **Keep extraction data-only.** ECS and scene extraction can build frame snapshots, but RenderGraph recording consumes prepared spans, compact ids, and unmanaged constants only.
4. **Do not let importers bypass cooked assets.** Model, material, texture, and scene importers should emit or reference the same cooked asset path used by runtime rendering.
5. **Reach beauty through simple, layered correctness.** Camera, transforms, PBR parameters, lights, sky, tonemapping, and shadows should arrive in clear slices instead of a monolithic renderer rewrite.
6. **Editor workflows must stay package-aware.** UI may edit package/workspace `Assets` roots and `.meta` sidecars, not generated `.arisen` outputs.
7. **Validate every visible slice.** Each milestone should keep fast validation green and use runtime scene smoke when rendering behavior changes.

---

## Milestone 1 - Scene Asset And Scene Loading

**Goal:** Replace code-only smoke scene setup with a first authored scene asset that spawns cameras and mesh renderers.

### TODO

- [x] Define a minimal scene source asset format.
  - [x] Stable entity ids or names for diagnostics.
  - [x] Transform data.
  - [x] Camera data.
  - [x] Mesh renderer data using `AssetRef<MeshSourceAsset>` and optional `AssetRef<MaterialSourceAsset>`.
- [x] Add scene asset metadata and generated typed refs.
  - [x] Add a `SceneSourceAsset` marker type.
  - [x] Generate typed scene refs from package `Assets/**/*.meta`.
  - [x] Keep dependency-only sidecars out of generated user refs.
- [x] Implement a scene loader boundary.
  - [x] Load scene source/cooked data during setup or package startup, not during render pass recording.
  - [x] Spawn ECS entities/components from scene data.
  - [x] Preserve existing code-created smoke scene as a fallback or test path.
- [x] Add validation.
  - [x] Unit-test scene parsing and missing asset diagnostics.
  - [x] Add or update runtime scene smoke to load the authored scene asset.

### Acceptance Criteria

- A package can load a scene by generated typed ref and spawn multiple visible renderable entities.
- Render worker threads never read scene source assets or the asset database.
- Missing mesh/material/camera references fail with clear scene asset diagnostics.

### Completion Notes

- `SceneSourceAsset` is now a typed asset-ref marker, and `.arisenscene` / `.scene` files import as asset type `Scene` through the runtime asset database and editor importer.
- `ArisenBuildTool` emits generated `AssetRef<SceneSourceAsset>` constants for package-owned scene assets while preserving dependency-only filtering.
- `SceneAssetLoader` is the first reusable setup-time scene loading boundary. It parses a narrow YAML scene source format, validates mesh/material GUIDs against `IAssetDatabase`, adds `NameComponent`, `TransformComponent`, `CameraComponent`, and `MeshRendererComponent` data to an `EntityManager`, and returns clear diagnostics for missing references.
- `PackageGame` now owns `Assets/Scenes/SmokeScene.arisenscene` and loads it by generated typed ref. The previous code-created smoke scene remains as a fallback if the scene asset or asset database is unavailable.
- Scene loading happens during deferred package setup before render extraction. RenderGraph pass recording still consumes only prepared frame snapshots and does not read scene source assets.
- Focused validation covers generated scene refs and scene loader success/missing-mesh diagnostics.

---

## Milestone 2 - Model Import As A Production Asset Flow

**Goal:** Make importing a real model produce usable mesh/material/texture assets instead of only low-level mesh payloads.

### TODO

- [x] Extend glTF importer scope deliberately.
  - [x] Support `.glb` container files.
  - [x] Import node transforms into the current static mesh bake path.
  - [x] Preserve primitive-to-material slot mapping.
  - [x] Keep skins, animation, morph targets, and advanced extensions out of this slice unless required by the sample asset.
- [x] Add first model asset concept if needed.
  - [x] Decide whether imported glTF scenes become `SceneSourceAsset`, `ModelSourceAsset`, or generated scene plus mesh/material assets.
  - [x] Keep generated/imported child assets package-aware and stable across reimport.
- [x] Import glTF material basics.
  - [x] Plan base color factor and base color texture from glTF materials.
  - [x] Plan metallic factor and roughness factor from glTF materials.
  - [x] Plan normal texture metadata for the PBR milestone.
  - [x] Emit package-owned `.arismaterial` files and supported external `.ppm` texture children from the model importer.
  - [x] Add PNG/JPEG image texture cooker support and generated external image emission.
  - [x] Add embedded glTF image extraction for buffer-view and data-URI image payloads.
- [x] Add editor/import diagnostics.
  - [x] Show imported mesh count, material count, texture refs, and unsupported feature warnings.

### Acceptance Criteria

- A real glTF or GLB model can be placed in package `Assets` and appear through the scene rendering path.
- Reimport keeps stable GUIDs for generated/imported assets where identity is preserved.
- Unsupported model features are explicit warnings or errors, not silent visual loss.

### Completion Notes

- The static mesh importer now supports binary `.glb` containers for the same first static mesh scope as JSON `.gltf`: GLB 2.0 header validation, JSON chunk parsing, embedded BIN chunk consumption, triangle primitives, supported static vertex attributes, index decoding, and compact material-slot extraction.
- glTF scene/node traversal now respects the selected scene, root nodes, parent/child transforms, matrix/TRS node transforms, and repeated mesh references. For the current static mesh asset boundary, node transforms are baked into cooked vertex positions, normals, and tangents.
- Primitive-to-material mapping remains compact and stable across `.gltf`, `.glb`, and scene-node traversal by preserving glTF primitive material indices as cooked material slots.
- `.glb` and node traversal remain mesh-source paths in this slice. Generated model/scene assets, material/texture import, skins, animation, morph targets, and advanced extensions stay explicit future work.
- The first model identity decision is now explicit: production multi-object imports should use a `ModelSourceAsset` authoring/import descriptor (`.arismodel` / `.model`) as the stable root identity, while generated scene/mesh/material/texture children remain package-owned assets with deterministic child identity. Raw `.gltf` / `.glb` files continue to index as `Mesh` for the current static mesh cooker until the generated-child model importer is implemented.
- The first generated-child identity contract is implemented in `com.arisen.core`. `GeneratedAssetIdentity` derives deterministic child GUIDs from source GUID, package id, child kind, and child key, and writes `Generated` provenance metadata for future generated sidecars. This gives reimport a stable identity rule before we start writing generated child assets into package `Assets`.
- `GltfModelImportPlanner` is now the setup/import planning boundary for production model import. It reads glTF/GLB JSON, plans package-owned generated scene/mesh/material/texture child identities, extracts base color factor/texture, emissive factor plus `KHR_materials_emissive_strength`, metallic factor, roughness factor, normal texture references, and emissive texture references, and reports unsupported skins, animations, morph targets, alpha modes, and occlusion textures as warnings. It does not yet emit `.arismaterial`, texture, mesh, or scene child source files.
- `GltfModelImportEmitter` now turns the plan into package-owned generated `.arismaterial` files plus `.meta` sidecars with generated provenance. When a glTF texture references an external `.ppm`, `.png`, `.jpg`, or `.jpeg`, the emitter copies it into the generated texture output folder, writes deterministic `Texture2D` metadata, and binds it from the generated material. `Texture2DAssetCooker` decodes PNG/JPEG sources through the package-owned `StbImageSharp` dependency into the same cooked RGBA8 texture payload. Embedded PNG/JPEG image payloads are extracted from glTF data URIs and `bufferView + mimeType` sources, including GLB BIN chunks, into generated package-owned texture files.
- The editor mesh inspector now surfaces glTF/GLB model import diagnostics directly from `GltfModelImportPlanner`: source/package identity, planned scene/mesh/material/image child counts, texture-reference counts, generated child GUIDs, material factor previews, texture refs, and unsupported feature warnings.

---

## Milestone 3 - First PBR Material And Normal Mapping

**Goal:** Move from the smoke material shader to a simple but real lit material model.

### TODO

- [x] Define the first standard material contract.
  - [x] BaseColor texture and factor.
  - [x] Metallic and roughness scalar values.
  - [x] Normal texture slot.
  - [x] Optional emissive factor if needed by sample content.
  - [x] Optional emissive texture if needed by sample content.
- [x] Add a standard lit ShaderLab shader.
  - [x] Use tangent-space normal mapping from cooked mesh tangents.
  - [x] Keep variant policy explicit for normal-map on/off and alpha mode.
  - [x] Keep specialization constants reserved for small non-layout values.
- [x] Update material GPU data.
  - [x] Prepare bindless texture/sampler constants for normal maps.
  - [x] Upload material scalar/vector data without per-draw allocations.
- [x] Add validation.
  - [x] Unit-test material contract validation for the standard lit shader.
  - [x] Runtime smoke draws at least one normal-mapped material when assets are available.

### Acceptance Criteria

- The renderer can draw a textured, normal-mapped, metallic/roughness mesh using the standard material contract.
- Materials with missing optional textures use deterministic defaults.
- Pass recording remains free of material name lookup, asset loading, and shader compilation.

### Progress Notes

- `MaterialTextureSlots.Normal` and `MaterialTextureSlots.Emissive` are now shared material slot names, and glTF generated material emission uses the shared slot constants instead of raw slot strings.
- `com.arisen.generic-renderpipeline` now owns `GenericRP/StandardLit`, a ShaderLab source asset with a `MaterialContract` for BaseColor, Normal, MetallicFactor, RoughnessFactor, BaseColorFactor, and EmissiveFactor. It declares explicit `USE_NORMAL_MAP`, `ALPHA_TEST`, and `USE_TRIPLANAR` keyword variants and reserves specialization constants for future small non-layout values.
- The package also owns a deterministic flat `DefaultNormal` texture and `StandardLitMaterial` authored material. Generated Editor-profile asset refs expose `StandardLitMaterial`, `StandardLitShader`, and `DefaultNormalTexture`.
- Focused tests validate the package-owned StandardLit shader contract, keyword policy, opaque render state, material keyword selection, normal texture binding, emissive factor, and PBR scalar/vector properties.
- `StaticMeshPass` now prepares the StandardLit contract during setup: base-color factor, metallic factor, roughness factor, emissive factor, base-color image/sampler indices, normal image/sampler indices, optional emissive image/sampler indices, object-buffer index, and object index. Emissive factor and emissive texture indices live in the existing per-object storage-buffer record so the already-packed draw push constants do not grow. Those values are resolved during material/resource setup and command recording only pushes unmanaged constants.
- `GenericRenderPipelinePackage` registers `StandardLitMaterial` as the default material, while legacy smoke material assets remain available as compatibility content.
- `com.arisen.generic-renderpipeline` now owns `Assets/Meshes/FacetedCrystal.obj`, and the default scene smoke path uses two faceted crystal mesh instances instead of the old flat quad composition. This gives a more visible authored mesh target before broader glTF model scene emission is implemented.
- glTF generated material emission now writes `EmissiveFactor` from glTF `emissiveFactor` plus `KHR_materials_emissive_strength` when present, and emits optional `Emissive` Texture2D refs for supported glTF emissive image sources. StandardLit multiplies the emissive factor by the emissive texture in linear HDR scene color, with bindless indices carried through object data instead of draw push constants.
- Full runtime validation passed with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` after the StandardLit and faceted-crystal runtime switch.

---

## Milestone 4 - Lights, Environment, And Tonemapping

**Goal:** Add the lighting minimum needed for an attractive scene.

### TODO

- [x] Define light component data.
  - [x] Directional light.
  - [x] Point light with range/intensity.
  - [x] Optional spot light if needed by the sample scene.
- [x] Extract light snapshots.
  - [x] Use contiguous component pools.
  - [x] Upload compact light data during render setup.
  - [x] Keep per-frame limits explicit and diagnosed.
- [x] Add first environment lighting.
  - [x] Sky color or skybox texture.
  - [x] Ambient term or first image-based lighting placeholder.
- [x] Add tonemapping pass.
  - [x] Render scene color in an HDR-capable target.
  - [x] Apply tonemap to the final output before presentation.
  - [x] Promote HDR scene-color allocation and layout transitions into the RenderGraph resource planner.

### Acceptance Criteria

- A scene can be lit by at least one directional light plus ambient/environment contribution.
- Lighting data reaches shaders through setup-time prepared buffers/constants.
- Runtime scene smoke can show a visibly lit mesh rather than only smoke diffuse shading.
- The generic pipeline preserves scene lighting in HDR scene color before a dedicated output pass maps it to the presentation target.

### Progress Notes

- `DirectionalLightComponent` is the first light component. It stores direction-to-light, RGB color, intensity, ambient intensity, and an enabled flag as scene-authored ECS data.
- `.arisenscene` files can now author a `DirectionalLight` block. `SceneAssetLoader` validates and spawns light entities alongside cameras and mesh renderers, and reports directional-light counts in its load diagnostics.
- `RenderSubsystem` extracts enabled directional lights from contiguous ECS component pools into `FrameArena` and exposes them through `RenderFrameSnapshot` / `RenderContext`. RenderGraph worker threads consume copied snapshot spans instead of ECS pools.
- The current StandardLit contract accepts one directional light per surface/frame. `DirectionalLightSnapshotExtractor` scans the contiguous pool without allocation, accepts the first enabled light, and reports source, enabled, accepted, and dropped counts. `RenderSubsystem` plots those counts in Tracy and emits a throttled warning when enabled lights exceed the explicit limit.
- `GenericRenderPipeline` selects the first valid directional light, falling back to deterministic default lighting when no authored light is present.
- `StaticMeshPass` prepares compact light constants during setup and folds them into existing per-draw push constants. `StandardLit` now uses scene-provided directional light color/intensity plus an ambient term instead of a fully hardcoded light direction.
- `PointLightComponent` adds authored local light data with transform-owned position, RGB color, intensity, range, and enabled state. `.arisenscene` files can now author `PointLight` blocks; scene loading, scene inspection, hierarchy summaries, and inspector views all report point-light data.
- `RenderSubsystem` extracts enabled point lights from contiguous ECS component/entity arrays, resolves positions through the transform pool, accepts up to four per frame into `FrameArena`, and plots source/enabled/accepted/dropped/missing-transform counts in Tracy. Render worker threads consume the copied snapshot span rather than ECS pools.
- `StaticMeshPass` appends point-light records after per-object records in the existing bindless object-data upload buffer and passes point-light start/count through push constants. `StandardLit` reads those records in the fragment stage, applies range falloff, and evaluates the same GGX direct-light BRDF used by the directional light without adding a new descriptor binding in this slice.
- `SpotLightComponent` adds authored cone light data with transform-owned position/direction, RGB color, intensity, range, inner/outer cone angles, and enabled state. `.arisenscene` files can now author `SpotLight` blocks; scene loading, scene inspection, hierarchy summaries, and inspector views all report spot-light data.
- `RenderSubsystem` extracts enabled spot lights from contiguous ECS component/entity arrays, resolves position and `+Z` forward direction through the transform pool, accepts up to four per frame into `FrameArena`, and plots source/enabled/accepted/dropped/missing-transform counts in Tracy.
- `StaticMeshPass` appends spot-light records after point-light records in the existing bindless object-data upload buffer. Point and spot counts are packed into the existing local-light count push-constant slot, so spot support does not widen draw push constants or add a descriptor binding. `StandardLit` evaluates spot cones with range falloff and the same GGX direct-light BRDF used by directional and point lights.
- Full runtime validation passed after point/spot local-light support and cleanup of the temporary GenericRP debug console print; Editor, Development, Production, and RHIVulkanTesting smoke runs passed with empty active Vulkan validation logs.
- `SceneEnvironmentComponent` authors sky, horizon, ground, and ambient colors plus independent sky/ambient intensities. Scene loading and contiguous snapshot extraction enforce one active environment per surface/frame and report source, enabled, accepted, and dropped counts through Tracy and throttled overflow warnings.
- `EnvironmentSkyPass` is the first visible environment renderer. It cooks a package-owned HLSL shader and prepares its graphics pipeline during setup, then records a fullscreen procedural gradient through `RenderCommandList` without vertex buffers, asset lookup, or shader compilation on worker threads. `StandardLit` consumes the same environment snapshot as colored ambient lighting. This is intentionally a color-gradient placeholder; skybox textures and image-based lighting remain future work.
- Before introducing HDR scene color, the default authored scene was deliberately promoted from the later sample-scene milestone into a pre-HDR visual slice. It now frames a package-owned Utah teapot on a lathed pedestal and ground plane, uses three explicit material GUIDs, and maps ambientCG's continuous CC0 Marble 021 texture through a `USE_TRIPLANAR` StandardLit variant.
- StandardLit now receives camera world position as a setup-prepared push constant and reconstructs per-vertex world position from the existing object buffer. Its direct-light path uses bounded Cook-Torrance/GGX shading; command recording still consumes only prepared unmanaged constants and bindless indices.
- `RenderGraphTexture` is now the first graph-owned transient texture allocation path. The generic pipeline requests `SceneColor` from the RenderGraph as a reusable `FORMAT_R16G16B16A16_SFLOAT` color-attachment/sampled image, and the graph owns image/view/sampler allocation plus bindless descriptor registration.
- `EnvironmentSkyPass` and `StaticMeshPass` now write linear color into scene color. `TonemapPass` samples scene color with the required screen-to-texture V-coordinate correction, applies exposure plus an ACES-style filmic curve, and writes the current swapchain/editor shared output.
- The RenderGraph now declares, allocates, and tracks `SceneColor` as a transient texture resource. The resource planner records scene-color barriers before pass recording, including first-use transitions, color-attachment writes, shader-read tonemap sampling, and the previous-frame state carried by the reusable physical image. `EnvironmentSkyPass` and `TonemapPass` no longer issue manual scene-color layout transitions.
- Full runtime validation passed with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` after HDR scene color and tonemapping.
- Full runtime validation passed again after graph-owned HDR scene color allocation/layout transitions; Editor, Development, Production, and RHIVulkanTesting smoke runs passed with empty active Vulkan validation logs.

---

## Milestone 5 - Shadows And Render Queue Hardening

**Goal:** Add the first visible depth-based shadow path and make opaque rendering scale beyond a demo.

### TODO

- [x] Add directional shadow map support.
  - [x] Shadow camera/cascade policy for the first slice.
  - [x] Shadow depth RenderGraph pass.
  - [x] Shadow map sampling in the standard lit shader.
- [x] Harden render queues.
  - [x] Opaque queue sorting.
  - [x] Alpha-test queue policy.
  - [x] Transparent queue policy documented even if not implemented.
- [x] Add culling foundation.
  - [x] Frustum culling from bounds and camera snapshot.
  - [x] Keep culling output as compact visible draw spans.

### Acceptance Criteria

- A simple scene can cast and receive directional shadows.
- Draw submission order is deterministic and documented.
- Culled entities do not produce mesh draw commands.

### Progress Notes

- `RenderCommandList` / `RHICommandBuffer` now expose a depth-only dynamic-rendering entry. The native bridge records zero color attachments plus one depth attachment, so render passes can stay backend-neutral instead of calling Vulkan-specific APIs.
- The first implementation used `DirectionalShadowTarget` as the sampled shadow-depth owner. Production Model Scene Milestone 8 later replaced it with a settings-sized graph-owned `RenderGraphTexture`, which now owns the `FORMAT_D32_SFLOAT` image/view/sampler, bindless descriptors, state tracking, replacement, and deferred-safe disposal.
- `DirectionalShadowPass` replays the prepared `MeshDrawCommand` span through package-owned `DirectionalShadow.hlsl` before the lit pass. The first shadow policy is a single fixed orthographic showcase slice centered around the teapot/pedestal/ground scene; cascades, scene-bounds fitting, and slope-scaled raster bias remain future quality work.
- `StaticMeshPass` appends shadow model-view-projection columns plus compact shadow sampling parameters to its existing per-object storage-buffer records. This feeds `StandardLit` without increasing push-constant size, and command recording still consumes prepared unmanaged constants plus prepared draw spans.
- `StandardLit` samples the shadow map as a regular bindless texture and performs a small manual PCF depth comparison. Only direct lighting is attenuated; environment and ambient lighting remain unshadowed for stable first-slice visuals.
- `GenericRenderPipeline` now requests the concrete sampled `DirectionalShadowMap` transient, records shadow depth before sky/static lighting, and plots shadow map size/format/enabled state plus pass draw count in Tracy. The depth-write and shader-read declarations drive its barriers.
- Full runtime validation passed with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1`; Editor, Development, Production, and RHIVulkanTesting smoke runs passed, and the active `vk_validation.log` files were empty.
- `RenderQueuePolicy` now classifies prepared material draws into opaque (`2000`), alpha-test (`2450` via the `ALPHA_TEST` shader keyword), and transparent (`3000` via blend-enabled render state) queues. At this roadmap's completion, `StaticMeshPass` sorted all three through one deterministic queue and transparency still lacked a dedicated depth policy. That limitation was later closed by Production Model Scene Milestone 7: setup now partitions a camera-depth-sorted transparent span into a separate test-on/write-off pass while preserving the opaque worker-recording path and adding queue-specific Tracy counters.
- `StaticMeshFrustumCuller` is the first bounds-based culling foundation. `GenericRenderPipeline` evaluates extracted `StaticMeshRenderItem` records against the primary camera view-projection during setup, prefers authored scene bounds when present, falls back to cooked mesh bounds when scene bounds are missing, and only expands visible items into the compact `MeshDrawCommand` array consumed by shadow/static mesh passes. Culling is disabled when no camera exists so partially-authored editor scenes do not disappear through identity clip-space assumptions. Tracy plots source, visible, culled, and emitted draw counts.

---

## Milestone 6 - Editor Scene Authoring Workflow

**Goal:** Let users assemble and iterate on scenes inside the editor instead of editing smoke code.

### TODO

- [x] Add scene asset inspection.
  - [x] Show entities, transforms, cameras, renderers, and referenced assets.
  - [x] Surface scene parse/load diagnostics.
- [x] Add first hierarchy view.
  - [x] Select scene entities.
  - [x] Route selection to the existing inspector.
- [x] Add transform editing.
  - [x] Edit translation/rotation/scale.
  - [x] Write source scene asset changes under allowed `Assets` roots only.
- [x] Add viewport selection feedback if practical.

### Acceptance Criteria

- A user can inspect and edit the first authored scene asset from the editor.
- Edits affect source scene assets or `.meta` sidecars, never generated `.arisen` files.
- Runtime/editor reload paths remain setup-time and render-safe.

### Progress Notes

- `SceneAssetLoader.InspectScene` is now the read-only scene inspection boundary. It parses the same `.arisenscene` source format as runtime scene loading, validates mesh/material references through `IAssetDatabase`, and returns entity/component/reference diagnostics without spawning ECS entities.
- The editor inspector recognizes `Scene` assets and displays scene summary counts, entity transform rows, camera data, mesh renderer submesh/bounds/reference data, directional light data, environment data, and parse/reference diagnostics.
- Selecting a `.arisenscene` asset now mirrors its authored entities into the Hierarchy panel without spawning ECS entities. Selecting one of those source-scene entities routes to the existing Inspector and shows read-only transform, camera, mesh renderer, directional light, environment, and reference diagnostics for that entity.
- Source-scene entity `Position`, `Rotation`, and `Scale` fields are now editable through the Inspector for scene files under allowed workspace/package `Assets` roots. Edits execute an undoable editor command, use `SceneAssetLoader.UpdateEntityTransform` to mutate the YAML scene through YamlDotNet's node model, and reject generated or non-asset paths before writing.
- Scene view selection feedback is implemented as an editor UI overlay, not as a runtime render pass or temporary ECS debug entity. `SceneViewModel` passes the shared `SelectionService` into `EditorViewportViewModel`; when a mirrored source-scene entity is selected, the viewport shows a compact selected-entity label with component summary and position, plus a projected marker using the first inspected scene camera. This keeps authored-scene feedback lightweight and outside RenderGraph/RHI hot paths.
- Focused tests cover successful scene inspection and missing-reference diagnostics. Scene inspection remains source/editing tooling; runtime rendering still loads scenes during setup/package startup and RenderGraph recording does not read scene source files.

---

## Milestone 7 - Sample Beautiful Scene (Pulled Forward Before HDR)

**Goal:** Build a small target scene that proves the renderer is becoming visually useful.

### TODO

- [x] Add a package-owned sample scene asset.
  - [x] One imported real mesh or multi-object model.
  - [x] At least two materials.
  - [x] One camera.
  - [x] One directional light.
- [x] Add visual diagnostics.
  - [x] Log mesh/material/light counts.
  - [x] Plot draw count, visible draw count, light count, and shadow pass cost in Tracy.
- [x] Keep validation bounded.
  - [x] One-frame or two-frame runtime smoke for CI.
  - [x] Optional longer visual run for manual profiling.

### Acceptance Criteria

- Running the default scene smoke renders an authored scene that is materially better than the old smoke quads.
- Tracy explains extraction, setup, pass recording, submission, and GPU-facing resource counts.
- Full runtime validation remains green.

### Progress Notes

- The default `SmokeScene.arisenscene` is now the Marble Teapot Showcase: one Utah teapot, one stepped pedestal, one ground plane, three explicit materials, one three-quarter camera, one warm directional light, and one blue-hour environment.
- `com.arisen.packagegame` owns the generated OBJ sources and stable `.meta` GUIDs. The teapot uses a 12-segment Bezier-patch tessellation generated with three.js; package-local third-party notices preserve the generator license and ambientCG CC0 texture provenance.
- `ShowcaseMarble`, `ShowcaseCharcoal`, and `ShowcaseGround` select a StandardLit `USE_TRIPLANAR` variant so the marble source does not repeat independently across teapot patch UV islands. They intentionally use the flat normal binding until OBJ tangent generation or a production glTF tangent stream is available. The marble and charcoal materials also carry subtle `EmissiveFactor` values to keep the showcase readable under the blue-hour lighting setup.
- The final pre-HDR composition used a lower, tighter three-quarter camera, a far-range ground plane, white marble against cool graphite/floor materials, and explicit sRGB encoding only when the presentation target was eight-bit UNORM. HDR scene color and tonemapping now own that output mapping; native sRGB targets retain hardware encoding, eight-bit UNORM outputs receive explicit encoding in `TonemapPass`, and floating-point outputs stay linear.
- RenderDoc verification confirmed that the teapot, pedestal, and ground now bind distinct vertex/index buffers on consecutive draws. The Vulkan executor clears pending vertex bindings after each draw so a prior mesh cannot leak into the next draw, while still allowing multiple bindings to be accumulated for one draw.
- Avalonia's Vulkan opaque-image import uses the opposite vertical row convention from the engine's editor render target. Scene and Game views now correct that once in `ArisenViewportControl` by reflecting the compositor surface visual around its vertical center; the shared RHI viewport and projection conventions remain unchanged.
- RHI viewport coordinates are top-left with positive width/height. The Vulkan executor implements that contract with a negative native viewport height, while `EFrontFace` maps directly to `VkFrontFace` for both static and dynamic state. An additional pipeline-time winding inversion was removed after the first back-face-culled glTF showcase exposed that it discarded the near-facing shell; cameras, shaders, and generic render passes remain backend-agnostic.
- The sample-scene content and direct-light GGX slice were completed before HDR by explicit visual-priority decision. HDR scene color, fixed exposure, and tonemapping now live as a dedicated output pass rather than being hidden inside the material shader.
- Visual diagnostics now report the setup-time scene shape instead of requiring a RenderDoc capture for every check. `RenderSubsystem` logs and plots camera, static-mesh, directional-light, environment, and output counts; `GenericRenderPipeline` logs visible/cull/material counts and plots registered/prepared materials, accepted light/environment count, and visible draw commands; `StaticMeshPass` and `DirectionalShadowPass` plot draw, batch, queue, object-buffer, and shadow-map counters. `DirectionalShadowPass.Prepare` also has an explicit Tracy zone, while pass recording remains visible through render-graph worker task spans.
- Bounded validation remains the default gate: `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` covers the canonical profiles with a one-frame request that still renders the deferred scene setup path. Longer profiler-enabled runs are manual Tracy sessions launched from an `EnableProfiler: true` profile so CI does not become a visual soak test.

---

## Recommended Immediate Sprint

Completed immediately before this sprint:

1. **Package-owned Marble Teapot Showcase and camera-aware GGX direct lighting.**
2. **HDR scene color and tonemapping.**
3. **First directional shadow map pass and StandardLit shadow sampling.**
4. **Milestone 6 editor scene authoring flow, including viewport selection feedback.**
5. **Optional point-light support with range/intensity.**
6. **Optional emissive material factor support.**
7. **Optional spot-light support using the shared local-light object-buffer path.**
8. **Generated glTF/GLB scene/mesh/material child emission for broader production model scenes.**
9. **Optional emissive texture support through generated glTF materials and StandardLit object data.**

Current roadmap TODOs are complete. Implement next in the following roadmap:

1. **Choose and integrate a production glTF/GLB showcase model that benefits from the completed generated-child scene path.**

Why this order:

- Authored directional lighting, bounded environment extraction, colored ambient lighting, the procedural sky pass, HDR scene color, and explicit tonemapping are complete.
- The scene now has material, light, first directional shadows, sky, output-mapping structure, deterministic queue policy, bounds-based culling, and graph-owned HDR scene-color allocation/barriers.
- Scene asset inspection, hierarchy mirroring, transform editing, viewport selection feedback, visual diagnostics, point lights, spot lights, emissive material factors, optional emissive textures, generated model scene/mesh emission, multi-material model polish, and bounded validation are now in place. Milestone 6 is complete and this roadmap has no unchecked TODO items.
- `GltfModelImportEmitter` now emits generated `.arisenscene` children plus deterministic single-mesh `.gltf` / `.glb` child sources with generated metadata. The mesh child writer strips scene/node transforms out of the mesh source, while the generated scene places mesh entities with decomposed node transforms, so generated scene loading avoids double-applying glTF node transforms. Multi-primitive glTF meshes emit one mesh-renderer entity per primitive, with `FirstSubmeshIndex`, `SubmeshCount: 1`, and that primitive's generated material GUID. Relative external glTF dependencies are copied beside the generated mesh source, and regression tests validate metadata, scene inspection, per-primitive material references, submesh ranges, material slots, and generated GLB mesh cooking.
- Scene setup now uses `RHIStaticMeshResource.CreateDrawCommandsWithMaterialOverride` for extracted scene items with explicit material GUIDs, so generated glTF primitive materials bind by exact material id instead of relying on fragile material-id contiguity. Mesh-only scene items keep the older cooked material-slot offset path for compatibility.
- Full runtime validation passed with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` after generated glTF/GLB scene and mesh child emission; Editor, Development, Production, and RHIVulkanTesting smoke runs passed, and active Vulkan validation logs were empty.
- Full runtime validation passed again with the same command after multi-material glTF/GLB scene polish; Editor, Development, Production, and RHIVulkanTesting smoke runs passed, and active `vk_validation.log` files remained 0 bytes.

The fastest path to continue improving the scene is:

1. Replace the generated OBJ showcase source with a production glTF/GLB model path once we choose a suitable package-owned sample model.
2. Use emissive textures when the chosen sample content includes authored textured emission.

The scene now reads as a small showcase rather than an engine primitive test. This roadmap is complete; continue in [ProductionModelSceneNextTodo.md](ProductionModelSceneNextTodo.md), which focuses on production glTF/GLB sample selection, model reimport workflow, material fidelity, environment lighting, shadow quality, RenderGraph resource ownership, and automated visual validation.
