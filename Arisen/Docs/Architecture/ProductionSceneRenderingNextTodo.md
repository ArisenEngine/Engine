# Arisen Production Scene Rendering: Next TODO Roadmap

**Date:** 2026-07-10  
**Scope:** Next implementation plan after completing the scene-rendering vertical slice.  
**Primary goal:** Move from correct static-mesh scene smoke rendering to attractive, authorable, production-facing scenes.

---

## Current State Summary

The renderer now has a solid vertical slice:

- Package-driven boot, profile generation, runtime/editor surfaces, Vulkan RHI startup, RenderGraph execution, task-graph command recording, and runtime smoke validation are working.
- Static mesh rendering uses scene-extracted ECS data, camera snapshots, depth, material pipeline-state batching, submesh ranges, material slots, setup-time GPU resource preparation, and pass-owned object-buffer uploads.
- ShaderLab, plain HLSL, Texture2D, Material, and StaticMesh assets load through stable GUIDs, cooked payloads, generated typed asset refs, and setup-time dependency invalidation.
- The first glTF mesh importer supports JSON `.gltf` triangle mesh data with external or embedded buffers, POSITION plus optional NORMAL/TANGENT/TEXCOORD_0/COLOR_0, unsigned indices, synthesized non-indexed streams, and material-slot extraction.
- Editor asset inspection can inspect and recook material, mesh, and shader assets without mutating generated `.arisen` files as source.
- Validation is repeatable with `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1`.

The next risk is visual and authoring scale: the engine can render a correct mesh scene, but it does not yet have a scene asset, model import pipeline, PBR material model, lights, environment lighting, shadows, or editor scene authoring flow.

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

- [ ] Define a minimal scene source asset format.
  - [ ] Stable entity ids or names for diagnostics.
  - [ ] Transform data.
  - [ ] Camera data.
  - [ ] Mesh renderer data using `AssetRef<MeshSourceAsset>` and optional `AssetRef<MaterialSourceAsset>`.
- [ ] Add scene asset metadata and generated typed refs.
  - [ ] Add a `SceneSourceAsset` marker type.
  - [ ] Generate typed scene refs from package `Assets/**/*.meta`.
  - [ ] Keep dependency-only sidecars out of generated user refs.
- [ ] Implement a scene loader boundary.
  - [ ] Load scene source/cooked data during setup or package startup, not during render pass recording.
  - [ ] Spawn ECS entities/components from scene data.
  - [ ] Preserve existing code-created smoke scene as a fallback or test path.
- [ ] Add validation.
  - [ ] Unit-test scene parsing and missing asset diagnostics.
  - [ ] Add or update runtime scene smoke to load the authored scene asset.

### Acceptance Criteria

- A package can load a scene by generated typed ref and spawn multiple visible renderable entities.
- Render worker threads never read scene source assets or the asset database.
- Missing mesh/material/camera references fail with clear scene asset diagnostics.

---

## Milestone 2 - Model Import As A Production Asset Flow

**Goal:** Make importing a real model produce usable mesh/material/texture assets instead of only low-level mesh payloads.

### TODO

- [ ] Extend glTF importer scope deliberately.
  - [ ] Support `.glb` container files.
  - [ ] Import node transforms into a scene/model hierarchy.
  - [ ] Preserve primitive-to-material slot mapping.
  - [ ] Keep skins, animation, morph targets, and advanced extensions out of this slice unless required by the sample asset.
- [ ] Add first model asset concept if needed.
  - [ ] Decide whether imported glTF scenes become `SceneSourceAsset`, `ModelSourceAsset`, or generated scene plus mesh/material assets.
  - [ ] Keep generated/imported child assets package-aware and stable across reimport.
- [ ] Import glTF material basics.
  - [ ] Base color factor and base color texture.
  - [ ] Metallic factor and roughness factor.
  - [ ] Normal texture as metadata for the PBR milestone.
- [ ] Add editor/import diagnostics.
  - [ ] Show imported mesh count, material count, texture refs, and unsupported feature warnings.

### Acceptance Criteria

- A real glTF or GLB model can be placed in package `Assets` and appear through the scene rendering path.
- Reimport keeps stable GUIDs for generated/imported assets where identity is preserved.
- Unsupported model features are explicit warnings or errors, not silent visual loss.

---

## Milestone 3 - First PBR Material And Normal Mapping

**Goal:** Move from the smoke material shader to a simple but real lit material model.

### TODO

- [ ] Define the first standard material contract.
  - [ ] BaseColor texture and factor.
  - [ ] Metallic and roughness scalar values.
  - [ ] Normal texture slot.
  - [ ] Optional emissive factor or texture if needed by sample content.
- [ ] Add a standard lit ShaderLab shader.
  - [ ] Use tangent-space normal mapping from cooked mesh tangents.
  - [ ] Keep variant policy explicit for normal-map on/off and alpha mode.
  - [ ] Keep specialization constants reserved for small non-layout values.
- [ ] Update material GPU data.
  - [ ] Prepare bindless texture/sampler constants for normal maps.
  - [ ] Upload material scalar/vector data without per-draw allocations.
- [ ] Add validation.
  - [ ] Unit-test material contract validation for the standard lit shader.
  - [ ] Runtime smoke draws at least one normal-mapped material when assets are available.

### Acceptance Criteria

- The renderer can draw a textured, normal-mapped, metallic/roughness mesh using the standard material contract.
- Materials with missing optional textures use deterministic defaults.
- Pass recording remains free of material name lookup, asset loading, and shader compilation.

---

## Milestone 4 - Lights, Environment, And Tonemapping

**Goal:** Add the lighting minimum needed for an attractive scene.

### TODO

- [ ] Define light component data.
  - [ ] Directional light.
  - [ ] Point light with range/intensity.
  - [ ] Optional spot light if needed by the sample scene.
- [ ] Extract light snapshots.
  - [ ] Use contiguous component pools.
  - [ ] Upload compact light buffers during render setup.
  - [ ] Keep per-frame limits explicit and diagnosed.
- [ ] Add first environment lighting.
  - [ ] Sky color or skybox texture.
  - [ ] Ambient term or first image-based lighting placeholder.
- [ ] Add tonemapping pass.
  - [ ] Render scene color in an HDR-capable target when resource planning supports it.
  - [ ] Apply tonemap to the final output before presentation.

### Acceptance Criteria

- A scene can be lit by at least one directional light plus ambient/environment contribution.
- Lighting data reaches shaders through setup-time prepared buffers/constants.
- Runtime scene smoke can show a visibly lit mesh rather than only smoke diffuse shading.

---

## Milestone 5 - Shadows And Render Queue Hardening

**Goal:** Add the first visible depth-based shadow path and make opaque rendering scale beyond a demo.

### TODO

- [ ] Add directional shadow map support.
  - [ ] Shadow camera/cascade policy for the first slice.
  - [ ] Shadow depth RenderGraph pass.
  - [ ] Shadow map sampling in the standard lit shader.
- [ ] Harden render queues.
  - [ ] Opaque queue sorting.
  - [ ] Alpha-test queue policy.
  - [ ] Transparent queue policy documented even if not implemented.
- [ ] Add culling foundation.
  - [ ] Frustum culling from bounds and camera snapshot.
  - [ ] Keep culling output as compact visible draw spans.

### Acceptance Criteria

- A simple scene can cast and receive directional shadows.
- Draw submission order is deterministic and documented.
- Culled entities do not produce mesh draw commands.

---

## Milestone 6 - Editor Scene Authoring Workflow

**Goal:** Let users assemble and iterate on scenes inside the editor instead of editing smoke code.

### TODO

- [ ] Add scene asset inspection.
  - [ ] Show entities, transforms, cameras, renderers, and referenced assets.
  - [ ] Surface scene parse/load diagnostics.
- [ ] Add first hierarchy view.
  - [ ] Select scene entities.
  - [ ] Route selection to the existing inspector.
- [ ] Add transform editing.
  - [ ] Edit translation/rotation/scale.
  - [ ] Write source scene asset changes under allowed `Assets` roots only.
- [ ] Add viewport selection feedback if practical.

### Acceptance Criteria

- A user can inspect and edit the first authored scene asset from the editor.
- Edits affect source scene assets or `.meta` sidecars, never generated `.arisen` files.
- Runtime/editor reload paths remain setup-time and render-safe.

---

## Milestone 7 - Sample Beautiful Scene

**Goal:** Build a small target scene that proves the renderer is becoming visually useful.

### TODO

- [ ] Add a package-owned sample scene asset.
  - [ ] One imported real mesh or multi-object model.
  - [ ] At least two materials.
  - [ ] One camera.
  - [ ] One directional light.
- [ ] Add visual diagnostics.
  - [ ] Log mesh/material/light counts.
  - [ ] Plot draw count, visible draw count, light count, and shadow pass cost in Tracy.
- [ ] Keep validation bounded.
  - [ ] One-frame or two-frame runtime smoke for CI.
  - [ ] Optional longer visual run for manual profiling.

### Acceptance Criteria

- Running the default scene smoke renders an authored scene that is materially better than the old smoke quads.
- Tracy explains extraction, setup, pass recording, submission, and GPU-facing resource counts.
- Full runtime validation remains green.

---

## Recommended Immediate Sprint

Implement next:

1. **Scene asset and scene loading.**

Why this order:

- It gives us a real authoring root for future model import, lights, cameras, and editor workflows.
- It removes the biggest remaining smoke-only assumption without touching the RHI backend.
- It lets the next visible milestones target authored content instead of hardcoded package setup.

After that, the fastest path to a beautiful mesh is:

1. Import a real glTF/GLB model into scene/model assets.
2. Add the standard lit PBR material contract with normal mapping.
3. Add one directional light, simple environment lighting, and tonemapping.
4. Add directional shadows.

At the end of milestones 2 and 3, we should be able to draw a real textured model. At the end of milestones 4 and 5, it should start looking like a real scene instead of an engine test.
