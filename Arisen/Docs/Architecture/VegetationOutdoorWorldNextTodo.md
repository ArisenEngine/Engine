# Arisen Vegetation And Biome Outdoor World: Next TODO Roadmap

This is the single active implementation roadmap as of 2026-08-03, after completion of the engine
stabilization and promotion gate.

Arisen can now author, cook, deploy, stream, query, render, and validate a layered multi-cell terrain with deterministic LOD, cascaded shadows, origin rebasing, bounded residency, Editor transactions, and copied cooked-only Production coverage. The next visible and scalability blocker for a large 3D open-world RPG is vegetation: stable species and biome data, deterministic terrain-aware placement, cell-owned residency, dense instanced rendering, wind and shadow integration, practical Editor authoring, and a Production reliability gate.

The goal is one attractive outdoor valley populated with grass, shrubs, rocks, and trees at useful density without turning every plant into an ECS entity or prematurely committing the engine to a Vulkan-specific GPU-driven design. Gameplay interaction, destruction, harvest systems, advanced tree generation, and global ecological simulation remain later layers built on this result.

---

## Verified Starting Point

The completed foundation provides:

- transactional package mount/rollback, aggregate idempotent teardown, semantic-version and engine
  compatibility enforcement, and finalized SHA-256 native payload identity before boot;
- a no-throw native ABI error contract, generation-qualified RHI/surface ownership, explicit
  acquired/submitted/presented-or-retired frame state, and request-owned RenderDoc publication;
- immutable cooked-asset snapshots with serialized atomic mutation plus deterministic Editor,
  import-worker, TaskGraph, diagnostics, and graphics-generation drains;
- package-owned source/cooked assets, generated-child identity, deterministic catalog closure, and relocatable cooked-only deployment;
- one stable ECS world with persistent and additive cell-owned scene instances, exclusive extension-component identities, and frame-boundary activation/unload;
- bounded asynchronous world-cell reads, cancellation, retries, edit pins, residency ownership, submission-ticket-safe disposal, and shutdown drain;
- double-precision world coordinates, origin-relative float ECS/render data, and deterministic frame-boundary rebasing;
- a package-neutral terrain runtime/query service with stable root/tile identity, bilinear height, normal, normalized layer weights, and explicit unavailable/outside states;
- deterministic terrain LOD and shared-edge topology with zero seam violations across the canonical two-by-two fixture;
- a Generic RP optional-feature registry with opaque and directional-shadow stages, frozen setup-time feature arrays, submission notification, and reverse device-resource release;
- graph-owned HDR color/depth, PBR materials, environment lighting, procedural sky/fog, four directional-shadow cascades, and color/depth readback validation;
- direct indexed instancing through `RenderCommandList.DrawIndexed(..., instanceCount, ..., firstInstance, ...)`;
- native RHI/Vulkan indirect command opcodes and `multiDrawIndirect` capability, but no managed `RHICommandBuffer` or `RenderCommandList` indirect API;
- Editor extension registration, transactional terrain sculpt/paint/save/reimport/cook, immutable authoring previews, diagnostics, and stable hierarchy state;
- package-provided bounded smoke scenarios, named visual captures, Tracy instrumentation, and copied-output Production validation.

The completed foundation gate on 2026-08-03 is:

- BuildTool tests: `72/72`;
- kernel tests: `72/72`;
- launcher tests: `18/18`;
- rendering/asset/Editor tests: `627/627`;
- the Debug `validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` gate
  passed with Editor, Development, Production, and RHIVulkanTesting GPU smoke coverage, zero
  skipped GPU checks, viewport coverage, world/terrain streaming checks, copied cooked-only
  Production coverage, and clean shutdown;
- generated Editor, Development, and Production workspaces built with zero warnings and zero errors.

Measured vegetation gaps:

- the Milestone 1 vegetation package spine now exists in three repositories, but no species,
  foliage, biome, scatter, or impostor data has been implemented;
- no source or cooked schema defines species meshes/materials, placement rules, stable instances, clusters, or LOD ranges;
- no scene component or runtime service binds vegetation clusters to world cells;
- ordinary static meshes are entity-oriented and are not an acceptable representation for tens of thousands of plants;
- Generic RP has no vegetation extraction, instance-buffer preparation, wind shading, alpha-cutout path, or vegetation shadow contribution;
- the existing direct indexed API can render instanced batches, but there is no measured vegetation command/memory baseline and no shared managed indirect-draw contract;
- Editor has no biome painting, density masks, exclusion volumes, scatter preview, instance inspection, or regeneration transaction;
- no Development/Production/relocated Production gate validates vegetation identity, placement, LOD, memory, visuals, wind, shadows, origin rebasing, or shutdown.

---

## Roadmap Outcome

At completion, Arisen must be able to author, cook, deploy, stream, and render a populated multi-cell outdoor valley where:

1. vegetation species, biome rules, masks, generated clusters, and cooked instance pages have stable asset identity;
2. unchanged inputs produce byte-identical placement and preserve generated cluster/instance identities;
3. Production loads only versioned cooked vegetation artifacts from the relocatable catalog;
4. world cells own vegetation cluster residency and cannot expose a cluster before required mesh/material/instance resources are prepared;
5. dense vegetation is represented as contiguous cluster/instance data, not one managed object or ECS entity per plant;
6. setup-owned culling and LOD produce deterministic, bounded direct-instanced batches across origin rebases;
7. vegetation participates in depth, opaque lighting, environment lighting, wind, and cascaded directional shadows through RenderGraph passes;
8. Editor users can paint biome density/exclusion, preview deterministic regeneration, inspect clusters, undo/redo, save, and recook without blocking the UI thread;
9. Tracy makes scatter cooking, cell activation, culling, LOD, upload, draw pressure, memory, and disposal attributable;
10. automated Development, Editor, Production, and relocated Production validation catches placement, residency, rendering, precision, visual, and teardown regressions.

---

## Guiding Rules

1. **Vegetation is a package feature.** Keep runtime data, Generic RP rendering, and Editor authoring in separate ownership units.
2. **Start from existing extension seams.** Reuse runtime cooker, scene-component, residency, Generic RP feature, and Editor extension registries before changing shared packages.
3. **World cells own cluster residency.** A visible instance must trace to one active or edit-pinned cell, one cooked cluster generation, and one residency owner.
4. **Do not create one ECS entity per plant.** Use one blittable cluster/component identity plus contiguous instance pages; reserve entities for promoted interactive objects.
5. **Authoring and runtime formats stay separate.** Production never parses biome YAML, masks, scatter settings, or package source files.
6. **Placement is deterministic and order-independent.** Stable IDs derive from authored identity and canonical spatial keys, never collection iteration order or timestamps.
7. **Terrain sampling happens during bake or bounded setup, not per frame.** Render loops consume cooked placement, bounds, and instance data.
8. **Large-world rules apply.** Persist double-world placement and emit origin-relative float instance transforms for the current frame/device generation.
9. **Direct instancing is the baseline.** Group by species/mesh/material/LOD/shadow state and measure command pressure before extending shared indirect APIs.
10. **No per-instance service lookup, allocation, interface dispatch, or lock.** Hot paths operate on reusable contiguous arrays/spans and batch native calls.
11. **LOD and density transitions must be stable.** Use hysteresis and deterministic dither/fade policy; camera motion must not reshuffle instance identity.
12. **Editor edits are transactions.** One stroke/regeneration has explicit affected cells, undo data, dirty state, and atomic save/cook publication.
13. **Validation follows observable states.** Streaming, generation, wind, LOD, and disposal checks use named checkpoints and artifacts, never sleeps.

---

## Package And Ownership Boundaries

- `com.arisen.vegetation` owns species/biome/scatter source and cooked schemas, stable spatial identity, deterministic placement, cluster ECS data, runtime CPU data/query services, and package-neutral diagnostics.
- `com.arisen.vegetation.generic-renderpipeline` owns prepared instance/mesh/material resources, culling and LOD draw preparation, vegetation shaders/passes, Generic RP feature registration, submission accounting, and deferred device-resource disposal.
- `com.arisen.vegetation.editor` owns biome/mask authoring, scatter preview, selection overlays, cluster diagnostics, regeneration, undo/redo, save, and explicit cook controls. It is selected only by the Editor profile.
- `com.arisen.terrain` remains the terrain data/query owner. Vegetation may depend on its public placement/query contracts; terrain must not depend on vegetation.
- `com.arisen.resources` remains the generic world/cell, cook-coordinator, scene-extension, asset, and residency owner. It must not learn species or scatter policy.
- `com.arisen.rendering` and `com.arisen.generic-renderpipeline` remain backend-neutral graph/pipeline owners. They should change only for a measured reusable rendering contract, not vegetation-specific types.
- composition/root metadata selects the vegetation runtime and concrete Generic RP/Editor adapters. Reusable vegetation packages must not depend on Vulkan.

The three vegetation repositories are real submodules at the canonical `Local/com.arisen.*`
paths. Keep them separate: runtime contracts/data in `com.arisen.vegetation`, Generic RP
integration in `com.arisen.vegetation.generic-renderpipeline`, and Editor authoring in
`com.arisen.vegetation.editor`. Do not replace these submodules with ordinary package
directories.

---

## Milestone 1 - Vegetation Package Spine And Contracts

**Goal:** Establish package direction and lifecycle using existing extension registries.

### TODO

- [x] Create and add `com.arisen.vegetation`, `com.arisen.vegetation.generic-renderpipeline`, and `com.arisen.vegetation.editor` as submodules.
- [x] Add explicit package/service metadata and profile composition.
  - [x] Select runtime and Generic RP adapter in Editor/Development/Production.
  - [x] Select the Editor adapter only in `Editor`.
  - [x] Keep Vulkan only at composition level.
- [x] Define narrow package-neutral services for runtime cluster data, optional queries, diagnostics, and authoring preview.
- [x] Register one stable Generic RP feature and one Editor extension through existing registries.
- [x] Prove optional lifecycle and package direction.
  - [x] Generic RP and Editor still run without vegetation selected.
  - [x] Vegetation runtime compiles without Generic RP, Editor, or Vulkan references.
  - [x] Adapter teardown releases feature resources before RHI destruction and unregisters before provider unload.

### Acceptance Criteria

- Package metadata alone expresses inclusion, service closure, and unload order.
- No shared package depends on vegetation.
- No vegetation service lookup is introduced in per-instance or command-recording loops.

### Milestone 1 Completion Record

- The runtime package publishes immutable atomic cluster-data, diagnostics, and
  authoring-preview snapshots and exposes explicit invalid/outside/unavailable
  query states.
- The Generic RP adapter registers a stable no-pass feature and releases it in
  reverse lifecycle order; the Editor adapter registers a lifecycle-valid
  extension without introducing placeholder UI.
- `72/72` BuildTool tests, `72/72` kernel tests, `18/18` launcher tests, and
  `627/627` rendering/Editor tests passed. The canonical Editor, Development,
  and Production workspaces generated and built successfully, and the complete
  Debug runtime validation artifact is
  `.arisen/Logs/validate-runtime-Debug-latest.json`.
- The next active work is Milestone 2: strict species, biome/scatter, and
  cooked-cluster asset contracts with deterministic fixtures and corruption tests.

---

## Milestone 2 - Species, Biome, And Cooked Cluster Assets

**Goal:** Define deterministic authoring and runtime data before renderer-specific structures leak into source assets.

### TODO

- [ ] Define a vegetation species asset.
  - [ ] Stable GUID/package identity, bounded mesh/material LOD list, shadow policy, scale/yaw/tilt ranges, collision-promotion metadata, and wind response.
  - [ ] Validate mesh/material dependencies and finite ordered distance/error ranges.
- [ ] Define a biome/scatter profile.
  - [ ] Ordered species entries with density, seed salt, altitude/slope/layer-weight rules, minimum spacing, cluster size, and exclusion policy.
  - [ ] Keep quality/runtime density multipliers out of stable source identity unless they change cooked placement.
- [ ] Define versioned cooked biome, species, cluster, and instance-page containers.
  - [ ] Fixed-width little-endian headers, magic/version/hash, bounded section tables, exact alignment, and explicit dependency identities.
  - [ ] Store quantized or compact instance position/orientation/scale, stable instance key, conservative bounds, species index, and optional LOD acceleration.
- [ ] Add strict readers, deterministic writers, and corruption tests.
  - [ ] Reject unsupported required sections, malformed counts/offsets, non-finite transforms, bad quaternions/scales, duplicate IDs, invalid bounds, and missing dependencies.
  - [ ] Prove unchanged inputs produce byte-identical output and preserve timestamps/artifact generations.

### Acceptance Criteria

- Source assets contain durable design intent; cooked assets contain bounded runtime-ready placement.
- Instance and cluster identity is independent from transient render LOD, buffer offsets, and backend handles.
- Production closure needs no vegetation authoring files.

---

## Milestone 3 - Deterministic Terrain-Aware Scatter Baking

**Goal:** Turn biome rules plus terrain data into stable cell-partitioned instance pages.

### TODO

- [ ] Implement canonical candidate generation from integer spatial cells and explicit seeds.
  - [ ] Use a documented deterministic hash/sequence with no global mutable RNG.
  - [ ] Make output independent of task scheduling and dictionary iteration.
- [ ] Evaluate terrain placement against cooked root/tile samples.
  - [ ] Height, normal, slope, altitude, and normalized layer weights.
  - [ ] Stable positive-border ownership matching terrain/world-cell policy.
- [ ] Apply density, spacing, exclusion, and overlap rules deterministically.
  - [ ] Bound candidate and accepted counts per source tile/cell.
  - [ ] Report overflow or invalid rules instead of silently truncating near content.
- [ ] Partition accepted instances into stable clusters/pages owned by world cells.
  - [ ] Derive generated cluster GUIDs from biome/species/cell/page identity.
  - [ ] Reuse unchanged pages and transactionally remove stale pages/catalog rows.
- [ ] Add determinism, border, and parallel-bake tests.

### Acceptance Criteria

- Rebuilding with different worker scheduling produces byte-identical cluster artifacts.
- Adjacent terrain/cell borders neither duplicate nor omit accepted candidates.
- Runtime never reruns source scatter rules during ordinary frame rendering.

---

## Milestone 4 - Scene Components, Cell Ownership, And Residency

**Goal:** Bind cooked vegetation clusters to the world-streaming lifecycle without per-instance ECS overhead.

### TODO

- [ ] Add a package-owned scene extension for vegetation cluster references.
  - [ ] Store biome/species/cluster GUIDs, owning cell, double-world bounds/origin, visibility flags, and quality group.
  - [ ] Use one blittable ECS component per cluster/page, not per instance.
  - [ ] Reject duplicate exclusive cluster ownership and invalid cell/bounds pairings.
- [ ] Add runtime CPU publication and optional query service.
  - [ ] Generation-qualify immutable cluster pages.
  - [ ] Return explicit unavailable/outside states and bounded nearby-instance results for future promotion/gameplay systems.
- [ ] Integrate generic residency.
  - [ ] Workers acquire and validate cooked cluster pages and dependencies.
  - [ ] Cells remain `WaitingForResources` until required prepared resources are ready.
  - [ ] Share species mesh/material resources while cluster instance buffers remain independently evictable.
  - [ ] Release only after ECS unload and defer device destruction through the latest submission ticket.
- [ ] Add cancellation, retry, shared-species, LRU, stale-generation, and shutdown-drain tests.

### Acceptance Criteria

- Every visible cluster has one active/pinned cell owner and generation-matched CPU/GPU residency.
- Unloaded cells expose no queryable or drawable vegetation instance.
- Repeated load/unload returns entity slots, cooked handles, instance pages, descriptors, and native resources to baseline.

---

## Milestone 5 - Direct-Instanced Generic RP Vertical Slice

**Goal:** Render one cooked species cluster through existing backend-neutral APIs before adding broad optimization machinery.

### TODO

- [ ] Prepare immutable species mesh/material resources and one cluster instance storage buffer outside command recording.
- [ ] Define compact origin-relative GPU instance records with transform, stable variation, wind phase, color variation, and selection/debug flags.
- [ ] Extract active cluster components in deterministic biome/cell/species/cluster order into reusable arrays.
- [ ] Group compatible instances by mesh/material/LOD/shadow state.
- [ ] Add opaque and directional-shadow vegetation passes through `IGenericRenderPipelineFeature`.
  - [ ] Use `RenderCommandList.DrawIndexed` with positive `instanceCount` and `firstInstance` ranges.
  - [ ] Declare graph-owned HDR color, depth, and cascade-array access explicitly.
  - [ ] Keep asset lookup, culling, LOD, upload, and pipeline creation outside recording.
- [ ] Add visual and command-contract tests for one tree/shrub cluster.

### Acceptance Criteria

- The first cluster renders in Development, Editor SceneView/GameView, Production, and copied Production.
- One draw represents many instances and native interop is batched.
- The adapter contains no Vulkan type or concrete backend dependency.

---

## Milestone 6 - Hierarchical Culling, LOD, And Large-World Stability

**Goal:** Scale direct instancing across many cells with deterministic setup-owned visibility.

### TODO

- [ ] Build conservative cluster/page acceleration during cooking.
  - [ ] World bounds, spatial hierarchy, per-species ranges, and LOD error/radius data.
- [ ] Implement reusable setup-owned culling and LOD preparation.
  - [ ] Double-world camera input, origin-relative float bounds, frustum and distance/error culling, quality density, and hard batch/instance budgets.
  - [ ] LOD hysteresis and deterministic dither/fade bands.
  - [ ] Stable nearest-first overflow behavior with explicit diagnostics.
- [ ] Preserve identity across origin rebasing.
  - [ ] Rebase changes GPU representation, not accepted instances, world bounds, selected species, or LOD decisions outside the defined hysteresis band.
- [ ] Split large batch ranges into bounded TaskGraph setup/recording work while preserving deterministic submission order.
- [ ] Add multi-kilometer, negative-coordinate, rebase, camera-path, overflow, and zero-steady-state-allocation tests.

### Acceptance Criteria

- Camera movement and rebasing do not reshuffle stable instances or visibly pop unchanged LODs.
- Culling/LOD hot paths reuse contiguous storage and allocate no managed objects after warmup.
- Overflow is measurable and deterministic.

---

## Milestone 7 - Vegetation Materials, Wind, Lighting, And Shadows

**Goal:** Make the populated valley visually credible under the existing outdoor renderer.

### TODO

- [ ] Add bounded vegetation material semantics.
  - [ ] Mipped sRGB albedo/opacity, renormalized normal maps, linear ORM, alpha cutoff, two-sided normal policy, tint variation, and roughness response.
  - [ ] Reject missing mip/format/semantic dependencies during cooking.
- [ ] Add deterministic wind inputs.
  - [ ] Global wind direction/strength plus species stiffness, height response, per-instance phase, and bounded gust parameters.
  - [ ] Keep simulation values frame-global/species-global; do not update managed state per blade/tree.
- [ ] Integrate PBR environment/direct light and four cascade shadows.
  - [ ] Alpha-consistent depth/shadow coverage.
  - [ ] Conservative wind-expanded bounds for culling and shadow fitting.
- [ ] Add distance fade/dither and color/depth/shadow visual tests.
- [ ] Build a canonical fixture with at least grass, shrub, rock, and tree species using inspectable non-placeholder assets.

### Acceptance Criteria

- The valley reads as populated outdoors rather than repeated static cutouts.
- Wind does not break bounds, shadows, depth, or origin-rebase stability.
- Near/mid/far visual captures retain valid silhouette, depth, material, and cascade coverage.

---

## Milestone 8 - Editor Biome And Scatter Authoring

**Goal:** Make vegetation placement usable without hand-editing serialized instance pages.

### TODO

- [ ] Add species and biome inspectors with strict dependency/range validation.
- [ ] Add density, exclusion, and species-weight paint tools.
  - [ ] World/terrain hit testing through existing SceneView and terrain query contracts.
  - [ ] Bounded affected cells/pages and deterministic brush samples.
- [ ] Add immutable unsaved scatter preview through the adapter.
  - [ ] No ECS/UI mutation from worker callbacks.
  - [ ] Previous valid preview remains visible until the complete replacement is ready.
- [ ] Add cluster/instance diagnostics and overlays.
  - [ ] Bounds, owner cell, species, LOD, accepted/rejected candidate counts, memory, dirty/conflict state, and selection focus.
- [ ] Add transaction-safe undo/redo, save, external-change conflict handling, regeneration, and incremental cook publication.
- [ ] Add focused editor tests and one real-host smoke.

### Acceptance Criteria

- An author can paint, preview, undo, save, regenerate, and recook vegetation without freezing the UI.
- Unchanged cells/pages retain identity and artifacts after localized edits.
- Closing/reloading does not lose dirty/conflict state or publish a partial generation.

---

## Milestone 9 - Measured Command And Memory Scaling

**Goal:** Decide the next rendering contract from evidence rather than assuming vegetation requires a backend-specific GPU-driven rewrite.

### TODO

- [ ] Add Tracy zones/plots for scatter bake, cell read/setup, extraction, culling, LOD, batch build, upload, opaque/shadow recording, submission, and disposal.
- [ ] Plot active cells/clusters/instances, candidates/visible/culled/faded instances, LOD histogram, direct batches/draws, CPU/prepared bytes, descriptors, upload bytes/time, budget stalls, and pending disposal.
- [ ] Establish explicit frame budgets and capture representative near/mid/far camera paths at target density.
- [ ] Measure direct-instanced command pressure and CPU recording cost.
- [ ] If measured limits are exceeded, add a shared backend-neutral indexed-indirect contract.
  - [ ] Expose `DrawIndexedIndirect` through managed RHI and `RenderCommandList` with validated argument layout, alignment, resource state, draw count, stride, and capability limits.
  - [ ] Implement Vulkan execution and focused native/managed contract tests without exposing Vulkan types to vegetation.
  - [ ] Start with deterministic CPU-built indirect arguments; add compute culling/count buffers only after a second measured bottleneck.
- [ ] If direct instancing remains inside budgets, record that result and keep the smaller contract.

### Acceptance Criteria

- The chosen path is supported by saved Tracy evidence and explicit thresholds.
- No optimization adds per-instance managed allocation or backend coupling.
- Memory and command pressure remain bounded under repeated cell streaming and camera traversal.

---

## Milestone 10 - Vegetation Reliability And Production Gate

**Goal:** Promote vegetation to the same deterministic standard as terrain/world streaming.

### TODO

- [ ] Add a package-provided bounded `vegetation-streaming` smoke scenario.
  - [ ] Deterministic near/mid/far path, cell-boundary crossing, one origin rebase, wind samples, and repeated load/reload/unload cycles.
  - [ ] Assert cluster/instance identities, owner cells, generations, culling/LOD/batch parity, bounds, and zero stale draws.
- [ ] Add memory and shutdown checks.
  - [ ] Bound ECS clusters, cooked pages, CPU placement bytes, prepared buffers/textures/descriptors, mesh/material sharing, and deferred disposal.
  - [ ] Require complete task/residency/device-resource drain after package shutdown.
- [ ] Add named color/depth/shadow captures.
  - [ ] Validate nonblank vegetation coverage, upright horizon, alpha/depth consistency, wind movement between selected frames, stationary determinism, LOD transitions, and post-rebase similarity.
- [ ] Integrate package/format/scatter/residency/render/editor tests into `validate_fast.bat`.
- [ ] Integrate Development, Production, Editor, RHIVulkanTesting, and relocated cooked-only Production checks into `validate_runtime.bat`.
- [ ] Run the complete Debug gate with zero source fallback, zero skipped GPU checks, bounded memory, clean shutdown, and empty Vulkan validation logs.

### Acceptance Criteria

- Automated validation catches vegetation payload, identity, placement, cell ownership, LOD, culling, rendering, precision, editor, memory, and shutdown regressions.
- A copied Production output streams and renders the canonical vegetation with no workspace/source/cache access.
- The populated valley is stable and visually strong enough to begin character/gameplay or physics/navigation work without replacing vegetation ownership.

---

## Immediate Implementation Sprint

Implement the first visible vertical slice in this order:

1. [x] create the three vegetation repositories, add submodules, and establish package/profile composition;
2. [ ] define one species asset, one biome asset, and strict deterministic fixtures;
3. [ ] cook one terrain-aware cluster page with stable identities and corruption tests;
4. [ ] add a vegetation cluster scene codec plus cell/residency ownership;
5. [ ] render one cluster as one direct indexed instanced batch through the Generic RP feature;
6. [ ] contribute matching cascaded-shadow work and prove copied Production closure;
7. [ ] extend the fixture to grass, shrub, rock, and tree species across multiple cells before broad Editor tooling.

The first checkpoint is not a complete forest system. It is one package-owned species and one deterministic cluster page, generated from the canonical terrain, owned by a world cell, rendered with one instanced opaque draw and one instanced shadow draw, and validated without source access.

---

## Explicitly Deferred

- SpeedTree compatibility, procedural tree topology, branch/frond generation, and commercial vegetation import SDKs;
- interactive chopping, bending, harvesting, damage, regrowth, fire, and promoted actor lifecycle;
- rigid-body tree collision, grass collision, navmesh obstacle carving, and gameplay cover queries;
- full GPU compute culling, draw-count buffers, mesh shaders, work graphs, and virtualized geometry until measured direct/indirect baselines exist;
- virtual texturing, sparse residency, texture feedback, and install-time page streaming;
- runtime ecological simulation, seasonal succession, weather-driven growth, and server replication;
- distant forest impostor atlases beyond the first measured mesh/billboard LOD need;
- roads/splines, rivers/water, decals, snow accumulation, and terrain deformation integration;
- character animation, gameplay framework, AI, quests, inventory, save games, and multiplayer.

---

## Roadmap Completion Rule

This roadmap is complete only when all milestones and acceptance criteria are implemented, architecture docs describe actual behavior, and the promoted fast/runtime gates pass from regenerated workspaces.

At completion, delete this file and create the next single active roadmap from measured results. The likely candidates are character/gameplay foundations or physics/navigation; choose from the populated outdoor vertical slice's actual bottleneck.
