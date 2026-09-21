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
- deterministic terrain LOD and shared-edge topology with zero seam violations across the canonical sixteen-tile fixture;
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
  Milestone 3 terrain-aware scatter slice now exist, while broader foliage and impostor
  data have not been implemented;
- versioned schemas/codecs plus the scatter planner now derive package-owned cluster/page GUIDs,
  compact canonical instances, exact dependency pins, and one frozen terrain-backed fixture;
- the package-owned scene codec, cell activation contract, generation-qualified CPU publication,
  bounded query service, and Generic RP prepared-resource provider bind vegetation clusters to
  world-cell residency; exact-key shared mesh/material leases, immutable cluster instance buffers,
  and direct-instanced opaque/shadow passes now complete the first visible slice;
- ordinary static meshes are entity-oriented and are not an acceptable representation for tens of thousands of plants;
- Generic RP now extracts deterministic cluster components, prepares 48-byte origin-relative GPU
  instances, and contributes one opaque plus four cascade shadow batches for the canonical cluster;
  cooking-side acceleration and runtime setup-owned hierarchical culling/LOD now exist, while wind
  shading, alpha cutout, deterministic dither/fade, and broad foliage material semantics remain
  absent;
- the existing direct indexed API now has a canonical baseline of one 13-instance opaque batch and
  four 13-instance cascade batches; the dense-valley camera-path measurement extends that baseline
  to 2,048 resident clusters and 524,288 instances (1,662 opaque plus 6,648 cascade draws and
  19.48 MiB of selected instance payload on the busiest frame), while a shared managed
  indirect-draw contract remains absent;
- Editor has no biome painting, density masks, exclusion volumes, scatter preview, instance inspection, or regeneration transaction;
- Development, Editor, Production, and relocated cooked-only Production now validate the canonical
  cluster identity, direct-instanced counts, visible opaque/depth coverage, shadow-only color
  contribution, closure, and shutdown; focused planner tests cover LOD, density, budgets,
  origin-rebase identity, multi-kilometer distance culling with exact double-precision ranking,
  negative-coordinate symmetry, budget fragmentation, and deterministic camera paths across
  rebased origins, while wind, multi-cell camera-path stress, and GPU visual LOD coverage remain
  later work.

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
- Milestone 3 remains active; deterministic empty output, bounded multi-page planning, and
  transactional page replacement are complete, while broader foliage and impostor data remain open.

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
  - [x] Store compact local position, packed orientation, uniform scale, conservative radius/bounds, stable instance key, and canonical species index. Runtime culling/LOD selection remains deferred to Milestone 6.
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
- Terrain sampling, scatter generation, generated GUID derivation, canonical cell ownership,
  explicit empty output, bounded multi-page planning, and transactional page-set replacement
  are now implemented by the first Milestone 3 slices.

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
- [x] Partition accepted instances into stable clusters/pages owned by world cells.
  - [x] Derive generated cluster/page GUIDs from biome, terrain, species, cell, and canonical content identity.
  - [x] Return an explicit no-output plan for valid empty cells.
  - [x] Partition larger accepted sets into bounded pages using the authored `ClusterSize` as the
    per-page target while enforcing cooked cluster page/count limits.
  - [x] Reuse unchanged pages and transactionally remove stale pages/catalog rows.
    - Format v1 reuses byte-identical artifacts and rejects changed bytes under an
      existing page GUID. The recipe generator stages the complete generated source
      set, preserves unchanged page identities, removes stale generated files and
      exact cooked rows, and restores the prior source/cooked closure on failure.
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
- This record completes valid-empty planning, bounded multi-page partitioning, and
  transactional page replacement within Milestone 3. The baker returns an explicit
  no-output plan with deterministic empty placement identity and reconciled metrics,
  and partitions larger accepted sets by stable-key order into pages bounded by the
  authored `ClusterSize`. The recipe generator publishes each page with its own
  content hash, removes stale generated pages/catalog rows by exact identity, and
  restores the previous generated/cooked closure when source refresh or manifest
  publication fails.

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

- [x] Build conservative cluster/page acceleration during cooking.
  - [x] World bounds, spatial hierarchy, per-species ranges, and LOD error/radius data.
- [x] Implement reusable setup-owned culling and LOD preparation.
  - [x] Double-world camera input, origin-relative float bounds, frustum and distance/error culling, quality density, and hard batch/instance budgets.
  - [x] LOD hysteresis; deterministic dither/fade bands remain deferred to the material/visual slice.
  - [x] Stable nearest-first overflow behavior with explicit diagnostics.
  - [x] Resolve each extracted cluster's resident record and planner selection through ordered
    lookups; the former per-extracted-cluster scans made prepared-draw setup quadratic in the
    resident cluster count.
- [x] Preserve identity across origin rebasing.
  - [x] Rebase changes GPU representation, not accepted instances or stable selection identity; LOD decisions remain held inside the defined hysteresis band.
- [x] Split large batch ranges into bounded TaskGraph setup/recording work while preserving deterministic submission order.
  - [x] Partition recording into bounded work items: the opaque pass records 256-draw ranges and the
    shadow pass now partitions each cascade the same way, so the dense-valley peak of 6,648 cascade
    draws becomes 26 independent recording tasks that still submit in cascade/range order.
  - [x] Partition per-cluster culling and prepared-draw setup across TaskGraph workers.
    - [x] `VegetationSetupWorkPartition` bounds every setup work item to 256 inputs; gather and
      prepared-draw setup dispatch through `VegetationSetupWorkDispatcher` and merge shard regions
      in work-item order, which reconstitutes the serial output order.
    - [x] `VegetationCullingPlanner` evaluates those bounded ranges with per-shard candidate, page,
      and node scratch, then merges shard candidates in work-item order ahead of the existing
      identity sort, history, and budget stages so scheduling cannot change the selection.
    - [x] Draw emission, budget selection, and shadow recording stay serial by design.
- [x] Add multi-kilometer, negative-coordinate, and camera-path stress tests; focused rebase, overflow, and zero-steady-state-allocation tests are present.
  - [x] Cover multi-kilometer distance culling, sub-meter nearest-first ranking at 10,000 km, and
    rebase-invariant selection at that magnitude.
  - [x] Cover negative-coordinate symmetry, instance-budget fragmentation, dense 256-cluster
    overflow, deterministic 65-frame rebased camera-path replay, and path-wide zero allocation.

### Acceptance Criteria

- Camera movement and rebasing do not reshuffle stable instances or visibly pop unchanged LODs.
- Culling/LOD hot paths reuse contiguous storage and allocate no managed objects after warmup.
- Overflow is measurable and deterministic.

### Milestone 6 Culling/LOD Slice Completion Record

- Instance pages now publish an optional fixed-width acceleration section with
  canonical per-species instance index ranges, exact species bounds, counts, and
  maximum conservative radii. Readers validate those records against decoded
  instances and reconstruct them for legacy v1 pages that omit the extension.
- Cluster roots now publish paired optional sections containing a deterministic
  median-split page hierarchy and per-species page coverage, bounds, radius, and
  farthest LOD distance/screen-error thresholds. The hierarchy is conservative,
  covers every page exactly once, and rejects unreachable or malformed nodes.
- Generic RP setup now consumes that acceleration through `VegetationCullingPlanner`. The planner
  validates generation-qualified ownership, traverses hierarchy/page ranges, rejects invalid or
  off-view bounds, computes distance and projected error, applies authored LOD hysteresis,
  deterministic density, and nearest-first batch/instance budgets, and retains stable output order.
  Reusable arrays keep warmed calls allocation-free; planner metrics are plotted alongside prepared
  and submitted draw metrics, and release/reset clears selection history.
- Focused tests cover distance LOD, frustum rejection, nearest-first overflow, hysteresis, origin
  rebasing identity, invalid-input fail-closed behavior, zero steady-state allocation,
  multi-kilometer distance culling, sub-meter ranking at 10,000 km, negative-coordinate symmetry,
  instance-budget fragmentation, dense 256-cluster overflow, and a 65-frame rebased camera path
  that replays identically with zero allocation after warmup. Deterministic dither/fade remains the
  next Milestone 6 work; TaskGraph range partitioning landed with the setup and culling shards
  recorded below.
- `VegetationDenseValleyMeasurementTests` records the dense-valley planning and command baseline on a
  24-frame, 2,400 m camera pass over 48 m cluster cells with a 2,000 m farthest-LOD limit and four
  shadow cascades: 256/1,024/2,048 resident clusters hold 16,384/131,072/524,288 instances, and the
  busiest frames plan 256/994/1,662 candidates, accept 1,662 clusters with 425,472 selected
  instances (19.48 MiB at the 48-byte GPU instance stride), and project 1,662 opaque plus 6,648
  cascade-shadow draws. In the Release allocation host with tiered compilation disabled, planning
  costs 40-43/156-178/302-332 us on average and 80-85/349-366/643-718 us at the worst frame,
  replays byte-identically, and allocates nothing after warmup. Because each scale also culls every
  resident cluster at some frame while still traversing it, planner cost is linear in the resident
  cluster count (about 0.15 us per cluster along this path), and draw pressure is dominated by the
  four cascade-batch sets. These numbers measure the planner alone, and the surrounding feature
  setup was quadratic in the resident cluster count until the ordered lookups below landed, so
  setup was the first TaskGraph partitioning target; these counts are the baseline for the
  Milestone 9 indirect-contract decision.
- Prepared-draw setup is no longer a linear scan per extracted cluster.
  `VegetationGenericRenderPipelineFeature` now resolves resident records and planner selections
  through `VegetationClusterLookup`, which binary-searches the Guid-ordered resident snapshot and
  the planner's `(cluster, generation, species)`-ordered selection output, rewinding to the first
  entry of a duplicate-cluster run so multi-species clusters keep the exact selection the former
  scan returned. The dense-valley lookup report (2,048 resident clusters, 1,662 probes, 4,096
  selections) measures 103.5 us ordered versus 720.1 us linear for resident clusters and 96.0 us
  ordered versus 1,584.5 us linear for selections, all allocation-free, replacing roughly 2.3 ms of
  quadratic setup per dense-valley peak frame with roughly 200 us. `VegetationClusterLookupTests`
  covers found, missing, boundary, and out-of-range Guids, sliced spans, duplicate-run ordering,
  and equivalence against the removed linear scans.
- Vegetation shadow recording is now partitioned by cascade through
  `VegetationShadowDrawWorkPartition`: every cascade contributes bounded 256-draw work items on the
  shared TaskGraph, work items are dispatched in cascade/range order so submission stays
  deterministic, and per-work-item batch/instance counters accumulate through interlocked adds. The
  canonical 13-instance cluster still records `OpaqueBatches=1`, `RecordedShadowBatches=4`, and
  `ShadowBatches=1,1,1,1` in the runtime gate, and the 2026-09-18 Debug runtime gate passed with
  zero skips and two vegetation visual comparisons after the change.
- The 2026-09-18 Debug runtime gate passed end to end after this slice. The schema-8 report at
  `.arisen/Logs/validate-runtime-Debug-latest.json` records `succeeded=true` with four GPU smoke
  runs, zero skips or CPU fallbacks, one Editor viewport smoke, relocated cooked-only Production,
  three world-streaming runs, three terrain-streaming runs, and two vegetation visual comparisons.
  The same gate re-passed unchanged after the ordered setup lookups landed, with the canonical
  vegetation contract line still `PreparedClusters=1`, `OpaqueBatches=1`, `OpaqueInstances=13`,
  `RecordedShadowBatches=4`, `ShadowBatches=1,1,1,1`, and `Dropped=0`.
- Per-cluster culling and prepared-draw setup now dispatch across bounded TaskGraph work items and
  reconstitute the serial output order afterwards. `VegetationSetupWorkPartition` caps every work
  item at 256 inputs; gather and prepared-frame setup run through `VegetationSetupWorkDispatcher`,
  and `VegetationCullingPlanner` evaluates the same ranges with per-shard candidate, page, and node
  scratch before merging shard candidates in work-item order ahead of the identity sort, history,
  and budget stages. The dense-valley report
  (`TaskGraphSetupShardingReportIsDeterministicAndAllocationFreeInline`, 2,048 and 10,240 clusters on
  the 24-frame path) measures at 10,240 clusters / 163,840 instances / 40 work items in Release:
  gather 942.9 to 327.3 us, plan 1,565.5 to 858.7 us, prepared-frame setup 359.2 to 183.8 us, and
  gather+prepare 1,302.1 to 511.1 us; the controlled Debug A/B measured the plan at 6,011-6,017 us
  for the former serial loop, 6,165-6,263 us with shard bookkeeping run inline (+2.5%), and
  2,128-2,179 us dispatched. The inline shard path stays allocation-free after warmup, the dispatched
  path adds only TaskGraph-owned work items, and draw emission, budget selection, and shadow
  recording remain serial so submission order and the runtime contract line are unchanged.
  `VegetationCullingPlannerTests` covers inline-versus-dispatched equivalence across a five-frame
  multi-shard path, zero steady-state allocation for the inline shard path, and replay under
  barrier-synchronized workers, and the dense-valley report now folds plan selections into its
  serial-versus-sharded fingerprint.

---

## Milestone 7 - Vegetation Materials, Wind, Lighting, And Shadows

**Goal:** Make the populated valley visually credible under the existing outdoor renderer.

### TODO

- [x] Add bounded vegetation material semantics.
  - [x] Mipped sRGB albedo/opacity, renormalized normal maps, linear ORM, alpha cutoff, two-sided normal policy, tint variation, and roughness response.
  - [x] Reject missing mip/format/semantic dependencies during cooking.
- [x] Add deterministic wind inputs.
  - [x] Global wind direction/strength plus species stiffness, height response, per-instance phase, and bounded gust parameters.
  - [x] Keep simulation values frame-global/species-global; do not update managed state per blade/tree.
- [x] Integrate PBR environment/direct light and four cascade shadows.
  - [x] Alpha-consistent depth/shadow coverage.
  - [x] Conservative wind-expanded bounds for culling and shadow fitting.
- [x] Add distance fade/dither and color/depth/shadow visual tests.
- [x] Build a canonical fixture with at least grass, shrub, rock, and tree species using inspectable non-placeholder assets.

### Acceptance Criteria

- The valley reads as populated outdoors rather than repeated static cutouts.
- Wind does not break bounds, shadows, depth, or origin-rebase stability.
- Near/mid/far visual captures retain valid silhouette, depth, material, and cascade coverage.

### Milestone 7 Completion Record

- Vegetation materials now declare bounded PBR semantics that cooking and loading enforce. The shared material contract
  gained mipped sRGB albedo/opacity, renormalized normal maps (`MipFilter: NormalMap`), linear ORM, alpha cutoff, a
  two-sided normal policy, tint variation, and roughness response, and the four canonical species materials use it: base
  color and normal plus ORM slots, `MetallicFactor`, `RoughnessFactor`, `OcclusionStrength`, `AlphaCutoff`,
  `TintVariation`, and `BaseColorFactor`. `VegetationMaterialContractTests` proves the opaque two-sided render state, the
  authored alpha factor, factor ranges, bindless-descriptor and cooked-variant requirements, and the rejection paths for
  materials that violate the shader contract; `RenderingAssetPipelineTests` keeps the shared PBR conventions, shader
  contract annotations, and texture-binding metadata honest; `VegetationRuntimeAssetCookingTests` proves the cook closes
  biome/species/mesh/material dependencies before a page renders.
- Wind is deterministic and bounded end to end. `VegetationWindSettings` clamps direction (planar-normalized), strength,
  gust amplitude and frequency, and accumulated time into a documented domain, wraps both gust phases on the CPU, and
  derives species-independent bounds (`MaximumBendFraction`, `MaximumHorizontalDisplacement`) from the same constants the
  shaders consume, so the gust envelope can never displace a vertex farther than its own height above the instance origin.
  Direction/strength and gust phases travel in the shared frame constants while the per-instance phase and stable
  variation stay in the canonical 48-byte GPU instance record at offset 32, so a frame never updates managed per-blade or
  per-tree state. `VegetationWindTests` covers the bounded domain, long-session phase wrapping, bend saturation,
  height-bounded travel, and the deterministic override; the render-pass contract tests cover shader/CPU parity, the
  height-bounded displacement shared by both shaders, and the undisplaced-origin coverage contract.
- Lighting and shadows integrated without weakening earlier gates. Wind-expanded page/cluster bounds are baked into the
  cooked payloads, and culling plus per-cascade shadow submission operate on those conservative bounds, so no reachable
  gust can move a visible instance outside its culling or shadow-fit budget; the shadow pass derives the same fade and
  alpha coverage as the opaque pass from the undisplaced instance origin, so depth, coverage, and silhouettes agree and
  never depend on wind. `validate_cascaded_shadow_visuals.ps1` still proves four increasing cascades with positive mesh and
  terrain work across the near/mid/far path, and `OpaqueShadowReceptionIsResolvedFromTheClusterBeforeRecording` keeps the
  per-cluster shadow source exact.
- Distance fade/dither is resolved from the undisplaced instance origin and shared by the opaque and shadow passes, which
  keeps coverage stable under wind and unable to disagree between color, depth, and shadow output. Color/depth/shadow
  visual tests now run as three process modes through `validate_vegetation_rendering_visuals.ps1`: `disabled` proves
  vegetation contributes measurable color and depth coverage, `opaque-only` proves the shadow passes change nothing about
  frame depth, and `full` proves the shadow contribution darkens the frame again.
- Cross-process visual validation is now well defined instead of accidentally flaky. Wind is the renderer's only
  wall-clock-driven input and the compared runs are independent processes, so every run launched by a comparison harness
  declares itself through `ARISEN_VEGETATION_RENDER_VALIDATION_MODE` (`full`, `opaque-only`, or `disabled`) and pins the
  wind clock to the shared `VegetationRenderValidationPolicy.ComparableWindTimeSeconds` phase through
  `IVegetationWindClockControl` before the feature is prepared. The previously failing shadow-only check now passes: the
  `opaque-only` and `full` `during` captures report the same frame-depth SHA-256 and the same written-depth pixel count,
  while `disabled` still differs in color and depth by the required margins and `full` still darkens more than
  `opaque-only`. `EveryDeclaredComparisonRunPinsTheSharedWindClock`, `ComparisonRunPinsTheWindClockDuringPackageLoad`, and
  `WindClockControlPinsThePoseIndependentlyOfTheWallClock` cover the policy, the package-load pin, and the wind contract.
- The stationary-frame clock pin is owned by the world-streaming scenario: `Time.Pin`/`Time.Unpin` freeze the animation
  clock for the blocking `shadow-far`/`shadow-far-stable` pair and release it before `after`, and `RuntimeWorldStreamingTests`
  asserts exactly one pinned frame, consecutive byte-identical stationary captures, and a released clock after shutdown.
  `TimePinTests` covers the kernel contract, and `KernelGlobalStateCollection` keeps the kernel tests that share the static
  clock from running concurrently with a kernel reset that would release another test's pin.
- The canonical fixture is four inspectable, non-placeholder species: a tapered two-tier tree, a three-blade grass tuft, a
  shrub, and a boulder, each with authored mesh, material, and texture assets plus a species LOD list, and a biome whose
  scatter rules bake the canonical center cell into Tree 9, Rock 13, Shrub 36, and Grass 422 instances. Placement, page,
  and cluster identities and the baked conservative bounds are pinned in `VegetationCanonicalFixture` and re-verified by
  `VegetationSourceAssetTests`, `VegetationSpeciesFixtureIntegrityTests`, and the showcase scene asset tests.
- Wind, view projection, and depth coverage are now origin-rebase invariant. The gust phase is anchored to an explicit
  frame anchor instead of a derived origin-relative instance position: `VegetationWindSettings.ResolveGustPhases(WorldPosition)`
  folds `dot(direction, anchor.xz) * GustSpatialFrequency` into both CPU-wrapped phases, so a vertex no longer reads the
  camera's streaming origin and a remote tree still moves on the gust actually reaching it. Both vegetation passes then
  render in the *view frame*, in which the render camera is the origin: `Camera.ViewRelativeViewMatrix` is the same
  rotation-only basis the directional-shadow fitter already used, the generic pipeline publishes `ViewRelativeViewProjection`
  beside the world-frame `ViewProjection`, `VegetationViewFrame.ToViewRelativePosition` subtracts the camera's
  double-precision world position from each cluster origin before the float cast, and `VegetationShadow.hlsl` pushes that
  frame-relative origin straight through the rotation-only cascade matrix instead of reconstructing a world position and
  subtracting a camera vector. `VegetationOpaqueFrameConstants` consequently shrank from 160 to 144 bytes, the shadow shader
  no longer declares `VEGETATION_WIND_SHADOW_CAMERA_VECTOR`, and the frame's camera vector is `float3(0)` because the camera
  *is* the origin of this frame. The terrain-streaming scenario additionally pins the animation clock (`Time.Pin`) for the
  whole comparison window, so neither replay pair - the `far-cascade`/`post-rebase` rebase pair and the
  `near`/`returned-start` return pair - can be separated by a gust, and unpins on every failure, shutdown, and
  early-unload path. The rebase is isolated instead of folded into a camera path: the fixture parks the camera on the far
  pose and moves only the streaming source across the rebase threshold, so the origin is the only variable left between the
  compared frames. Measured on Development and Production alike, the cost of that origin change is an average luminance
  delta of 8.33e-6, a spatial luminance grid delta of 2.31e-5, an average depth delta of 1.17e-6, a spatial depth grid
  delta of 4.22e-6, and 2 written-depth pixels against the 16-pixel budget: the twelve terrain cells of the 4x4 luminance
  grid carry the whole delta, while the four sky cells - which no origin-relative geometry covers - stay bit-invariant.
  `near` versus `returned-start` reports 3.68e-5 on the luminance grid and 4 written-depth pixels, because the far
  excursion legitimately leaves the hysteresis-locked LOD distribution behind.
  `VegetationViewFrameTests` pins the rotation-only basis, the float-range rejection, and bit-exact invariance of the
  view-relative position when the camera and the world position shift by the same rebase delta; `VegetationWindTests` pins
  the frame-anchored gust phase and its large-render-origin bounds; and the render-pass contract tests keep both shaders
  building view-frame positions and forbid any world-origin reference from creeping back in.


---

## Milestone 7b - Game-Ready Showcase Content (CC0 Asset Pass)

**Goal:** Turn the validated vertical slice into a scene that reads as a real game location instead of a
fixture, without adding engine capability.

### TODO

- [x] Rebuild the canonical terrain at a real outdoor scale.
- [x] Replace the placeholder terrain look with authored CC0 layer textures.
- [x] Re-bake the biome against the new terrain and split dense species across multiple pages.
- [x] Replace the procedural sky with an authored CC0 panorama and an explicit dusk outdoor profile.
- [x] Reduce the persistent showcase scene to camera, lights, environment, and one prop cluster.
- [x] Keep every ownership, closure, residency, budget, and visual gate green at the new density.

### Acceptance Criteria

- The scene reads as a populated dusk valley at the authored camera instead of a test fixture.
- Production still loads only versioned cooked artifacts from the relocatable catalog.
- No gate is weakened by content: no source access, no swallowed failure, and no timing policy.

### Milestone 7b Completion Record

- The canonical terrain is a real 512 m outdoor valley: 1025x1025 samples at 0.5 m spacing, `HeightRange` 0..56 m,
  `TileResolution` 257 with `SharedEdgeSamples`, and `WorldPlacement` `(-256, 0, -256)`, placed by sixteen
  terrain-tile entities in the centre cell scene. The four original tile identities and the committed 513x513 cell
  window are unchanged, `Scripts\Windows\generate_showcase_valley.ps1` owns the bytes and reproduces them, and
  generated tile files are named by coordinate
  (`Assets/Terrain/Generated/ShowcaseValley/x_0_z_0.ariterraingenerated`) so a regenerated tile can never be
  confused with a hand-edited one.
- Terrain look is authored instead of procedural. The layer set is four layers (GrassSoil, Rock, Path, River),
  each with a CC0 albedo, normal, and ORM map cooked to `r8g8b8a8unorm.srgb.mips`,
  `r8g8b8a8unorm.linear.mips.normalmap`, and `r8g8b8a8unorm.linear.mips`, plus per-layer tint, roughness,
  metallic, normal strength, and world tiling. The relocated Production closure therefore requires exactly sixteen
  terrain tiles and twelve layer textures (four per variant).
- The biome was re-baked against the new terrain and the dense species now own ordered multi-page clusters:
  Tree 312 instances over 1 page, Rock 762 over 1 page, Shrub 4369 over 5 pages, and Grass 44392 over 11 pages,
  for 49835 instances, 4 clusters, 18 pages, 4 species, and one biome. A cluster requires every page it owns,
  so the vegetation runtime artifact count is 27 and the catalog-referenced deployment closure is 31 files
  (27 vegetation artifacts plus 4 vegetation shader stages). `validate_runtime.bat` and
  `validate_relocated_production.ps1` were updated to the measured canonical submission record
  (`OpaqueInstances=49835`, `RecordedShadowInstances=199340`, four cascades of `49835`), and their exact
  catalog-dependency assertions now walk each species' page list instead of assuming one page per species.
- The sky is an authored CC0 panorama. `MistfallDusk.arienvironment` (GUID `1fc01236-449b-4097-88c0-223b1ed736ff`)
  uses `QwantaniDuskSky.hdr` (2048x1024, LatLong) with an outdoor profile that adds atmosphere, height fog, and a
  scene-relative exposure policy. `RotationDegrees: 88.232` is verified rather than eyeballed: the shader maps a
  direction to `longitude = atan2(dir.x, dir.z) + rotation` and `u = frac(0.5 + longitude / 2*pi)`, the scene key
  light `(-0.7524, 0.1876, 0.6314)` is the direction *toward* the light (`ndotl = saturate(dot(N, lightDirection))`),
  so its azimuth is `atan2(-0.7524, 0.6314) = -49.997 deg` at `10.813 deg` elevation; `38.232 - (-49.997) = 88.230 deg`
  places the panorama sun at `u = 0.60620`, which is exactly the measured brightest texel, and at the light's
  elevation within 0.002 degrees.
- The showcase content is honest about what is gameplay versus decoration: `Mistfall Valley Showcase` /
  `Mistfall Valley World` keep the persistent scene limited to the camera, the dusk key light, the lantern fill
  and down lights, the environment, and the three lantern prop parts (eight entities total), and the center cell
  scene now contains only the terrain plus the four vegetation clusters, with the earlier teapot, pedestal, and
  ground stand-ins removed. The world-streaming scenario's `before` checkpoint confirms the eight-entity
  persistent scene, and every cluster still resolves to one active cell and one cooked generation.
- Two engine-level contract fixes came out of the new content. The world-streaming budget scenario selects its
  two normal cells and its one oversized failure cell from the authored `EstimatedCpuBytes`; growing the center
  cell to 4 MiB made *both* normal cells fail admission with `Failed` instead of stalling, so the center cell is
  back to 1 MiB and the scenario keeps a real budget stall. And `TerrainStreamingCameraPoses` carries
  `NearPosition`/`NearRotation`, so the pose the fixture captures twice - the `near` checkpoint and its
  `returned-start` replay - is built by the same `LookRotation` aim at the terrain bounds centre as the boundary and far
  corners; `Build_AimsEveryCapturedPoseAtTheTerrainBounds` pins the near, boundary and far aims and eye heights against
  the root bounds, and `FixtureCheckpoints_CoverEveryCanonicalTile` pins that those three captured checkpoints frame
  every canonical tile (the parked `post-rebase` replay reuses the far pose).
- Measured cost of the content pass on this machine: two 2K terrain maps plus 1K vegetation textures at RGBA8
  cook to roughly 0.7 GB of runtime texture memory, which the runtime budget still absorbs. Dropping 4K terrain
  maps into the same slots reaches roughly 1.1 GB and breaks that budget, so the next content-quality step is
  BC compression (BC1/BC5/BC7) with mip-aware cooking rather than more pixels.
- The vegetation closure is cooked from the recipe, so re-authoring the terrain also re-authors the scene.
  The scatter bake derives each species' page count, page identity, and exact per-page instance count from the
  terrain weights, and `VegetationSceneComponent.ValidateClusterClosure` rejects a scene whose
  `VegetationCluster` components still describe the previous bake (`... is invalid: page count, instance
  count, or bounds do not match the exact cooked cluster closure.`). Retuning the weights moved every dense
  species: Rock 1152 over 2 pages became 764 over 1 page, Shrub 2801 over 3 pages became 4375 over 5, and
  Grass 25293 over 7 became 44397 over 11. The centre cell scene, the canonical fixture, the scene and
  runtime-contract tests, and both relocation gates carried that re-baked closure; the sixteen-tile terrain
  growth later moved the dense counts to Rock 762, Shrub 4369, and Grass 44392 with 49835 submitted instances.
- `.hdr` lat-long sources are already linear radiance and the environment pipeline has to be told so.
  `SourceColorSpace` defaults to `SRgb`, and `EnvironmentTextureAssetCooker.DecodeLinearSource` applies the
  sRGB transfer function to every channel of an HDR source it decodes, so a Radiance source left on the
  default raises every value above 1.0 (`v -> ((v + 0.055) / 1.055)^2.4`, 10.0 -> roughly 224) and the sky
  clips to white: the last capture taken before `MistfallDusk.arienvironment` declared
  `SourceColorSpace: Linear` put 10% of its pixels in the top luminance bin, and every capture since has an
  empty top bin and a 0.895 maximum. Rejecting an HDR source declared sRGB at cook time is follow-up work
  rather than a silent correction here.
- The dusk look was tuned against captured frames instead of by eye. At the authored camera the environment
  carries `SkyIntensity: 0.30`, `AmbientIntensity: 0.34`, `AerialStrength: 0.42`, and
  `HeightFogDensity: 0.0075`, which keeps the far capture at an average luminance of 0.358 with no clipped
  pixel and a still-increasing near/mid/far haze progression of 0.236/0.317/0.358.
- The outdoor-atmosphere gate no longer assumes a sky model. `validate_outdoor_atmosphere_visuals.ps1` takes
  `-ExpectedSkyMode` from its caller instead of defaulting to `ProceduralOutdoor`, the `validate_runtime.bat`
  and relocated-Production call sites declare `Panorama`, and horizon continuity is checked per sky mode: an
  analytic procedural sky must stay uniform within a 0.08 luminance range across the far frame's horizon
  band, while an authored panorama - which legitimately carries azimuthal content - is bounded to a 0.35 step
  between horizontally adjacent band cells (measured 0.15 to 0.24 across the near/mid/far poses). The earlier
  single threshold was calibrated for the analytic sky and failed on every panorama frame.

- The terrain-streaming fixture no longer buries its own cameras in the terrain. Every derived pose used to inherit
  the authored camera height (`5.93 m`), which is above the surface only at the authored position
  (`-102, 5.93, -128`): at the fixture's own corner poses the canonical terrain stands at `50.25 m` (`near`,
  `-245.76, -245.76`), `26.21 m` (`boundary-mixed-lod`, `245.76, -245.76`) and `30.45 m` (`far-cascade`,
  `245.76, 245.76`), so those captures looked out from `44.32 m`, `20.28 m` and `24.52 m` *inside* the hillside.
  Back-face culling then left the lower frame without written depth and the summary validator rejected the capture with
  `failed upright color orientation`. `TerrainStreamingCameraPath.Build` now takes a `TerrainSurfaceHeightSampler`, the
  scenario passes `ITerrainQueryService`, and every derived pose stands `EyeClearanceMetres` (`6 m`) above the surface
  reported at its own horizontal position. The discovery build runs before the canonical tiles are active, so its poses
  fall back to the terrain maximum plus the clearance - above every point of the surface - and the fixture rebuilds the
  path as soon as the complete render snapshot exists.
- The fixture's aim band is a shallow downward band (`-12..-4 deg`) instead of `-20..+2 deg`, every captured pose stands
  `CornerInsetFraction` (`2 %`) inside one of the root's four corners ordered by distance to the authored camera position,
  and the view aims at the bounds centre, so every captured frame keeps sky over the ridge line and the surface under the
  view. Measured `validate_terrain_streaming_summary.ps1` orientation deltas (top-half minus bottom-half average) at the
  current poses are `+0.357` luminance and `+0.0107` depth at `near`, `+0.534` and `+0.0074` at `boundary-mixed-lod`, and
  `+0.362` and `+0.0083` at `far-cascade`, all above the `+0.05` and `+0.001` thresholds, with `near`/`boundary`/`far`
  still producing distinct color and depth hashes and `post-rebase` replaying the parked `far-cascade` frame instead of the
  boundary frame.
- The soak check compares against a running high-water baseline instead of the first checkpoint's state. Camera
  checkpoints frame different parts of the valley, so `loadedCookedHandles` legitimately rises from `99` at
  `boundary-mixed-lod` to `104` at `far-cascade` as more cooked pages become resident, and the previous exact-equality
  baseline against `near` failed every soak cycle. `TerrainStreamingSmokeScenario` now records the high-water mark of
  the observed bounds, and `validate_terrain_streaming_summary.ps1` derives its steady-state baseline from the
  high-water mark of all five named checkpoints and applies it to the `soak-*` checkpoints only.
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
7. [x] extend the fixture to grass, shrub, rock, and tree species across multiple cells before broad Editor tooling;
8. [x] replace the fixture content with authored CC0 terrain layers, a dusk panorama, and a re-scaled multi-page biome.

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
- character animation, gameplay framework, AI, quests, inventory, save games, and multiplayer;
- BC1/BC5/BC7 texture compression with mip-aware cooking, until a content pass needs more than the current RGBA8 2K terrain and 1K vegetation texture budget;
- a cooker-side rejection of an HDR lat-long source declared `SourceColorSpace: SRgb`, which today produces a silently blown-out sky instead of a diagnostic.

---

## Roadmap Completion Rule

This roadmap is complete only when all milestones and acceptance criteria are implemented, architecture docs describe actual behavior, and the promoted fast/runtime gates pass from regenerated workspaces.

At completion, delete this file and create the next single active roadmap from measured results. The likely candidates are character/gameplay foundations or physics/navigation; choose from the populated outdoor vertical slice's actual bottleneck.
