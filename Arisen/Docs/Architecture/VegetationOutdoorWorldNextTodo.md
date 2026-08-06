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

- the Milestone 1 vegetation package spine, Milestone 2 source-to-cooked formats, and first
  Milestone 3 terrain-aware one-page scatter slice now exist, but valid-empty planning,
  multi-page replacement, broader foliage, and impostor data have not been implemented;
- versioned schemas/codecs plus the scatter planner now derive package-owned cluster/page GUIDs,
  compact canonical instances, exact dependency pins, and one frozen terrain-backed fixture, but
  cluster-closure replacement cannot yet atomically prune stale generated page rows/files;
- the package-owned scene codec, cell activation contract, generation-qualified CPU publication,
  bounded query service, and Generic RP prepared-resource provider bind vegetation clusters to
  world-cell residency; exact-key shared mesh/material leases, immutable cluster instance buffers,
  and direct-instanced opaque/shadow passes now complete the first visible slice;
- ordinary static meshes are entity-oriented and are not an acceptable representation for tens of thousands of plants;
- Generic RP now extracts deterministic cluster components, prepares 48-byte origin-relative GPU
  instances, and contributes one opaque plus four cascade shadow batches for the canonical cluster;
  hierarchical culling/LOD, wind shading, alpha cutout, density scaling, and broad foliage material
  semantics remain absent;
- the existing direct indexed API now has a canonical baseline of one 13-instance opaque batch and
  four 13-instance cascade batches, but representative dense-valley command/memory measurements and
  a shared managed indirect-draw contract remain absent;
- Editor has no biome painting, density masks, exclusion volumes, scatter preview, instance inspection, or regeneration transaction;
- Development, Editor, Production, and relocated cooked-only Production now validate the canonical
  cluster identity, direct-instanced counts, visible opaque/depth coverage, shadow-only color
  contribution, closure, and shutdown; LOD, density, wind, origin-rebase, and multi-cell vegetation
  stress coverage remain later work.

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
- Milestone 3 remains active; its first deterministic one-page scatter slice is complete, while
  explicit empty output, multi-page replacement, and atomic stale-page pruning remain open.

---

## Milestone 2 - Species, Biome, And Cooked Cluster Assets

**Goal:** Define deterministic authoring and runtime data before renderer-specific structures leak into source assets.

### TODO

- [x] Define a vegetation species asset.
  - [x] Stable GUID/package identity, bounded mesh/material LOD list, shadow policy, scale/yaw/tilt ranges, collision-promotion metadata, and wind response.
  - [x] Validate mesh/material dependencies and finite ordered distance/error ranges.
- [x] Define a biome/scatter-rule profile.
  - [x] Ordered species entries with density, seed salt, altitude/slope/layer-weight rules, minimum spacing, cluster size, and exclusion policy.
  - [x] Keep quality/runtime density multipliers out of stable source identity unless they change cooked placement.
- [x] Define versioned cooked species and biome containers.
  - [x] Use fixed-width little-endian headers, magic/version/hash, bounded section tables, exact alignment, and explicit dependency identities.
- [x] Add strict species/biome readers, deterministic writers, and corruption tests.
  - [x] Reject unsupported required sections, malformed counts/offsets, invalid identity/ranges/order, duplicate entry/rule IDs, and missing or mismatched dependency ownership.
  - [x] Prove identical validated descriptors produce byte-identical species/biome payloads.
- [x] Define versioned cooked cluster and instance-page containers.
  - [x] Store compact local position, packed orientation, uniform scale, conservative radius/bounds, stable instance key, and canonical species index. LOD acceleration remains deferred to Milestone 6.
- [x] Add strict cluster/page readers, deterministic writers, and corruption tests.
  - [x] Reject unsupported required sections, malformed counts/offsets, non-finite transforms, bad quaternions/scales, duplicate IDs, invalid bounds, and missing dependencies.
  - [x] Prove unchanged inputs preserve cluster/page timestamps and artifact generations.

### Acceptance Criteria

- Source assets contain durable design intent; cooked assets contain bounded runtime-ready placement.
- Instance and cluster identity is independent from transient render LOD, buffer offsets, and backend handles.
- Production closure needs no vegetation authoring files.

### Milestone 2 Completion Record

- Instance pages use `runtime.vegetation-instance-page.v1`, `ARIVPAGE`, and
  `.arivegetationpage`; cluster roots use `runtime.vegetation-cluster.v1`,
  `ARIVCLUS`, and `.arivegetationcluster`.
- Canonical readers/writers enforce bounded sections, transforms, species/page
  order, cross-page instance-key uniqueness, authored and cooked biome species
  membership, exact page byte-size/SHA-256 pins, dependency closure, and root
  bounds equal to the page-bounds union.
- Format-v1 cluster/page GUIDs are explicit caller-supplied identities. Page GUIDs
  are immutable publication identities: byte-identical recooks preserve path,
  timestamp, and registry generation, while changed bytes require a new page GUID.
- `46/46` focused vegetation tests pass, including rehashed corruption, exact-pin
  tampering, unchanged artifact reuse, immutable page identity, and deployment
  closure from initially uncooked authored dependencies.
- Terrain sampling, one-page scatter generation, generated GUID derivation, and canonical
  cell ownership are now implemented by the first Milestone 3 slice. Explicit empty output,
  multi-page planning, and transactional page-set replacement remain open.

---

## Milestone 3 - Deterministic Terrain-Aware Scatter Baking

**Goal:** Turn biome rules plus terrain data into stable cell-partitioned instance pages.

### TODO

- [x] Implement canonical candidate generation from integer spatial cells and explicit seeds.
  - [x] Use a documented deterministic hash/sequence with no global mutable RNG.
  - [x] Make output independent of task scheduling and dictionary iteration.
- [x] Evaluate terrain placement against cooked root/tile samples.
  - [x] Height, normal, slope, altitude, and normalized layer weights.
  - [x] Stable positive-border ownership matching terrain/world-cell policy.
- [x] Apply density, spacing, exclusion, and overlap rules deterministically.
  - [x] Bound candidate and accepted counts per source tile/cell.
  - [x] Report overflow or invalid rules instead of silently truncating near content.
- [ ] Partition accepted instances into stable clusters/pages owned by world cells.
  - [x] Derive generated cluster/page GUIDs from biome, terrain, species, cell, and canonical content identity.
  - [ ] Return an explicit no-output plan for valid empty cells and partition larger accepted sets into bounded pages.
  - [ ] Reuse unchanged pages and transactionally remove stale pages/catalog rows.
    - Format v1 already reuses byte-identical artifacts and rejects changed bytes
      under an existing page GUID. The one-page planner now derives replacement
      GUIDs from canonical content; atomically switching the cluster/catalog
      closure, removing stale rows/files, and rolling back a failed replacement
      remain part of this unchecked item.
- [x] Add determinism, border, and parallel-bake tests for the one-page slice.

### Acceptance Criteria

- Rebuilding with different worker scheduling produces byte-identical cluster artifacts.
- Adjacent terrain/cell borders neither duplicate nor omit accepted candidates.
- Runtime never reruns source scatter rules during ordinary frame rendering.

### Immediate Sprint Item 3 Completion Record

- The public scatter entry loads cooked biome/species/terrain data through
  `IAssetDatabase` and verifies every terrain tile against the root's exact
  size/SHA-256 pin. Direct decoded-record baking is internal test surface only.
- Candidate generation, terrain/rule/exclusion filtering, spacing halo,
  cell-local float narrowing, positive-border ownership, reconciled metrics,
  generated identities, and codec-canonical page hashing are deterministic and
  bounded. Sparse spacing-sized buckets contain only already visited lower-key
  candidates, the maximum `1,048,576`-candidate fixture completes with
  reconciled metrics, and overflow fails immediately instead of returning an
  unpublishable descriptor.
- The canonical Showcase fixture produces one frozen terrain-backed page and
  publishes it through the existing cluster cooker; rehashed non-finite page
  corruption is rejected through both direct page and cluster-closure loads.
- Focused terrain/query plus scatter/showcase coverage passes `31/31`, and the
  package-boundary test proves vegetation depends on terrain without a reverse
  dependency.
- Final validation passes BuildTool `73/73`, kernel `72/72`, launcher `18/18`,
  and rendering/asset/Editor `694/694`. The schema-7 Debug runtime report at
  `.arisen/Logs/validate-runtime-Debug-latest.json` records `succeeded=true`,
  four GPU smoke runs with zero skips/fallbacks, three world-streaming runs,
  three terrain-streaming runs, the real Editor viewport smoke, relocated
  cooked-only Production, and no reported failure.
- This record completes Immediate Sprint item 3 only. Valid empty/no-output
  planning, multi-page partitioning, and atomic stale-page replacement remain
  unchecked Milestone 3 work.

---

## Milestone 4 - Scene Components, Cell Ownership, And Residency

**Goal:** Bind cooked vegetation clusters to the world-streaming lifecycle without per-instance ECS overhead.

### TODO

- [x] Add a package-owned scene extension for vegetation cluster references.
  - [x] Store biome/species/cluster GUIDs, owning cell, double-world bounds/origin, visibility flags, and quality group.
  - [x] Use one blittable ECS component per cluster/page, not per instance.
  - [x] Reject duplicate exclusive cluster ownership and invalid cell/bounds pairings.
- [x] Add runtime CPU publication and optional query service.
  - [x] Generation-qualify immutable cluster pages.
  - [x] Return explicit unavailable/outside states and bounded nearby-instance results for future promotion/gameplay systems.
- [x] Integrate generic residency.
  - [x] Workers acquire and validate cooked cluster pages and dependencies.
  - [x] Cells remain `WaitingForResources` until required prepared resources are ready.
  - [x] Share species mesh/material resources while cluster instance buffers remain independently evictable.
  - [x] Release only after ECS unload and defer device destruction through the latest submission ticket.
- [x] Add cancellation, retry, shared-species, LRU, stale-generation, and shutdown-drain tests.

### Acceptance Criteria

- Every visible cluster has one active/pinned cell owner and generation-matched CPU/GPU residency.
- Unloaded cells expose no queryable or drawable vegetation instance.
- Repeated load/unload returns entity slots, cooked handles, instance pages, descriptors, and native resources to baseline.

### Immediate Sprint Item 4 Completion Record

- `VegetationClusterSceneComponentCodec` publishes a strict source/cooked schema with canonical
  big-endian GUID identity, exact dependency variants, world-cell ownership, double-world bounds,
  visibility/shadow flags, quality group, and one blittable `VegetationClusterComponent` per
  cluster entity. Activation validators receive the owning cell context and run before ECS
  mutation; duplicate exclusive cluster identities are rejected. Source scene staging validates
  the already-published generated cluster/page closure and discovers current authored biome,
  species, and LOD dependencies without requiring cooked biome/species artifacts, so a clean scene
  root closes through the coordinator. Cooked scene staging instead requires the exact cooked
  cluster, biome, and every cooked biome species with no source fallback. Both paths require the
  cluster's sole canonical species to be a biome member and emit the same flattened dependency plan.
- `VegetationRuntimeDataStore` prepares cluster/page records on workers and atomically publishes
  immutable generation-qualified snapshots. The bounded query service never reads a stale or
  inactive generation, and exact cooked page size/SHA-256 identity survives publication without a
  production reserialization step.
- Generic RP registers a CPU-only vegetation prepared provider that decodes exact residency-held
  handles. Cluster/page claims bind and validate the reciprocal biome/species/page/parent closure,
  including schema, exact page size/SHA-256 pins, species union, counts, bounds, biome membership,
  and cross-page stable keys; species and biome claims bind mesh/material and species dependencies.
  The root and dependency claims, canonical binding, and owner-plan generation are revalidated
  under active publication admission before and after the external publication callback. Stale
  publication is rolled back to `Waiting`, while a current owner that omits a decoded dependency
  fails deterministically.
- Required cluster/page/species/biome keys keep a cell in `WaitingForResources` until worker
  validation and frame-boundary publication complete. Cancellation, stale claims, cleanup retry,
  shared species keys, incompatible shared-owner rejection, independent page eviction, projected
  LRU budget selection, and provider shutdown are covered.
- Prepared-provider admission excludes lifecycle release and coherent metrics sampling from an
  in-flight setup callback without holding the residency state gate across package code. Atomic
  publication blocks claim-mutating owner attachment/release/rollback, and post-callback claim
  validation catches dependency-provider invalidation before `Ready` is committed. Every world
  lifecycle operation rejects reentry from acquisition, `Prepare`, prepared-publication, and
  provider-lifecycle callbacks before waiting for the world lifecycle gate.
- Duplicate acquisitions of one exact cooked-handle generation retain one cleanup row per logical
  reference but one transferable CPU-byte charge. Losing-racer cleanup remains shareable while a
  live winner exists; once that winner is evicted, outstanding failed cleanup blocks reacquisition
  until its deterministic retry succeeds.
- Deferred startup-world activation publishes a state-gate-coherent revision containing the active
  asset/GUID and pending winner. Each Editor viewport subscribes before its initial snapshot read,
  never rearms over an active world, and follows the current pending winner across restart or
  supersession. An armed barrier prioritizes a pending successor over an outgoing active world in
  the same revision. Matching callback/current revisions reject stale and B-to-C-to-B ABA
  observations; terminal empty state releases without activation. Once the coherent winner activates, the visual
  remains detached while outputs through that exact ticket boundary are consumed with the normal
  compositor semaphore handshake, and attaches only after a newer output is accepted and reported
  consumed while the same activation revision remains current.
- Focused validation passes `RuntimeWorldStreamingTests` `39/39`,
  `RuntimeAssetResidencyTests` `38/38`, `VegetationResidencyCoordinationTests` `36/36`, and all
  vegetation-filtered tests `123/123`.
  The complete unfiltered Debug rendering/asset/Editor surface passes `827/827` with zero skips.
  Fast validation covers the same `827` unique tests as `826/826` non-allocation Debug tests plus
  the exact-allocation test `1/1` in a fresh Release host with tiered compilation disabled.
- Final fast validation passes BuildTool `73/73`, kernel `102/102`, launcher `18/18`, the rendering
  split above, and all four profile graphs. The schema-7 Debug runtime report at
  `Arisen/Development/PackageGame/.arisen/Logs/validate-runtime-Debug-latest.json` records
  `succeeded=true`, `exitCode=0`, and `gpuAvailable=true`: four GPU smoke runs with zero skips or
  CPU fallbacks and two visual-summary artifacts; three world-streaming runs with two summary
  artifacts; three terrain-streaming runs with two summary artifacts; one real Editor viewport
  run/artifact; one relocated cooked-only Production run/artifact; all four profiles passed and
  no failure was reported.
- This record completes Immediate Sprint item 4. Exact-key shared mesh/material leases, GPU cluster
  instance buffers, deferred native destruction, extraction, and RenderGraph passes are completed by
  the following Immediate Sprint item 5/6 record.

---

## Milestone 5 - Direct-Instanced Generic RP Vertical Slice

**Goal:** Render one cooked species cluster through existing backend-neutral APIs before adding broad optimization machinery.

### TODO

- [x] Prepare immutable species mesh/material resources and one cluster instance storage buffer outside command recording.
- [x] Define compact origin-relative GPU instance records with transform, stable variation, wind phase, color variation, and selection/debug flags.
- [x] Extract active cluster components in deterministic biome/cell/species/cluster order into reusable arrays.
- [x] Group compatible instances by mesh/material/LOD/shadow state.
- [x] Add opaque and directional-shadow vegetation passes through `IGenericRenderPipelineFeature`.
  - [x] Use `RenderCommandList.DrawIndexed` with positive `instanceCount` and `firstInstance` ranges.
  - [x] Declare graph-owned HDR color, depth, and cascade-array access explicitly.
  - [x] Keep asset lookup, culling, LOD, upload, and pipeline creation outside recording.
- [x] Add visual and command-contract tests for one canonical multi-instance rock cluster.

### Acceptance Criteria

- The first cluster renders in Development, Editor SceneView/GameView, Production, and copied Production.
- One draw represents many instances and native interop is batched.
- The adapter contains no Vulkan type or concrete backend dependency.

### Immediate Sprint Item 5/6 Completion Record

- Generic RP exposes a narrow prepared-asset source whose exact residency-key mesh and material
  leases carry device and publication generations. Caller-thread release synchronously tombstones
  the key, setup entry points retire physical resources in FIFO order, and coherent metrics are
  published atomically without steady-state allocation. A stale material publication never replaces
  the current resource while residency remains `Ready`; invalidation transitions ownership through
  `Waiting` before a replacement can publish.
- The vegetation adapter packs the canonical cooked page into one immutable storage buffer of
  48-byte origin-relative instance records. Stable-key ordering produces one compatible LOD-0 batch
  with 13 instances while retaining the exact Generic RP mesh/material publications. Cluster buffers
  remain independently evictable, and final buffer, descriptor, mesh, and material release is
  deferred through the latest submitted ticket.
- `VegetationClusterRenderSource` scans the contiguous ECS component pool into reusable storage and
  applies deterministic biome/world/cell/species/cluster ordering. Feature preparation joins only
  matching active CPU, residency, prepared-resource, and RHI generations before expanding cached
  opaque and cascade draw ranges.
- `VegetationOpaquePass` records one direct indexed batch with 13 instances into graph-owned HDR
  color/depth. `VegetationShadowPass` records one 13-instance batch for each of four directional
  cascades. Both use backend-neutral `RenderCommandList` calls and pre-created shaders, pipelines,
  buffers, bindless data, and constants; command recording performs no service lookup, asset access,
  upload, pipeline creation, managed allocation, or lock.
- The per-surface/device-generation `[Vegetation.GenericRP.Validation]` record requires the exact
  canonical cluster/species identity, opaque `1/13`, shadow `4/52`, per-cascade `1/13`, zero drops,
  and a positive submission ticket. Editor validation requires records from two distinct surfaces.
- `ARISEN_VEGETATION_RENDER_VALIDATION_MODE` provides fail-closed `disabled`, `opaque-only`, and
  `full` process-start modes. The world-streaming `during` checkpoint owns the exact center cell and
  fixed camera state: disabled-to-opaque changes meaningful color and depth coverage, while
  opaque-only-to-full preserves frame depth exactly and produces a measurable darker color delta,
  isolating the vegetation shadow contribution. The same comparison runs against relocated
  cooked-only Production output with no source fallback.
- Final validation on 2026-08-06 passed the isolated Vulkan Release suite `27/27` with an empty
  validation log and the full Debug schema-8 runtime gate with four profile smokes, three world and
  three terrain runs, two vegetation visual comparisons retaining three summaries, one real Editor
  viewport run, and one relocated cooked-only Production run. All ten retained Vulkan validation
  logs were empty; the gate reported zero skips, zero CPU fallbacks, and no failure.

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
2. [x] define one species asset, one biome asset, and strict deterministic fixtures;
3. [x] cook one terrain-aware cluster page with stable identities and corruption tests;
4. [x] add a vegetation cluster scene codec plus cell/residency ownership;
5. [x] render one cluster as one direct indexed instanced batch through the Generic RP feature;
6. [x] contribute matching cascaded-shadow work and prove copied Production closure;
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
