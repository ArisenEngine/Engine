# Architecture Spec: Runtime World Streaming

**Status:** Active
**Packages:** `com.arisen.resources`, `com.arisen.taskgraph`, `com.arisen.ecs`

Runtime world streaming turns a cooked `World` descriptor into one persistent scene plus additive cell-owned scene instances in the stable `SceneSubsystem` `EntityManager`. It owns cell selection, asynchronous payload staging, frame-boundary activation/unload, cancellation, and streaming diagnostics. Rendering consumes the resulting ECS extraction and does not choose residency.

## Startup Ownership

`ProjectSceneBootstrapSubsystem` selects startup content during `PostInit`:

- when `StartupWorld` is valid, `IRuntimeWorldStreamingService.LoadWorld` loads the descriptor and
  admits its persistent scene for activation. If required residency is not ready, the call returns
  a deferred result; the persistent scene and world become active together at a later frame
  boundary;
- otherwise, `StartupScene` is activated through the compatibility `IRuntimeSceneService` path;
- loading a world does not automatically select streamable cells. A composition/gameplay caller must set a finite streaming source, while Editor authoring explicitly pins cells for edit residency. Editing the persistent scene camera does not silently change cell ownership.

`IRuntimeWorldStreamingService.PresentationSnapshot` returns one state-gate-coherent revision with
the active world asset, pending world asset, and active world GUID. `WorldPresentationChanged`
carries that exact committed snapshot. The revision advances when pending ownership is admitted,
replaced, or cleared and when active ownership is committed or cleared by activation, failure, or
shutdown; direct supersession publishes pending A to pending B without an observable empty gap.

An Editor viewport with a configured startup world subscribes before reading its initial snapshot.
Any active world prevents barrier arming. With no active world, any valid pending winner arms the
barrier, including a superseding world observed after restart. Callback and current-snapshot
revisions must match, so stale identity callbacks and B-to-C-to-B ABA transitions cannot capture an
obsolete output boundary. Once a barrier is armed, a pending successor takes precedence over an
outgoing active world present in the same revision. A terminal snapshot with neither an active nor
pending world releases the barrier without activation. Committed presentation changes publish
before fallible residency cleanup; a failed supersession then publishes its terminal empty revision
and leaves cleanup-only frame processing available for deterministic retry.

Editor/diagnostic source mode validates `.arisenworld` and `.arisenscene` source. Read-only Production resolves `.ariworld` and `.ariscene` artifacts from `runtime-assets.json`. Production never falls back to authoring YAML.

## Thread And Mutation Contract

Cell work is split deliberately:

1. At `EngineKernel.OnFrameEnd`, the service computes the desired set, consumes completed work, runs bounded prepared-resource setup, admits requests, unloads unwanted instances, and activates ready staging.
2. `IBackgroundTaskScheduler` workers perform file reads, decoding, hashing/container validation, immutable scene-staging validation, and generation-checked CPU dependency acquisition.
3. Workers never mutate `EntityManager`, register or destroy RHI resources, or record command buffers.
4. Worker state notifications are queued internally. Public `CellStateChanged` callbacks are delivered when the frame-boundary owner drains them, never directly from a worker.
5. Activation calls `RuntimeSceneService.ActivatePreparedAdditiveAtFrameBoundary`; unload calls `UnloadSceneAtFrameBoundary`. Both structural operations therefore occur on the owning frame thread.

World lifecycle operations are serialized by an explicit non-reentrant gate. `LoadWorld`,
`ProcessAtFrameBoundary`, and `Shutdown` wait for an operation already in progress; a same-thread
reentry from an activation validator or observer fails immediately with a diagnostic instead of
deadlocking. A ready cell also takes a monotonic activation claim before placement and scene
activation. Reload, preview replacement, undesired-cell planning, and shutdown cannot release that
cell's staging or residency lease until the claim has completed. If a successful activation is
superseded, the new scene instance is unloaded first and only then are staging and residency
ownership released and the cell requeued.

`CellStateChanged`, `ActiveWorldChanged`, and `WorldPresentationChanged` are synchronous
observational boundaries. The publisher snapshots each multicast delegate, invokes every subscriber
independently and outside the streaming state lock, and never lets one observer prevent later
observers from seeing an already-committed transition. Failures emitted by one public operation are
collected until its owning boundary (`LoadWorld`, preview/reload/retry, frame processing, or
shutdown) completes. The service then adds one `SubscriberAggregate` entry to `GetDiagnostics()`,
increments `SubscriberFailureCount`, and logs one aggregate error. Each child diagnostic retains the
event name, payload identity, deterministic registration index plus declaring type/method,
exception type, and message; presentation payloads record the revision, active asset, pending asset,
and active GUID. Observer failure does not roll back committed world/ECS state, but it is never
silently swallowed.

The payload loader has Tracy zones for `WorldStreaming.Read`, `WorldStreaming.Decode`, and `WorldStreaming.Validate`. Each admitted worker also emits `WorldStreaming.CellRead/<cell-id>`, while frame-boundary ownership emits `WorldStreaming.Activate/<cell-id>` and `WorldStreaming.Unload/<cell-id>` around concrete cell work. The stable cell suffix makes latency attributable without adding per-entity zones. Coarse aggregate zones remain available for `WorldStreaming.PlanRequests`, `WorldStreaming.WaitForResources`, `WorldStreaming.Activate`, and `WorldStreaming.Unload`.

## State And Retry Contract

The observable states are:

```text
Unloaded -> Queued -> Reading -> Decoding -> Validating -> WaitingForResources -> ReadyToActivate -> Active
Active -> QueuedToUnload -> Unloading -> Unloaded
Queued/Reading/Decoding/Validating/WaitingForResources/ReadyToActivate -> Cancelled
Queued/Reading/Decoding/Validating/WaitingForResources/ReadyToActivate -> Failed
Failed/Cancelled -> Queued or Unloaded through RetryCell
```

Every admitted request increments a monotonic per-cell generation. Cancellation also advances the generation before signalling the worker. A completion whose task sequence or generation is stale is discarded without exposing staging or mutating ECS. `BackgroundTask<T>` disposes an unclaimed disposable result when cancellation wins after the operation returns, so a late worker result cannot leak residency ownership.

Read, decode, validation, budget, and activation failures remain `Failed` until `RetryCell` is called. If unload is rejected because a live hierarchy reference crosses the instance boundary, the cell remains `Active` with its original scene instance ID and a retained diagnostic. It is excluded from automatic unload retries so it cannot spin or load a duplicate instance. After repairing the reference, `RetryCell` explicitly re-enables unload.

Each prepared additive activation carries its owning `WorldCellId` into `RuntimeSceneService`. Entity-to-instance ownership can therefore resolve back to the cell without transient hierarchy or UI state. Package-owned scene components may also expose an exclusive stable identity; the service validates those identities before ECS mutation, rejects a second active owner, releases them only with successful instance unload, and permits the same stable identity after unload/reload. Terrain uses this boundary for one tile GUID per owning cell scene.

Shutdown cancels all tracked requests, drains their completion, and clears staging accounting. It
prevalidates one dependency-safe batch containing every active additive instance and the persistent
instance, then unloads that batch atomically before releasing any cell or persistent residency
lease. A rejected batch leaves the old scenes, ECS entities, and leases intact and returns the
service to its prior running state; no partial world replacement is reported. Shutdown runs before
task-graph worker teardown, after which `ResourcesPackage` clears any remaining scene ownership.

## Selection And Ordering

The residency desired set combines:

- a camera/source cell and all cells within `LoadRadius`;
- already active cells within `LoadRadius + UnloadHysteresis`;
- explicit pins;
- the transitive dependency closure of every selected cell.

Camera candidates are ordered by source distance, layer priority, then stable `WorldCellId`. Their dependency closure must fit `MaxActiveCells`; cells that do not fit remain deferred with an observable diagnostic. Explicit pins and their dependency closure override this automatic camera-cell cap because editor/tooling residency is intentional. I/O and staging budgets still apply to pinned cells.

`WorldCellStreamingSnapshot.Desired` is the union used for residency. `DesiredSources` preserves why a cell is in that union: `Runtime` for the accepted streaming-source closure, `EditPin` for a directly pinned cell, and `EditDependency` for an unpinned prerequisite reached only through an edit pin. Flags may overlap when runtime and editor demand the same cell. A change in provenance publishes a snapshot even when residency and streaming state do not change, so editor diagnostics cannot mislabel an edit dependency as runtime-owned.

Dependencies must reach `Active` before a dependent cell can activate. Unload runs in reverse dependency-safe order and will not remove a cell still required by another active cell.

## Large-World Coordinate Contract

Arisen uses four explicit coordinate spaces:

- **world space** is `WorldPosition` (`double X/Y/Z`) and owns partition origin, cell size/bounds, streaming sources, serialization, stable cell selection, and cross-cell identity;
- **cell space** is authored scene transform data relative to `WorldPartitionCoordinates.GetCellOrigin(partition, coordinate)`;
- **active simulation space** stores compact `TransformComponent.Position` floats relative to `IWorldOriginService.CurrentOrigin`;
- **render space** is the same origin-relative float space consumed by camera matrices, mesh matrices/bounds, culling, point/spot lights, directional-shadow fitting, and GPU uploads.

`WorldOriginService` is resources-owned and registered as `IWorldOriginService`. A finite source passed to `IRuntimeWorldStreamingService.SetStreamingSource` also becomes the origin policy's primary source. The policy retains the current origin while the source remains inside per-axis hysteresis, then snaps only escaped axes to deterministic partition-cell grid coordinates. At `EngineKernel.OnFrameEnd`, before cell planning/activation, it preflights the complete contiguous `TransformComponent` pool, publishes `RebaseStarting`, applies one `previousOrigin - currentOrigin` translation, commits one monotonic sequence, and publishes `Rebased`. These coarse events are the extension points for future physics, navigation, particles, audio, and gameplay participants; handlers must complete synchronously and must not schedule a second structural mutation.

Cell payloads remain origin-independent. Immediately before frame-boundary activation, immutable staging is copied and translated by `cellOrigin - currentOrigin`; source/cooked staging is never mutated. A cell prepared under an older origin therefore activates against the latest origin, while cell identity, dependency order, entity GUIDs, and cross-cell references remain unchanged. Persistent scene content is world-origin content and is shifted with the rest of the active ECS when a rebase occurs.

`IWorldOriginService.ToWorld` and `TryToOriginRelative` are the only general conversion boundary. `RenderFrameSnapshot.RenderOrigin` carries the double origin once per frame for diagnostics and future world reconstruction; per-camera, per-light, per-mesh, culling, and shadow records remain compact floats. Extraction rejects non-finite camera, mesh transform, and light input before matrix construction or upload. No projection, winding, or Y-axis correction is part of origin handling.

Offline vegetation scatter planning uses the same partition coordinate contract. Terrain samples remain double-world values until each accepted placement is narrowed to a `float3` relative to its computed owner-cell origin. The narrowed value is reconstructed in double world space and ownership is recomputed until stable, so a stored point cannot round across a cell boundary after the ownership decision. Positive borders follow the half-open positive-cell rule. A one-spacing X/Z halo contributes deterministic lower-key blockers from adjacent cells, but only placements owned by the requested XYZ cell are emitted. The page origin is that requested cell's canonical world origin; scatter identity never depends on the current runtime render origin.

## Asset And GPU Residency

`IRuntimeAssetResidencyService` is the coarse resources-owned coordination contract. A persistent scene or cell request receives a generation-qualified `RuntimeAssetResidencyOwnerId`; its immutable scene staging is reduced to a canonical, deduplicated `RuntimeAssetResidencyKey` plan and acquired on the worker before the result is published. Keys contain package, GUID, asset type, and cooked variant. The current cooked-scene dependency record does not serialize a variant, so `RuntimeAssetVariantPolicy` deliberately resolves the supported default variants: `staticmesh.uint32`, `material.runtime`, and `latlong.r16g16b16a16sfloat.nomips`. Non-default scene variants require a future cooked-scene schema revision rather than an implicit guess.

The coordinator holds one generation-checked cooked handle per key and reference-counts owners above the asset database. Required acquisition failure fails the cell before ECS activation. Editor/diagnostic source mode may create an explicit source-backed lease when no mutable cooked artifact exists; the frame-boundary provider may then use the package cooker. Read-only runtime mode has no source-backed fallback. Optional unsupported or unavailable dependencies are diagnosed but do not expose a partial required set.

`IRuntimePreparedAssetProvider` keeps backend setup behind a package-neutral boundary. GenericRP registers one provider for meshes, materials, and environment texture/IBL resources. Setup runs at the frame boundary, outside RenderGraph command recording, and is bounded by count and soft wall time. A cell remains `WaitingForResources` until every required key is ready. GenericRP caches by stable residency key, shares material resources by GUID, and shares texture/image/sampler allocations across materials with the same texture variant and sampler settings. Render passes consume the resulting prepared resources without registry or asset-database lookup. Residency snapshots expose immutable owner IDs so package diagnostics can attribute resources without creating a second ownership table. Device-resource teardown calls `InvalidatePreparedProvider`: every still-owned key returns to `Waiting`, provider resources are released, and normal bounded setup recreates them after a valid device becomes available.

Residency may invoke provider `Release` and `GetMetrics` from the caller that drops an owner, not
only from the frame setup thread. Generic RP therefore tombstones the exact released key under a
narrow lifecycle gate and publishes one immutable metrics snapshot immediately; new acquisitions
and retained-lease currency checks fail closed against that tombstone. Physical material/RHI
retirement is queued and drained only by setup-thread entry points. Source acquisition, lease
disposal, device mutation, and deferred-queue access remain setup-thread affine, and unchanged
metrics publication allocates nothing in the warmed frame path.

Acquisition, `Prepare`, prepared publication, and provider lifecycle callbacks run outside the
residency state gate under explicit callback admission. Every world-streaming lifecycle entry asks
the residency coordinator to reject callback-side reentry before it waits for the world lifecycle
gate. This ordering prevents a callback that the current world operation is draining from waiting
back on that operation's gate. Publication rejection and release failure retain generation-qualified
ownership for deterministic frame-boundary cleanup retry instead of abandoning a cooked handle or
prepared resource.

The terrain Generic RP adapter registers a second prepared provider for `TerrainRoot` and `TerrainTile` keys. Root preparation validates the cooked contract and acquires reference-counted albedo, normal, and ORM leases through GenericRP's shared texture cache, so terrain and ordinary materials converge on the same image/view/sampler allocation. Tile preparation waits for a valid RHI frame context, validates the cooked tile, uploads expanded heights, packed four-channel weights, and packed geometric-error records into three independently owned bindless storage buffers, and shares immutable grid vertex/index buffers by resolution. Terrain scene dependencies therefore keep a cell in `WaitingForResources` until the root layers and tile resources are ready. Tile buffers, exact root texture generations, and the final shared-grid reference are released through the latest submitted ticket, while render extraction consumes only active terrain components and prepared views. Asset changes and immutable authoring previews build all affected GPU replacements first and publish matching CPU root/tile generations as one frame-boundary transaction; unrelated tile generations remain resident, retained unsaved previews are reapplied after a cell reload, and any failure preserves the previous valid publication.

Successful root/tile preparation also publishes immutable CPU data through `ITerrainRuntimeDataStore`; release removes only the matching generation. `ITerrainQueryService` never acquires or loads an asset. It resolves a double-world X/Z position against the canonical root grid, requires the resolved tile's ECS component to be active, and returns an explicit invalid, outside, unavailable, or available result with bilinear height, gradient normal, normalized layer weights, tile identity, and generation. Positive interior borders resolve to the positive tile, while the outer root border remains inclusive. This makes cached-but-inactive cell data unavailable to gameplay, editor, physics, and navigation callers.

On unload, ECS entities are destroyed first. Only a successful unload releases the cell lease; rejected hierarchy unload retains the active instance and all resources. Final inactive resources are evicted in deterministic least-recently-needed/key order. Persistent and explicitly pinned owners keep their dependencies non-evictable. Mesh, material, texture, environment, bindless-descriptor, and native-handle destruction is routed through `DeferredRenderResourceDisposalQueue` at the latest submitted ticket. Shared texture leases are released only when the deferred final material disposal executes, so bindless indices cannot be recycled while an in-flight frame references them.

`RuntimeAssetResidencyMetrics` reports owner/resource states, CPU cooked bytes, prepared GPU estimates, peak values, inactive/pinned counts, setup/failure/eviction/budget-pressure counts, pending deferred disposal, and last setup time. Tracy uses `AssetResidency.*`; `WorldStreaming.WaitForResources` exposes cell backpressure separately. `ITerrainResidencyDiagnostics` projects those owners onto terrain root/tile/coordinate identity and adds decoded height/weight/error bytes, prepared buffer bytes, layer descriptor count, pending disposal, setup timing, and terrain budget pressure through `Terrain.Residency.*` plots.

## Budgets And Backpressure

`WorldStreamingBudgets` bounds:

- concurrent reads;
- bytes reserved in flight;
- decoded plus reserved staging bytes;
- activations per frame;
- activation wall time per frame;
- unloads per frame.

Shared pressure leaves requests queued and increments `BudgetStallCount`. A cell whose individual reservation exceeds a configured byte limit fails immediately with required/configured byte counts instead of remaining queued forever. Activation may complete the operation that crosses the wall-time deadline; no further activation is admitted in that frame.

Budgets may be changed through `TryConfigureBudgets` only before the first cell request or after every cell has unloaded. This keeps already-reserved worker and staging accounting under one immutable limit set. The bounded validation scenario uses this pre-request seam to force one queued cancellation and one deterministic over-budget admission failure without timing sleeps.

`WorldStreamingMetrics` reports current states, current/peak bytes, cancellations, failures, stale completions, budget stalls, and last load/activation/unload timings. Tracy plots use the `WorldStreaming.*` prefix for those values. Diagnostics and terminal observations are bounded and use stable world/cell identities.

## Editor Authoring Residency

`IEditorWorldDocumentService` mirrors the active world descriptor into one world document, one persistent-scene document, and independently tracked cell documents. Saved source, immutable working source, dirty state, external-file conflicts, selected cell, stable entity selection, hierarchy expansion, focus requests, and edit pins are separate state. Runtime refreshes rebuild presentation from world/cell/entity identities and reuse matching view models, so tree expansion and selection do not depend on transient ECS handles.

SceneView edit residency is explicit. `LoadCellForEditing` adds an editor pin; `UnloadCellForEditing` removes only that pin and does not override a GameView/camera desired cell. Working cell source is validated into an immutable preview snapshot and then follows the normal worker staging, dependency acquisition, resource preparation, frame-boundary unload, and additive activation path. Unsaved YAML is never written by preview and survives view switches.

The World Partition panel exposes one checked edit-residency toggle, focus, save-all, reimport, retry, state, ownership, dirty state, dependency/resource diagnostics, and streaming metrics. Its rows retain identity and selection across state refreshes, and selecting an already-selected cell is idempotent. `Runtime desired` means the accepted runtime streaming-source closure needs the cell; `Edit dependency` means an editor pin pulled in that prerequisite transitively; direct pins remain `Edit pin`, and overlapping demand reports both sources. None of these dependency/runtime labels grants editor authoring residency. SceneView draws world-cell bounds with loaded, desired, pinned, dirty, selected, and failed state: active runtime cells are green, editor-pinned cells are yellow, edit-pin dependencies are amber, runtime-desired cells that have not activated are blue, and unloaded cells are gray. The map can select a cell and shows the persistent camera as a live white cross without treating that camera as an implicit streaming source. Its per-project visibility toggle survives SceneView/GameView switches and editor restarts; tab deactivation disposes only the GPU viewport binding, not declarative overlay controls. Hierarchy always retains persistent/cell root rows for world-state diagnostics, but it exposes cell entity children only while that cell is explicitly edit-pinned and `Active`; an `Active | runtime` or `Active | edit dependency` cell is render-resident but intentionally not authoring-resident. Unpinning removes those children and their stable selection; the document service also rejects stale inspector transform writes against a non-resident cell. Reloading the currently active persistent scene for an editor preview replaces only that persistent instance and preserves additive cell instances; opening a different persistent scene remains a controlled full scene replacement. Moving the selected hierarchy subtree to the selected cell is an undoable two-document source transaction; partial save is rejected, Save All commits both files transactionally, and cross-cell parent/reference violations fail with a repair diagnostic before either working source changes.

Focus is a SceneView navigation operation, not a residency or authoring operation. A successful World Partition focus command activates the Scene panel so its camera change is immediately visible; GameView remains bound to the persistent gameplay camera. A version-2 world cell may author world-space `FocusBounds`, which must be valid and contained within the cell bounds. Framing prefers that explicit subject region, then aggregates visible mesh bounds from the selected cell's working inspection, preferring authored per-instance bounds and otherwise using the authoritative bounds in the default cooked mesh header. The controller caches those value-only mesh bounds by GUID, invalidates them on asset changes, skips mesh metadata reads entirely when explicit focus bounds exist, and never recooks or creates GPU resources on the UI thread. Descriptor cell bounds are the final fallback when no visible mesh can be resolved. `EditorSceneViewFocusController` publishes the resulting world-space camera through rendering without modifying the persistent gameplay camera, dirtying source, pinning the cell, or changing runtime desired state. Focus also does not solo the cell: other runtime- or edit-resident cells remain rendered. An unloaded cell can therefore be framed while remaining visually empty until normal runtime ownership or an explicit edit pin activates its content.

The canonical `LanternWorld` fixture keeps its one gameplay camera, global directional/local showcase lights, environment, and non-streamed landmark under the persistent scene. Its cell scenes contain local mesh content authored relative to each cell origin and no competing camera, global light, or environment components. Reusing a complete standalone showcase scene as a cell payload is invalid fixture composition: it duplicates global ownership and makes edits appear ineffective because the current render pipeline has no camera-role selector.

## Current Boundary

Decoded scene staging remains separate from CPU cooked residency and is released immediately after a
completed activation claim or an explicit rejection. Prepared setup is intentionally a soft
per-item budget because one RHI upload cannot currently be preempted; no additional item starts
after the count/time limit. GPU estimates are package-provider accounting rather than backend heap
telemetry. DirectStorage-style I/O, compressed pack files, exact non-default variants in cooked
scene metadata, and backend memory-budget queries remain later work.

Vegetation cluster scenes now participate in this boundary. The package-owned codec validates the
cluster/biome/species identities, owning world cell, canonical double-world origin and bounds, and
exclusive cluster ownership before ECS mutation. `com.arisen.vegetation` publishes immutable,
generation-qualified CPU cluster/page snapshots and bounded query results; Generic RP's provider
loads only the residency-held cooked handles on workers and binds each decoded dependency closure
to the owner-plan generation. Cluster/page preparation validates reciprocal parent membership,
schema, exact page size/SHA-256 pins, species union, counts, bounds, biome membership, and
cross-page stable keys; publication revalidates root/dependency claims, canonical binding, and
owner-plan generation before and after the callback, and keeps cells in `WaitingForResources`
when ownership changes. Frame-boundary GPU setup retains exact-key Generic RP mesh/material leases,
packs one immutable origin-relative instance buffer per cluster, and publishes only a matching CPU,
prepared-resource, RHI-device, and owner-plan generation. Cell unload removes ECS ownership first,
then tombstones each exact prepared key; material, mesh, instance-buffer, and descriptor retirement
stays on the setup thread and is deferred through the latest submitted ticket. Extraction therefore
cannot expose an unloaded or stale cluster, and the opaque/four-cascade shadow passes never perform
asset or service lookup while recording.

## Validation

Focused coverage in `RuntimeWorldStreamingTests.cs` and `RuntimeAssetResidencyTests.cs` proves delayed I/O does not block or mutate ECS, callback thread ownership, callback-before-lifecycle-gate reentry rejection, concurrent prepared-publication/reload completion, deterministic dependency/priority order, edit-pin dependency provenance, overlapping runtime/editor demand, camera active-cell limiting, hysteresis, read/setup/activation/unload budgets, resource-gated activation, shared persistent/cell ownership, required failure, deterministic LRU eviction, cancellation and stale-completion cleanup, explicit retry, unload rejection without duplicate ownership, activation-claim supersession, serialized lifecycle calls, rejected whole-world unload retention, coherent presentation revisions across admission, supersession, activation, failure, and shutdown, subscriber isolation, and shutdown drain. `StartupWorldPresentationBarrierStateTests.cs` covers subscribe-before-read startup, restart while pending, stale and ABA callback rejection, superseding winners, exact activation revisions and output boundaries, and terminal release without activation. `TerrainResidencyCoordinationTests.cs` adds shared-root and independently evicted tile lifetime, immutable owner attribution, failed-generation retry, cancelled-acquisition rollback, provider invalidation/re-preparation, and terrain shutdown drain. `VegetationResidencyCoordinationTests.cs` covers worker publication, cancellation, retries, shared species dependencies, independently evicted pages, stale generations, and provider shutdown. `VegetationSceneComponentTests.cs` covers source/cooked codec round trips, clean-root dependency cooking, strict cooked-only closure, exact dependency graphs, identity and bounds rejection, exclusive ownership, and activation-context validation. `TerrainPreparedPayloadPackingTests.cs` locks canonical height/weight/error GPU packing and malformed-input rejection. `WorldOriginServiceTests.cs` proves negative floor selection, deterministic hysteresis/grid rebasing, one frame-boundary shift, parent/child and camera/light-relative stability, far-cell sub-meter precision, immutable staging, and world reconstruction. `EditorWorldDocumentServiceTests.cs` proves first-open state, stable UI identity, independent edit pins, focus without residency or dirty-state mutation, source preview without disk writes, conflict/discard behavior, transactional save-all, undo/redo moves, and cross-cell hierarchy rejection. `EditorSceneViewFocusFramingTests.cs` and `SceneViewCameraOverrideTests.cs` prove content framing, cell-bounds fallback, SceneView/GameView isolation, and stable world reconstruction across render-origin rebases. `RuntimeAssetSelectionTests` also races concurrent cooked acquisition and proves all callers share one generation-checked slot and balanced reference count.

The kernel owns only the package-neutral `IRuntimeSmokeScenarioProvider`/`IRuntimeSmokeScenario` lifecycle. A selected package may provide a bounded scenario for a named mode; the kernel supplies frame callbacks, a wall-clock deadline, optional named visual capture service, guaranteed engine shutdown, and one post-shutdown inspection callback. `com.arisen.resources` provides `world-streaming` and writes schema-versioned JSON atomically through `--smoke-summary-output`.

The canonical scenario follows observable transitions rather than sleeps. It captures `before`, `during`, `unloaded`, and `after` ECS checkpoints; observes `Queued`, `Active`, `Cancelled`, `Unloaded`, and `Failed`; completes four load/unload soak cycles; and verifies active-cell sets, entity/component parity, origin stability, and hard streaming/residency limits every frame. Peak accounting includes allocated ECS slots, cooked handles, in-flight and decoded staging bytes, resident/prepared resources, estimated prepared GPU bytes, prepared descriptors, and pending deferred disposals. After normal package shutdown it requires zero active world cells, scene instances, worker tasks, cooked handles, and residency entries.

When visual summaries are enabled, the scenario produces seven independent schema-2 color/depth artifacts: `before`, `during`, `shadow-near`, `shadow-mid`, `shadow-far`, `shadow-far-stable`, and `after`. Captures are scheduled for the following native frame so each observes a completed render/resource-preparation boundary. After the four streaming soak cycles, the persistent validation camera follows a deterministic shadow path: its authored transform is used for `shadow-near`, it retreats 20 world units opposite its forward direction for `shadow-mid`, and it retreats 64 units for `shadow-far`. `shadow-far-stable` captures the immediately following frame without moving the camera, then the authored transform is restored before `after`. Camera world position is converted through `IWorldOriginService`, so this path remains valid after origin rebasing and does not change streaming-source ownership.

`validate_world_streaming_summary.ps1` requires every checkpoint and all seven captures to pass, at least four completed soak cycles, stable failure diagnostics, strictly increasing capture frames, nonblank color, written finite normalized depth, and valid SHA-256 hashes of both immutable pixel payloads. Near/mid/far color and depth hashes must be pairwise distinct, while the consecutive stationary far captures must be byte-identical. `validate_cascaded_shadow_visuals.ps1` correlates those capture frame indices with Generic RP and terrain diagnostics, then requires four increasing cascade splits, positive mesh and terrain work, no dropped commands, coverage of every layer across the path, and stable far-frame split/draw distributions. The real Editor viewport smoke hosts the production SceneView, exercises World Partition panel selection and edit residency, activates GameView, and rejects removal of the declarative world-partition overlay during SceneView deactivation.

`validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` promotes this scenario for both Development and Production in addition to their normal scene smoke. Production is then copied outside the workspace and rerun cooked-only; the copied run must stream the closed world catalog, preserve all seven visual artifacts, pass the same world-streaming and cascade checks, avoid workspace/source/cache access, reject one tampered artifact by SHA-256, and reject one missing artifact with a stable diagnostic. Every run that actually initializes Vulkan must produce an empty `vk_validation.log`; the lightweight Editor kernel smoke deliberately skips hardware warmup, while the real Avalonia viewport smoke owns and proves the Editor Vulkan log.

Vegetation rendering validation reuses the canonical `world-streaming` full-mode run and adds
Development `disabled` and `opaque-only` runs. `validate_vegetation_rendering_visuals.ps1` requires
each run's named `during` checkpoint to own exactly the center cell and the same ECS/origin state,
and requires the capture surface, dimensions, formats, and spatial-grid shape to match without
requiring cross-process frame indices to be equal. Disabled-to-opaque must exceed conservative
relative color, depth-grid, and written-depth coverage margins. Opaque-only and full must have
byte-identical complete depth output, while full must be measurably darker in aggregate and in at
least one spatial region, isolating vegetation's directional-shadow contribution without expected
pixel hashes. The relocated cooked-only Production audit runs the same three modes and retains each
comparison summary plus named `during` artifact outside its temporary player root.

`com.arisen.terrain` also provides the independent `terrain-streaming` scenario. It follows deterministic near, boundary, far, post-rebase, and returned-to-start camera checkpoints, performs exactly one origin rebase, then completes four observable load/reload/unload cycles without sleeps. Thirteen ordered checkpoints verify four stable tile identities, generation advancement after every reload, active ECS/query parity, normalized height/normal/weight queries, conservative world bounds, bounded LOD/patch histograms, zero seam violations, and bounded ECS/cooked/residency/prepared/deferred-disposal state. The final post-shutdown inspection requires the terrain cell to be undesired and unloaded or cancelled, with zero visible/runtime/diagnostic/residency resources, pending disposals, and tasks.

Five schema-2 color/depth captures are retained for `near`, `boundary-mixed-lod`, `far-cascade`, `post-rebase`, and `returned-start`. `validate_terrain_streaming_summary.ps1` requires upright nonblank color and finite written depth, distinct near/boundary/far views, stable world/query state across the rebase, and tight numeric similarity when returning to an equivalent camera/origin state. Equivalent frames use bounded luminance/depth-grid and written-coverage tolerances rather than byte-identical hashes because origin-relative floating-point reconstruction can differ slightly while remaining visually equivalent.

Development and Production run the dedicated terrain scenario locally, and copied Production runs it again from an isolated cooked-only player. Every run requires a fresh positive terrain submission marker, an empty Vulkan validation log, clean feature/RHI teardown ordering, a completely drained terrain summary, and all five visual artifacts. The relocated run rejects concrete workspace manifest/cache/package/scene/terrain-source access while allowing harmless embedded debug-symbol paths, then rewrites preserved capture paths out of its temporary player root. Aggregate runtime validation schema 8 records per-profile world/terrain results, three terrain runs, the Development and relocated Production vegetation comparisons, and their summary artifact paths. Relocated Production schema 6 records terrain and vegetation closure, both streaming checks, clean shutdown, the terrain/world visuals, and all six vegetation comparison summary/`during` artifact paths.

The relocated Production audit validates terrain closure before boot. Starting from the `startupWorld` and `renderPipeline` catalog roots, it requires the canonical cooked-v2 terrain root, all four generated tiles, exactly three sRGB albedo mip chains, three renormalized linear normal mip chains, three linear ORM mip chains, and three terrain shader stages to be resolved and reachable. It rejects stale or unreferenced terrain-tile rows, wrong texture format versions/variants, deployed terrain layer-set authoring assets, and terrain source extensions including `.ariweights`. Tamper and missing-artifact checks target a terrain tile, and `terrainClosureComplete` is published only after the graph audit succeeds.
