# Architecture Spec: Runtime World Streaming

**Status:** Active
**Packages:** `com.arisen.resources`, `com.arisen.taskgraph`, `com.arisen.ecs`

Runtime world streaming turns a cooked `World` descriptor into one persistent scene plus additive cell-owned scene instances in the stable `SceneSubsystem` `EntityManager`. It owns cell selection, asynchronous payload staging, frame-boundary activation/unload, cancellation, and streaming diagnostics. Rendering consumes the resulting ECS extraction and does not choose residency.

## Startup Ownership

`ProjectSceneBootstrapSubsystem` selects startup content during `PostInit`:

- when `StartupWorld` is valid, `IRuntimeWorldStreamingService.LoadWorld` loads the descriptor and activates its persistent scene;
- otherwise, `StartupScene` is activated through the compatibility `IRuntimeSceneService` path;
- loading a world does not automatically select streamable cells. A composition/gameplay caller must set a finite streaming source, while Editor authoring explicitly pins cells for edit residency. Editing the persistent scene camera does not silently change cell ownership.

Editor/diagnostic source mode validates `.arisenworld` and `.arisenscene` source. Read-only Production resolves `.ariworld` and `.ariscene` artifacts from `runtime-assets.json`. Production never falls back to authoring YAML.

## Thread And Mutation Contract

Cell work is split deliberately:

1. At `EngineKernel.OnFrameEnd`, the service computes the desired set, consumes completed work, runs bounded prepared-resource setup, admits requests, unloads unwanted instances, and activates ready staging.
2. `IBackgroundTaskScheduler` workers perform file reads, decoding, hashing/container validation, immutable scene-staging validation, and generation-checked CPU dependency acquisition.
3. Workers never mutate `EntityManager`, register or destroy RHI resources, or record command buffers.
4. Worker state notifications are queued internally. Public `CellStateChanged` callbacks are delivered when the frame-boundary owner drains them, never directly from a worker.
5. Activation calls `RuntimeSceneService.ActivatePreparedAdditiveAtFrameBoundary`; unload calls `UnloadSceneAtFrameBoundary`. Both structural operations therefore occur on the owning frame thread.

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

Shutdown cancels all tracked requests, drains their completion, clears staging accounting, and runs before task-graph worker teardown. `ResourcesPackage` then clears all remaining scene ownership.

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

## Asset And GPU Residency

`IRuntimeAssetResidencyService` is the coarse resources-owned coordination contract. A persistent scene or cell request receives a generation-qualified `RuntimeAssetResidencyOwnerId`; its immutable scene staging is reduced to a canonical, deduplicated `RuntimeAssetResidencyKey` plan and acquired on the worker before the result is published. Keys contain package, GUID, asset type, and cooked variant. The current cooked-scene dependency record does not serialize a variant, so `RuntimeAssetVariantPolicy` deliberately resolves the supported default variants: `staticmesh.uint32`, `material.runtime`, and `latlong.r16g16b16a16sfloat.nomips`. Non-default scene variants require a future cooked-scene schema revision rather than an implicit guess.

The coordinator holds one generation-checked cooked handle per key and reference-counts owners above the asset database. Required acquisition failure fails the cell before ECS activation. Editor/diagnostic source mode may create an explicit source-backed lease when no mutable cooked artifact exists; the frame-boundary provider may then use the package cooker. Read-only runtime mode has no source-backed fallback. Optional unsupported or unavailable dependencies are diagnosed but do not expose a partial required set.

`IRuntimePreparedAssetProvider` keeps backend setup behind a package-neutral boundary. GenericRP registers one provider for meshes, materials, and environment texture/IBL resources. Setup runs at the frame boundary, outside RenderGraph command recording, and is bounded by count and soft wall time. A cell remains `WaitingForResources` until every required key is ready. GenericRP caches by stable residency key, shares material resources by GUID, and shares texture/image/sampler allocations across materials with the same texture variant and sampler settings. Render passes consume the resulting prepared resources without registry or asset-database lookup.

On unload, ECS entities are destroyed first. Only a successful unload releases the cell lease; rejected hierarchy unload retains the active instance and all resources. Final inactive resources are evicted in deterministic least-recently-needed/key order. Persistent and explicitly pinned owners keep their dependencies non-evictable. Mesh, material, texture, environment, bindless-descriptor, and native-handle destruction is routed through `DeferredRenderResourceDisposalQueue` at the latest submitted ticket. Shared texture leases are released only when the deferred final material disposal executes, so bindless indices cannot be recycled while an in-flight frame references them.

`RuntimeAssetResidencyMetrics` reports owner/resource states, CPU cooked bytes, prepared GPU estimates, peak values, inactive/pinned counts, setup/failure/eviction/budget-pressure counts, pending deferred disposal, and last setup time. Tracy uses `AssetResidency.*`; `WorldStreaming.WaitForResources` exposes cell backpressure separately.

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

Focus is a SceneView navigation operation, not a residency or authoring operation. A version-2 world cell may author world-space `FocusBounds`, which must be valid and contained within the cell bounds. Framing prefers that explicit subject region, then aggregates visible mesh bounds from the selected cell's working inspection, preferring authored per-instance bounds and otherwise using the authoritative bounds in the default cooked mesh header. The controller caches those value-only mesh bounds by GUID, invalidates them on asset changes, skips mesh metadata reads entirely when explicit focus bounds exist, and never recooks or creates GPU resources on the UI thread. Descriptor cell bounds are the final fallback when no visible mesh can be resolved. `EditorSceneViewFocusController` publishes the resulting world-space camera through rendering without modifying the persistent gameplay camera, dirtying source, pinning the cell, or changing runtime desired state. Focus also does not solo the cell: other runtime- or edit-resident cells remain rendered. An unloaded cell can therefore be framed while remaining visually empty until normal runtime ownership or an explicit edit pin activates its content.

The canonical `LanternWorld` fixture keeps its one gameplay camera, global directional/local showcase lights, environment, and non-streamed landmark under the persistent scene. Its cell scenes contain local mesh content authored relative to each cell origin and no competing camera, global light, or environment components. Reusing a complete standalone showcase scene as a cell payload is invalid fixture composition: it duplicates global ownership and makes edits appear ineffective because the current render pipeline has no camera-role selector.

## Current Boundary

Decoded scene staging remains separate from CPU cooked residency and is released immediately after activation or rejection. Prepared setup is intentionally a soft per-item budget because one RHI upload cannot currently be preempted; no additional item starts after the count/time limit. GPU estimates are package-provider accounting rather than backend heap telemetry. DirectStorage-style I/O, compressed pack files, exact non-default variants in cooked scene metadata, and backend memory-budget queries remain later work.

## Validation

Focused coverage in `RuntimeWorldStreamingTests.cs` and `RuntimeAssetResidencyTests.cs` proves delayed I/O does not block or mutate ECS, callback thread ownership, deterministic dependency/priority order, edit-pin dependency provenance, overlapping runtime/editor demand, camera active-cell limiting, hysteresis, read/setup/activation/unload budgets, resource-gated activation, shared persistent/cell ownership, required failure, deterministic LRU eviction, cancellation and stale-completion cleanup, explicit retry, unload rejection without duplicate ownership, and shutdown drain. `WorldOriginServiceTests.cs` proves negative floor selection, deterministic hysteresis/grid rebasing, one frame-boundary shift, parent/child and camera/light-relative stability, far-cell sub-meter precision, immutable staging, and world reconstruction. `EditorWorldDocumentServiceTests.cs` proves first-open state, stable UI identity, independent edit pins, focus without residency or dirty-state mutation, source preview without disk writes, conflict/discard behavior, transactional save-all, undo/redo moves, and cross-cell hierarchy rejection. `EditorSceneViewFocusFramingTests.cs` and `SceneViewCameraOverrideTests.cs` prove content framing, cell-bounds fallback, SceneView/GameView isolation, and stable world reconstruction across render-origin rebases. `RuntimeAssetSelectionTests` also races concurrent cooked acquisition and proves all callers share one generation-checked slot and balanced reference count.

The kernel owns only the package-neutral `IRuntimeSmokeScenarioProvider`/`IRuntimeSmokeScenario` lifecycle. A selected package may provide a bounded scenario for a named mode; the kernel supplies frame callbacks, a wall-clock deadline, optional named visual capture service, guaranteed engine shutdown, and one post-shutdown inspection callback. `com.arisen.resources` provides `world-streaming` and writes schema-versioned JSON atomically through `--smoke-summary-output`.

The canonical scenario follows observable transitions rather than sleeps. It captures `before`, `during`, `unloaded`, and `after` ECS checkpoints; observes `Queued`, `Active`, `Cancelled`, `Unloaded`, and `Failed`; completes four load/unload soak cycles; and verifies active-cell sets, entity/component parity, origin stability, and hard streaming/residency limits every frame. Peak accounting includes allocated ECS slots, cooked handles, in-flight and decoded staging bytes, resident/prepared resources, estimated prepared GPU bytes, prepared descriptors, and pending deferred disposals. After normal package shutdown it requires zero active world cells, scene instances, worker tasks, cooked handles, and residency entries.

When visual summaries are enabled, `before`, `during`, and `after` each produce independent schema-2 color/depth artifacts. The initial capture is scheduled one frame after scenario start, and transition captures use the following frame, so each artifact observes a completed render/resource-preparation boundary. `validate_world_streaming_summary.ps1` requires all checkpoints and captures to pass, at least four completed soak cycles, stable failure diagnostics, strictly increasing capture frames, nonblank color, and written finite normalized depth. The real Editor viewport smoke hosts the production SceneView, exercises World Partition panel selection and edit residency, activates GameView, and rejects removal of the declarative world-partition overlay during SceneView deactivation.

`validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1` promotes this scenario for both Development and Production in addition to their normal scene smoke. Production is then copied outside the workspace and rerun cooked-only; the copied run must stream the closed world catalog, preserve all three visual artifacts, avoid workspace/source/cache access, reject one tampered artifact by SHA-256, and reject one missing artifact with a stable diagnostic. Every run that actually initializes Vulkan must produce an empty `vk_validation.log`; the lightweight Editor kernel smoke deliberately skips hardware warmup, while the real Avalonia viewport smoke owns and proves the Editor Vulkan log. The aggregate runtime summary is schema 6 and records per-profile world-streaming and relocated-Production artifacts.
