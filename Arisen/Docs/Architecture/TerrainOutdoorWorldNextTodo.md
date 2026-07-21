# Arisen Streamed Terrain And Outdoor World: Next TODO Roadmap

This is the active implementation roadmap after completion of the open-world data foundation.

Arisen can now cook, deploy, stream, inspect, and unload deterministic world cells, but those cells still contain conventional scene meshes. The next playable-content blocker for a large 3D open-world RPG is a real terrain system: package-owned source and cooked data, cell-aware residency, precision-safe LOD, layered PBR rendering, usable outdoor shadows, editor authoring, and a Production validation path.

The goal is not to solve every landscape problem at once. This roadmap should produce one attractive, traversable multi-cell outdoor valley with stable frame behavior and production data ownership. Foliage, full physics, navigation, virtual texturing, weather, and gameplay remain separate systems built on this result.

---

## Verified Starting Point

The completed foundation provides:

- one stable ECS world with persistent and additive cell-owned scene instances;
- deterministic `.arisenscene` and `.arisenworld` cooking plus relocatable cooked-only Production deployment;
- asynchronous worker staging, cancellation, frame-boundary activation/unload, bounded budgets, and retry diagnostics;
- generation-checked asset handles and cell-scoped CPU/GPU residency with submission-ticket-safe disposal;
- double-precision world coordinates, origin-relative float ECS/render data, and deterministic frame-boundary rebasing;
- Editor world/cell documents, partition bounds, load/unload/pin/focus/save/reimport controls, and stable hierarchy identity;
- a RenderGraph/TaskGraph pipeline with HDR scene color, depth, PBR static meshes, environment lighting, one directional shadow map, transparent ordering, and color/depth readback validation;
- package-neutral bounded smoke scenarios, named visual captures, Tracy instrumentation, and copied-output Production validation.

The latest full Debug gate is:

- BuildTool tests: `39/39`;
- kernel tests: `35/35`;
- launcher tests: `18/18`;
- rendering tests: `302/302`;
- Editor, Development, Production, and RHIVulkanTesting runtime profiles passing;
- four runtime smoke runs, zero skips, and zero CPU fallbacks;
- one real Editor viewport smoke;
- Development and Production world-streaming smoke plus relocated Production world streaming;
- all required Vulkan validation logs present and empty.

Measured terrain gaps:

- no terrain package, asset type, cooker, ECS component, runtime query service, or editor tool exists;
- world cells can own scene dependencies, but they cannot yet declare terrain-tile ownership;
- Generic RP has no package extension seam for a separately owned render feature;
- the RHI exposes direct indexed draws, compute dispatch, bindless resources, mip/array image descriptions, and Vulkan multi-draw capability, but the shared command contract does not yet expose a production indirect terrain path;
- the current single directional shadow map is intentionally insufficient for outdoor camera ranges;
- texture mip generation and terrain-scale material filtering are not production complete;
- there is no deterministic terrain-specific visual, LOD, seam, residency, or relocated-output gate.

---

## Roadmap Outcome

At completion, Arisen must be able to author, cook, deploy, and render a multi-cell outdoor terrain where:

1. each terrain root, layer set, and generated tile has stable asset identity;
2. Production loads only versioned cooked terrain artifacts from the relocatable runtime catalog;
3. world-cell activation acquires terrain CPU/GPU dependencies before exposing terrain entities;
4. camera-relative LOD selects bounded visible patches without cracks or origin-rebase jumps;
5. terrain participates in depth, opaque lighting, environment lighting, and cascaded directional shadows through RenderGraph passes;
6. a bounded number of PBR layers provide filtered albedo, normal, and material response without per-frame asset lookup;
7. Editor users can import, inspect, sculpt, paint, save, recook, pin, and preview affected tiles with undo/redo;
8. CPU height/normal queries are available to future gameplay, physics, and navigation without coupling them to the renderer;
9. automated Development, Editor, and relocated Production validation catches cook, seam, LOD, residency, precision, visual, and shutdown regressions.

---

## Guiding Rules

1. **Terrain is a package feature, not Generic RP source code by default.** Keep terrain data/runtime ownership separate from its concrete Generic RP adapter.
2. **World cells own terrain residency.** A loaded visual patch must trace back to one active/pinned terrain tile and one residency owner.
3. **Authoring and runtime formats remain separate.** Editor-friendly descriptors and height sources are never parsed by Production.
4. **Stable tile identity does not include transient LOD.** Source tile coordinates identify content; selected render patches are frame data.
5. **Workers prepare immutable CPU data only.** ECS mutation and RHI setup remain bounded frame-boundary work; RenderGraph recording consumes prepared spans and handles.
6. **Do not begin with tessellation, mesh shaders, or GPU-driven indirect draws.** Establish a correct shared-grid/direct-draw path and measured command pressure first.
7. **LOD must be deterministic and crack-free.** Camera input, quality settings, and resident tiles must produce a stable ordered patch list with bounded neighbor deltas.
8. **Large-world rules apply everywhere.** Serialized tile placement is double precision; hot transforms, bounds, and GPU data are origin/camera-relative floats.
9. **Material scale requires mipmaps.** Terrain must not ship with shimmering full-resolution layer sampling or hidden runtime mip generation.
10. **No per-patch service lookups or allocations.** Resolve services during setup and build reusable contiguous patch/draw arrays.
11. **Editor edits are transactions.** A brush stroke has deterministic affected tiles, undo data, dirty state, and an atomic save boundary.
12. **Validation follows observable states, never sleeps.** LOD, streaming, cooking, and editor checks use bounded state/artifact contracts.

---

## Package And Ownership Boundaries

- `com.arisen.terrain` owns terrain source/cooked schemas, tile identities, deterministic cooking/reading, ECS terrain components, runtime tile/query services, LOD planning, and package-neutral diagnostics.
- `com.arisen.terrain.generic-renderpipeline` owns Generic RP feature registration, prepared terrain GPU resources, terrain shaders/passes, draw preparation, and deferred device-resource disposal.
- `com.arisen.terrain.editor` owns terrain import/sculpt/paint UI, brush transactions, terrain SceneView overlays, selection, dirty state, and explicit save/reimport/cook commands. It is selected only by the Editor profile.
- `com.arisen.resources` continues to own world/cell loading and generic residency coordination. It should not learn terrain rendering policy.
- `com.arisen.rendering` continues to own shared RenderGraph, frame snapshots, command abstractions, and backend-neutral resource contracts.
- `com.arisen.generic-renderpipeline` owns a narrow feature registry/hook contract for separately packaged Generic RP features. It must not depend on terrain types.
- composition/root metadata selects the terrain runtime plus the concrete pipeline/editor adapters. Reusable packages must not depend on Vulkan.

---

## Milestone 1 - Terrain Package Spine And Render-Feature Seam

**Goal:** Establish package direction and lifecycle before terrain data leaks into Generic RP or Editor internals.

### TODO

- [ ] Scaffold `com.arisen.terrain`, `com.arisen.terrain.generic-renderpipeline`, and `com.arisen.terrain.editor` with explicit package/service metadata.
  - [ ] Add runtime/renderer packages to applicable workspace profiles and the editor adapter only to `Editor`.
  - [ ] Keep Vulkan selection at composition level; terrain packages depend on shared RHI/rendering contracts only where required.
  - [ ] Validate topological load/unload order and generated project references.
- [ ] Add a Generic RP feature registration seam owned by `com.arisen.generic-renderpipeline`.
  - [ ] Let extension packages register before pipeline activation and unregister idempotently on unload.
  - [ ] Freeze/cache the active feature list for frame setup; do not query `IServiceRegistry` in `SetupGraph`, pass preparation, or recording loops.
  - [ ] Define bounded hooks for resource preparation, extraction consumption, graph contribution, submission notification, and device-resource release.
  - [ ] Reject duplicate feature IDs and registration after active-pipeline startup with stable diagnostics.
- [ ] Add an Editor extension registration seam if existing editor composition cannot host terrain tools without a direct dependency.
  - [ ] Keep terrain editor panels/inspectors in the adapter package.
  - [ ] Preserve Editor startup when terrain packages are absent.
- [ ] Add focused package-boundary and lifecycle tests.
  - [ ] Prove Generic RP and Editor compile/run without terrain selected.
  - [ ] Prove terrain runtime compiles without Generic RP or Editor references.
  - [ ] Prove adapter teardown releases feature-owned resources before RHI unload.

### Acceptance Criteria

- The package graph expresses terrain runtime, rendering adapter, and editor adapter as separate ownership units.
- Generic RP has no compile-time dependency on terrain.
- No terrain service lookup or interface dispatch is introduced inside patch/draw recording loops.

---

## Milestone 2 - Versioned Terrain Source And Cooked Tile Assets

**Goal:** Turn one authored heightfield and layer description into deterministic, independently deployable terrain tiles.

### TODO

- [ ] Define terrain authoring assets.
  - [ ] Add a terrain-root descriptor with stable GUID, world placement, sample spacing, height scale/range, tile resolution, border policy, and generated tile records.
  - [ ] Add a terrain layer-set asset with a bounded ordered layer list and GUID references to albedo, normal, and ORM inputs.
  - [ ] Support one explicit 16-bit lossless height import format first; reject ambiguous channel/bit-depth/color-space input.
  - [ ] Persist deterministic generated tile GUIDs by signed tile coordinate and preserve them across unchanged reimport.
- [ ] Define versioned cooked root and tile containers.
  - [ ] Use fixed-width little-endian headers, magic, versions, hashes, section directories, bounded counts/offsets, and explicit alignment.
  - [ ] Store quantized heights with scale/offset, duplicated or reconstructible borders, min/max bounds, geometric-error hierarchy, layer weights, dependency identities, and optional diagnostics.
  - [ ] Keep source tile identity independent from runtime LOD nodes and backend handles.
  - [ ] Reject unsupported required sections before publishing staging data.
- [ ] Add deterministic cooking and bounds-checked readers.
  - [ ] Canonicalize tile/layer/dependency order and omit timestamps, absolute paths, and dictionary order.
  - [ ] Produce byte-identical root/tile artifacts for unchanged inputs.
  - [ ] Validate hashes, truncation, overlap, dimensions, finite scale/range, border agreement, normalized weights, and error monotonicity.
  - [ ] Register explicit runtime variants and extend package-owned cook recipes without teaching `ArisenBuildTool` terrain parsing.
- [ ] Integrate terrain closure and deployment.
  - [ ] Ensure a world root deterministically closes over terrain root, layer set, every referenced tile, and transitive texture/shader dependencies.
  - [ ] Reuse unchanged tile artifacts and transactionally remove stale generated tiles/catalog rows.
  - [ ] Prove copied Production output needs no authoring heightmap, terrain descriptor, package source, or workspace cache.
- [ ] Add corruption and determinism tests.
  - [ ] Cover wrong magic/version/hash, malformed dimensions, oversized sections, invalid neighbors, stale generated identity, and missing dependencies.

### Acceptance Criteria

- Terrain tiles cook independently and unchanged tiles retain byte identity after a partial reimport.
- Production can validate every terrain byte before ECS or GPU mutation.
- Runtime catalog paths remain relocatable and source-independent.

---

## Milestone 3 - World-Cell, ECS, And Residency Integration

**Goal:** Make terrain tile lifetime follow the completed world-streaming ownership model.

### TODO

- [ ] Add explicit terrain scene-component schema and cooked-scene support.
  - [ ] Store terrain root/tile/layer GUIDs, signed tile coordinate, world placement, and visibility/quality flags.
  - [ ] Extend source/cooked parity and reject duplicate tile ownership or invalid tile/root pairings.
  - [ ] Keep UI state and selected LOD out of ECS components.
- [ ] Bind terrain entities to world cells.
  - [ ] Generate or author one deterministic terrain-tile entity under its owning cell scene.
  - [ ] Define border ownership so adjacent cells do not create overlapping surfaces or gaps.
  - [ ] Preserve tile GUID/coordinate identity across unload/reload and origin rebasing.
- [ ] Extend runtime residency variants and prepared-resource coordination.
  - [ ] Acquire cooked height/error/weight payloads on workers before activation.
  - [ ] Keep cells in `WaitingForResources` until required terrain GPU resources are ready.
  - [ ] Share root/layer resources across tiles while tile height/weight resources remain independently evictable.
  - [ ] Release only after successful ECS unload and defer bindless/native destruction through the latest submission ticket.
- [ ] Add terrain-specific residency metrics without duplicating generic ownership.
  - [ ] Report resident tile count, CPU height bytes, prepared height/weight bytes, layer descriptors, pending disposals, setup time, and budget pressure.
  - [ ] Attribute diagnostics to terrain root, tile GUID, cell ID, and signed coordinate.
- [ ] Add focused streaming tests.
  - [ ] Cover shared layers, cancellation-lost results, activation gating, unload rejection, retry, LRU pressure, and shutdown drain.

### Acceptance Criteria

- No terrain entity becomes active before its required tile and layer resources are usable.
- Unloading a cell cannot destroy terrain resources referenced by an in-flight frame or neighboring tile owner.
- Repeated tile load/unload returns handles, descriptors, and task counts to baseline.

---

## Milestone 4 - Deterministic Terrain Queries, Bounds, And LOD Planning

**Goal:** Produce a crack-free, bounded patch list from resident tiles before recording any GPU work.

### TODO

- [ ] Add a package-neutral terrain query service.
  - [ ] Query resident height, normal, material weights, and terrain presence from double-precision world coordinates.
  - [ ] Return explicit unavailable/outside-residency results; never synchronously load from a query.
  - [ ] Define deterministic border sampling shared by editor, future physics/navigation, and renderer tests.
- [ ] Build immutable tile acceleration data during cooking/loading.
  - [ ] Store min/max pyramids and geometric error per quadtree node or equivalent patch hierarchy.
  - [ ] Validate conservative bounds against source samples.
  - [ ] Keep query data contiguous and generation-qualified by tile ownership.
- [ ] Implement setup-owned LOD selection.
  - [ ] Use camera projection, viewport height, authored error threshold, and camera-relative bounds.
  - [ ] Add LOD hysteresis to avoid frame-to-frame oscillation.
  - [ ] Enforce a maximum one-level neighbor delta and choose deterministic stitch/skirt patterns.
  - [ ] Order visible patches by terrain/tile identity and patch key after culling.
  - [ ] Bound patches per frame and surface overflow/budget diagnostics instead of silently dropping near content.
- [ ] Integrate origin rebasing.
  - [ ] Keep selection and queries in double world space while emitting origin-relative float patch records.
  - [ ] Prove a rebase changes representation but not selected world coverage or sampled height.
- [ ] Add precision, seam, and DOD tests.
  - [ ] Verify borders bit-match across adjacent tiles and all legal mixed-LOD edges are watertight.
  - [ ] Verify stable selection at negative coordinates and representative multi-kilometer distances.
  - [ ] Verify steady-state selection reuses arrays and allocates no managed objects per patch.

### Acceptance Criteria

- Identical camera/residency/settings input produces an identical ordered patch list.
- Neighboring resident tiles render a continuous surface across tile and LOD boundaries.
- Query and LOD results remain stable through an origin rebase.

---

## Milestone 5 - First Generic RP Terrain Render Path

**Goal:** Render streamed terrain through the existing RenderGraph and shared command contract without bypasses.

### TODO

- [ ] Add prepared terrain GPU resources in the Generic RP adapter.
  - [ ] Reuse immutable shared grid vertex/index buffers and a bounded stitch/skirt index-pattern set.
  - [ ] Upload tile height/error/weight data outside pass recording into explicit sampled/storage resources.
  - [ ] Track GPU estimates, bindless indices, and deferred disposal through residency ownership.
  - [ ] Rebuild changed tiles at a frame boundary while retaining the previous valid resource until replacement succeeds.
- [ ] Add terrain extraction and draw preparation.
  - [ ] Consume contiguous active terrain components and the LOD planner into reusable patch/draw spans.
  - [ ] Frustum-cull conservative patch bounds before draw expansion.
  - [ ] Use direct indexed draws first and measure command pressure before adding shared indirect APIs.
  - [ ] Split large patch ranges into bounded TaskGraph recording work items while preserving deterministic submission order.
- [ ] Add terrain RenderGraph passes.
  - [ ] Participate in the graph-owned depth target and HDR scene color with typed access declarations.
  - [ ] Integrate terrain with opaque depth/write ordering and existing output/readback ownership.
  - [ ] Keep setup, asset lookup, LOD selection, and resource creation outside `RecordCommands`.
- [ ] Add a minimal lit terrain shader.
  - [ ] Reconstruct world/origin-relative position and normal from prepared height data.
  - [ ] Consume the existing camera, directional light, environment/IBL, exposure, and tonemap contracts.
  - [ ] Validate finite data and define one backend-neutral UV/projection convention.
- [ ] Add focused render tests and a first visible fixture.
  - [ ] Render a two-by-two tile heightfield with mixed LOD and deterministic color/depth summaries.
  - [ ] Verify blank/fallback terrain is never substituted after required-resource failure.

### Acceptance Criteria

- A streamed cooked terrain tile is visibly rendered in Editor, Development, and Production.
- Terrain uses RenderGraph transitions and `RenderCommandList`; no pass invokes concrete Vulkan APIs.
- Command recording performs no asset/service lookup or managed allocation per patch.

---

## Milestone 6 - Layered PBR Terrain Materials And Filtering

**Goal:** Replace debug terrain shading with a stable, attractive outdoor material path.

### TODO

- [ ] Define a bounded terrain layer contract.
  - [ ] Support an initial maximum of four simultaneously blended layers per tile.
  - [ ] Store albedo, normal, ORM, tint, roughness/metallic multipliers, normal strength, and world-space tiling.
  - [ ] Normalize weights deterministically and define zero-weight fallback behavior.
- [ ] Complete texture mip cooking needed by terrain.
  - [ ] Generate deterministic color-space-correct mip chains for sRGB and linear texture variants.
  - [ ] Preserve normal-map normalization and ORM channel semantics through downsampling.
  - [ ] Add anisotropic sampler settings within backend capability limits and include them in prepared-resource identity.
- [ ] Implement terrain PBR evaluation.
  - [ ] Blend normals with a documented method, not raw linear interpolation of encoded values.
  - [ ] Keep shader variants bounded; layer count and optional maps must not create an unbounded keyword Cartesian product.
  - [ ] Use camera-relative UV math or stable world reconstruction that does not shimmer after origin rebasing.
  - [ ] Add distance macro variation only after base mip/LOD stability is proven.
- [ ] Add material hot reload and residency sharing.
  - [ ] Reimporting one layer texture invalidates only dependent prepared layer sets/tiles.
  - [ ] Shared layer textures retain one GPU allocation until the last terrain/material owner releases it.
- [ ] Add visual and numeric tests.
  - [ ] Verify weight sums, normal lengths, mip dimensions/content hashes, color-space rules, and sampler identity.
  - [ ] Capture near/mid/far views that detect tiling, shimmering-prone missing mips, and broken normal blending.

### Acceptance Criteria

- The canonical outdoor fixture has at least rock, grass/soil, and path material response with stable filtered detail.
- Camera movement and origin rebasing do not visibly reset world-space texture placement.
- Layer resources remain shared and ticket-safe under cell churn and reimport.

---

## Milestone 7 - Outdoor Cascaded Directional Shadows

**Goal:** Replace the single showcase shadow slice with stable outdoor-scale terrain and mesh shadows.

### TODO

- [ ] Extend serialized Generic RP shadow quality settings.
  - [ ] Add bounded cascade count, maximum distance, practical split weight, terminal fade, and per-tier resolution.
  - [ ] Preserve compatible defaults and reject invalid/non-finite settings.
- [ ] Add shared depth-array/layer rendering support where the RHI contract is missing it.
  - [ ] Keep array-layer selection backend-neutral in image-view/rendering commands.
  - [ ] Add Vulkan implementation/tests without exposing Vulkan types to passes.
- [ ] Implement setup-owned cascade preparation.
  - [ ] Split by linear camera depth, fit receivers/casters per cascade, stabilize to texels, and retain camera-relative precision.
  - [ ] Build compact terrain and static-mesh caster ranges per cascade.
  - [ ] Cull outside each cascade before command recording and cap work with explicit diagnostics.
- [ ] Integrate cascade sampling in static-mesh and terrain shaders.
  - [ ] Select/fade cascades without seams and retain configurable PCF/bias behavior.
  - [ ] Keep alpha-test mesh shadow behavior and exclude transparent materials.
- [ ] Add stability and visual tests.
  - [ ] Verify split coverage, array-layer writes, receiver orientation, texel snapping, and rebase stability.
  - [ ] Capture near/mid/far terrain shadows and detect missing layers or one-frame jumps.

### Acceptance Criteria

- Terrain and static meshes share stable cascaded sunlight across the canonical outdoor camera range.
- Cascade transitions are bounded and do not expose backend-specific projection/Y fixes.
- Vulkan validation remains empty for every promoted render run.

---

## Milestone 8 - Outdoor Atmosphere And Distance Readability

**Goal:** Make the terrain scene read as an outdoor world without committing to a full weather/time-of-day system.

### TODO

- [ ] Define an asset-driven outdoor environment profile.
  - [ ] Store sun/sky coupling, horizon/zenith response, aerial-perspective distance, height fog, and exposure policy.
  - [ ] Keep authored values finite, bounded, and independent from backend conventions.
- [ ] Extend the environment/sky path.
  - [ ] Preserve image-based environment lighting while adding one deterministic procedural outdoor sky mode.
  - [ ] Couple the visible sun direction to the accepted directional light without duplicating light ownership.
  - [ ] Ensure sky and terrain share HDR/exposure/tonemap response.
- [ ] Add depth-aware distance haze or aerial perspective as a RenderGraph pass.
  - [ ] Sample graph-owned depth through an explicit typed dependency.
  - [ ] Reconstruct distance consistently with reversed/non-reversed depth policy already used by the pipeline.
  - [ ] Keep the pass optional and free of resource setup during recording.
- [ ] Add visual checks for horizon continuity, exposure, and depth orientation.

### Acceptance Criteria

- The outdoor fixture has readable scale, horizon, sunlight, and distant terrain separation.
- Atmosphere uses graph-owned HDR/depth resources and does not reintroduce Y-flip or projection branches.

---

## Milestone 9 - Editor Terrain Authoring

**Goal:** Let an author create and modify terrain without hand-editing descriptors or generated tiles.

### TODO

- [ ] Add terrain creation/import workflow.
  - [ ] Create a terrain root from a supported height source, layer set, world bounds, and tile settings.
  - [ ] Preview the deterministic tile/cell mapping before committing generated assets.
  - [ ] Reject destructive identity/layout changes until the author explicitly confirms regeneration.
- [ ] Add terrain selection and diagnostics.
  - [ ] Draw tile bounds, selected LOD, residency, dirty state, failed state, memory, and neighbor/seam diagnostics in SceneView.
  - [ ] Expose root/tile/layer identity, cooked version, min/max/error metrics, and current query result in Inspector/terrain panels.
  - [ ] Preserve selection, camera focus, world pins, hierarchy expansion, and unsaved state across runtime refreshes.
- [ ] Add bounded sculpt and paint tools.
  - [ ] Implement one deterministic height brush and one four-layer weight brush first.
  - [ ] Compute affected tiles including shared borders, capture compact undo data, and update immutable preview revisions.
  - [ ] Keep UI-thread edits out of live ECS/GPU state; queue coalesced frame-boundary preview replacement.
- [ ] Add save/reimport/cook transactions.
  - [ ] Save all affected tile sources atomically or leave every source unchanged.
  - [ ] Reimport external height changes with explicit dirty/conflict resolution.
  - [ ] Recook only changed tiles plus dependency-invalidated outputs.
- [ ] Add focused editor tests and one real-host smoke.
  - [ ] Verify first-open terrain visibility, brush undo/redo, border propagation, save/reload, conflict handling, pin independence, and no lost UI state.

### Acceptance Criteria

- An author can import, sculpt, paint, save, recook, and preview a multi-cell terrain from the Editor.
- Brush edits preserve tile borders and stable generated identities.
- SceneView preview follows the same residency and frame-boundary replacement rules as runtime.

---

## Milestone 10 - Terrain Reliability, Profiling, And Production Gate

**Goal:** Promote terrain to the same deterministic standard as world streaming before adding foliage or gameplay.

### TODO

- [ ] Add a package-provided bounded `terrain-streaming` smoke scenario.
  - [ ] Follow a deterministic near/mid/far camera path across tile/cell boundaries and one origin rebase.
  - [ ] Assert resident tile sets, selected LOD distribution, patch bounds, ECS ownership, terrain query parity, and zero seam violations at named checkpoints.
  - [ ] Complete repeated load/unload/reimport-compatible soak cycles without sleeps.
- [ ] Add terrain memory and shutdown checks.
  - [ ] Bound ECS slots, cooked handles, CPU height/error bytes, prepared images/buffers, layer descriptors, patch capacity, and deferred disposal.
  - [ ] Verify no terrain tile, feature, task, residency owner, descriptor, or native resource survives package teardown.
- [ ] Add named visual/GPU captures.
  - [ ] Capture near, boundary/mixed-LOD, far/cascade, post-rebase, and returned-to-start frames.
  - [ ] Validate color/depth coverage plus terrain-specific silhouette/seam samples and stable horizon orientation.
  - [ ] Keep required Vulkan logs empty for real Editor, Development, Production, relocated Production, and RHIVulkanTesting runs.
- [ ] Add Tracy zones and plots.
  - [ ] Attribute tile read/cook/setup, LOD planning, patch expansion, terrain pass recording, cascade preparation, and unload to terrain/tile IDs where useful.
  - [ ] Plot resident/visible tiles, LOD histogram, patches/draws, culled patches, CPU/GPU bytes, descriptors, setup time, budget stalls, and seam violations.
  - [ ] Keep counters coarse and out of sample/vertex/entity inner loops.
- [ ] Promote the full gate.
  - [ ] Integrate package, format, query, LOD, render, editor, and lifecycle tests into `validate_fast.bat`.
  - [ ] Integrate terrain streaming/visual checks and copied cooked-only Production into `validate_runtime.bat`.
  - [ ] Run the complete Debug gate with zero source fallback, zero skipped GPU checks, bounded memory, and empty Vulkan validation logs.

### Acceptance Criteria

- Automated validation catches terrain payload, identity, border, LOD, residency, precision, rendering, editor, and shutdown regressions.
- A copied Production output renders and streams the canonical terrain with no workspace/source/cache access.
- Tracy makes terrain streaming, LOD, setup, and draw pressure attributable and measurable.
- The resulting outdoor scene is stable and visually strong enough to begin foliage or character/gameplay work without replacing the terrain foundation.

---

## Immediate Implementation Sprint

Implement the first visible vertical slice in this order:

1. [ ] scaffold the three terrain packages and add profile composition;
2. [ ] add the Generic RP feature registry and prove optional feature lifecycle/teardown;
3. [ ] define terrain root/layer/tile source identities and small deterministic fixtures;
4. [ ] implement cooked terrain tile v1 plus corruption/determinism tests;
5. [ ] add terrain scene-component source/cooked parity and world-cell ownership;
6. [ ] implement a shared-grid direct-draw adapter that renders one cooked tile through RenderGraph;
7. [ ] extend the fixture to adjacent tiles and prove border continuity before broader LOD/material work.

The first checkpoint is not a full editor brush system. It is one package-owned cooked tile, loaded through world residency, rendered by an optional Generic RP feature, and validated without source access.

---

## Explicitly Deferred

- virtual texturing, sparse residency, texture feedback, and install-time page streaming;
- procedural erosion, biome synthesis, runtime terrain generation, and destructive topology editing;
- foliage/grass scattering, hierarchical instance culling, impostors, and vegetation simulation;
- rigid-body terrain collision, streamed collision meshes, navmesh generation, and pathfinding;
- GPU-driven multi-draw/mesh-shader terrain until direct-draw Tracy data justifies the shared RHI work;
- caves, overhangs, voxel terrain, roads/splines, water bodies, and decals;
- full weather, clouds, volumetrics, and dynamic time of day;
- character animation, gameplay framework, AI, quests, inventory, save games, and world-state persistence;
- multiplayer replication and server-authoritative terrain edits;
- DX12/Metal terrain adapters until those backend packages exist.

---

## Roadmap Completion Rule

This roadmap is complete only when all ten milestones and acceptance criteria are implemented, architecture docs describe the actual behavior, and the promoted fast/runtime gates pass from a clean generated workspace.

At completion, delete this file and create the next single active roadmap from measured results. The likely candidates are foliage/vegetation, character animation/gameplay, or physics/navigation; choose from the outdoor vertical slice's actual bottleneck rather than preserving this roadmap as history.
