# Architecture Spec: Profiling And Runtime Tracing

Arisen uses Tracy as the first production-grade realtime profiler backend. The engine should emit low-overhead zones, plots, and frame marks from package code, then inspect the timeline in the external Tracy Profiler viewer.

## Current Policy

- `com.arisen.core.native` contains the Tracy client dependency and the native `Core.Diagnostic` profiler bridge.
- `com.arisen.core` exposes the managed facade: `Profiler.Zone`, `Profiler.FrameMark`, `Profiler.FrameMarkNamed`, `Profiler.PlotValue`, and `Profiler.SetThreadName`.
- Profiles with `EnableProfiler: true` define `ARISEN_PROFILER_ENABLED`, build and link the Tracy
  client, and deploy `TracyClient.dll` plus its optional debug symbols.
- Profiles with `EnableProfiler: false` compile out profiler instrumentation and neither build nor
  deploy Tracy. Resolved and deployed package metadata omit the profiler-only payload, and stale
  Tracy output is rejected.
- Regular profiler-enabled Arisen builds do not build the Tracy Profiler viewer executable.

## RenderDoc Frame Capture

RenderDoc is an explicit Vulkan diagnostic mode, separate from Tracy profiling. Installing RenderDoc must not change an ordinary Editor or runtime launch. `VulkanRHIBackend` clears RenderDoc's Vulkan enable flag and disables its registered implicit layer unless capture was explicitly requested.

Arisen supports two activation paths:

- Process-start activation through `ARISEN_ENABLE_RENDERDOC=1` or a RenderDoc-launched process.
- One-way in-process Editor activation through the viewport `Enable RenderDoc` command. This keeps the Editor process and CPU/editor state alive while replacing the complete graphics-device generation.

External injection into an already-running Vulkan generation is unsupported. Loading a layer into a live instance does not retroactively place it in that instance or device's dispatch chain, and attempting to combine late injection with existing queues, swapchains, and exported objects violates their ownership.

Opt in for one PowerShell launch from the repository root:

```powershell
$env:ARISEN_ENABLE_RENDERDOC = "1"
& "Arisen\Development\PackageGame\.arisen\bin\Editor\Debug\PackageGame.exe" --workspace "Arisen\Development\PackageGame" --profile Editor
Remove-Item Env:ARISEN_ENABLE_RENDERDOC
```

For Rider process-start capture, set `ARISEN_ENABLE_RENDERDOC=1` in that run configuration and restart the process. Launching through RenderDoc's own process launcher is also treated as an explicit diagnostic launch.

For in-process activation, start the Editor normally and select `Enable RenderDoc` in the viewport toolbar. The lifecycle is:

1. `GraphicsDeviceLifecycleCoordinator` stops presentation participants in ascending order.
2. Each viewport awaits active initialization, presentation, resize, and compositor-import disposal, then unregisters its render surface.
3. The render thread requires zero surfaces, disposes the pipeline, invalidates prepared residency, drains old-generation deferred tickets, and explicitly releases each disposal queue's device-generation binding.
4. `VulkanRHIBackend` detaches the stable `IRHIDevice` service, destroys the old RHI instance/device, preloads and enables RenderDoc, creates the next backend generation, and reattaches the service.
5. Participants restore in descending order, reacquire compositor interop, register new surfaces, and resume presentation.

Scene/ECS state, hierarchy, selection, layout, authoring documents, and CPU asset state remain alive. Vulkan instances, devices, queues, swapchains, exported images/semaphores, pipelines, descriptors, and prepared GPU resources do not survive the generation boundary. Activation is one-way; disabling or unloading RenderDoc in the same Editor process is unsupported.

RenderDoc changes the Vulkan dispatch chain, so it remains explicit rather than part of ordinary startup. Editor opaque-Win32 image/semaphore interop is supported in this mode: each virtual frame slot keeps one native producer/consumer semaphore pair, and Avalonia keeps the corresponding imported objects alive for the whole swapchain generation. Do not import or dispose those semaphores per frame. Resize awaits the active compositor update, disposes generation imports, acknowledges external release, and only then replaces native synchronization and images.

Each viewport capture targets its exact `RenderSurfaceRegistration(host, generation)`. A request owns
the state sequence `Pending -> Capturing -> PublishingArtifact -> Succeeded/Failed`; another request
cannot replace it while any of those active states is owned. Before starting native capture,
`RenderDocService` snapshots `GetNumCaptures()` and assigns a unique template beneath
`logs/renderdoc` containing the process id, monotonic request id, and a random suffix. A successful
`EndFrameCapture` plus successful render-frame retirement/completion advances only to
`PublishingArtifact`.

Artifact success is owned by RenderDoc's inventory, not by filesystem timing or inferred writer
process ownership. One long-running publication worker observes `GetNumCaptures` and `GetCapture`,
accepts only a newer `.rdc` path matching the request template, and verifies that the file exists and
is non-empty. Render frames pulse an `AutoResetEvent` between observations. Cancellation wakes the
worker and teardown joins it synchronously before disposing its signal or replacing capture state.
There is no sleep, quiet period, retry bound, file-timestamp heuristic, or Restart Manager inference
in this ownership contract. Replay-UI launch occurs only after terminal publication and a launch
failure does not invalidate the already-published capture artifact.

Use the real dual-viewport regression when changing editor presentation, synchronization, resize, or RenderDoc integration:

```powershell
$env:ARISEN_ENABLE_RENDERDOC = "1"
& "Arisen\Development\PackageGame\.arisen\bin\Editor\Debug\PackageGame.exe" --workspace "Arisen\Development\PackageGame" --profile Editor --editor-viewport-smoke --editor-viewport-smoke-timeout 90
powershell -NoProfile -ExecutionPolicy Bypass -File "Arisen\Scripts\Windows\validate_editor_viewport_summary.ps1" -ArtifactPath "Arisen\Development\PackageGame\.arisen\Logs\editor-viewport-summary-Editor-latest.json" -ExpectedProfile Editor -ExpectRenderDoc
Remove-Item Env:ARISEN_ENABLE_RENDERDOC
```

The run must load RenderDoc before Vulkan initialization, complete four exact resize generations, keep both SceneView and GameView active with Terrain Paint selected for at least 320 accepted frames each, retain fixed per-viewport import high-water marks of three images and four semaphores, and leave `vk_validation.log` empty. This is a correctness gate for the diagnostic mode, not a reason to enable RenderDoc in ordinary validation.

Use the in-process generation-replacement regression for the `Enable RenderDoc` path:

```powershell
& "Arisen\Development\PackageGame\.arisen\bin\Editor\Debug\PackageGame.exe" --workspace "Arisen\Development\PackageGame" --profile Editor --editor-viewport-smoke --editor-viewport-smoke-timeout 120 --editor-viewport-smoke-restart-renderdoc --editor-viewport-smoke-capture-renderdoc
powershell -NoProfile -ExecutionPolicy Bypass -File "Arisen\Scripts\Windows\validate_editor_viewport_summary.ps1" -ArtifactPath "Arisen\Development\PackageGame\.arisen\Logs\editor-viewport-summary-Editor-latest.json" -ExpectedProfile Editor -ExpectRenderDocRestart -ExpectRenderDocCapture
```

The schema-8 artifact must record a completed generation advance, RenderDoc availability after restart, at least 320 additional accepted frames from both SceneView and GameView, one terminal capture request identity, and an existing non-empty `.rdc` path. The process must then complete package and Vulkan shutdown without a remaining `PackageGame.exe`, and `vk_validation.log` must remain empty.

Use the promoted stabilization gate after lifecycle, native ABI, rendering ownership, asset
publication, worker-drain, RenderDoc, or deployment changes:

```bat
Arisen\Scripts\Windows\validate_stability_stress.bat --config Release --cycles 2 --no-pause
```

This gate runs fast validation and the isolated Vulkan package suite once, then performs two full
GPU-required runtime cycles plus one in-process RenderDoc restart/capture per cycle. Its structured
report archives profile logs, eleven empty Vulkan validation logs, copied Production evidence,
world/terrain memory and shutdown baselines, Editor ownership/cache bounds, graphics-generation
advance, and each non-empty capture artifact under `.arisen/Logs`.

## Bundled Tracy Viewer

Tracy viewer and client protocols must match. Arisen's launcher always configures and builds the viewer from the same bundled Tracy source used by `com.arisen.core.native`; do not substitute an arbitrary installed Tracy version. The bundled source currently reports Tracy `0.11.2`, with `TracyVersion.hpp` remaining the version authority.

Run this from the repository root:

```bat
Arisen\Scripts\Windows\open_tracy_profiler.bat --config Release --no-pause
```

The script:

- builds the bundled `tracy-profiler` CMake target under `Arisen\Projects\TracyProfiler`;
- writes `Arisen\Projects\TracyProfiler\open_tracy_profiler.log`;
- launches the resulting `Release\tracy-profiler.exe`;
- accepts `--clean` when a clean viewer rebuild is required.

If the viewer reports `Incompatible protocol`, close that viewer and run the command above again. A successful regular workspace build does not rebuild the viewer executable.

## Model Scene Capture Recipe

The canonical Development profile has `EnableProfiler: true` and renders the package-owned Lantern model scene. Build it, open the bundled viewer, and launch an unbounded manual session from the repository root:

```bat
Arisen\Scripts\Windows\build_workspace.bat --config Debug --profile Development
Arisen\Scripts\Windows\open_tracy_profiler.bat --config Release --no-pause
Arisen\Development\PackageGame\.arisen\bin\Development\Debug\PackageGame.exe --workspace Arisen\Development\PackageGame --profile Development
```

In Tracy, connect to the discovered `PackageGame` client on localhost. Do not pass `--smoke-mode` or `--frames` for this workflow; those options intentionally bound validation and are too short for interactive timeline analysis. Close the PackageGame window when the capture interval is complete, then save the capture from Tracy when a persistent `.tracy` file is needed.

The Development path loads and renders already-generated Lantern children. It does not reimport glTF every frame. To profile explicit model import, build and launch the `Editor` profile with the same viewer, select the stable `Lantern.arismodel` root, and invoke `Reimport` while connected. That action should produce `ModelSourceReimporter.Reimport`, nested planner/emitter zones, generated-child plots, and a separate cooked-output invalidation zone.

## Timeline Inspection

Start with these zone groups:

- Model import: `ModelSourceReimporter.Reimport`, `GltfModelImportPlanner.CreatePlan`, `GltfModelImportEmitter.Emit`, and `ModelSourceReimporter.InvalidateCookedOutputs`.
- Scene activation: `RuntimeSceneService.LoadScene` and `SceneAssetLoader.LoadSceneSource`.
- Frame/render setup: `RuntimeFrame` frame marks, `RenderSubsystem.Tick`, and `RenderPipeline.SetupGraph`.
- Graph/scheduling: `RenderGraph.Compile`, `RenderGraph.RecordLayer`, `RenderGraph.Submit`, `TaskGraph.Execute`, and `TaskGraph.Layer`.
- Worker recording: `ArisenWorker-N` threads and per-task spans such as `EnvironmentSkyPass[0]`, `DirectionalShadowPass[0]`, `GenericStaticMeshPass[N]`, `GenericTransparentStaticMeshPass[N]`, and `TonemapPass[0]`. Render command tasks execute inside `Profiler.Zone(task.Name)`.
- Setup work: `DirectionalShadowPass.Prepare` and other coarse setup spans outside command recording.

Then correlate these plot groups:

- `ModelImport.*`: planned and emitted child/material/image/texture counts, warnings, orphan/foreign output, and invalidated assets.
- `SceneLoad.*`: entity, camera, mesh renderer, light, and environment counts from the activated scene.
- `Render.*`: extracted and visible mesh items, camera/shadow culling, draw queues, materials, lights, environment state, output size, and frame depth.
- `RenderGraph.*`: pass/layer/work-item counts, compile-cache hits, culling, transitions, and transient texture lifetime/peak-live counts.
- `StaticMeshPass.*`, `TransparentStaticMeshPass.*`, and `DirectionalShadowPass.*`: draw/batch/work-item/object-buffer and shadow quality counts.
- `RenderSubmission.*`: acquire, submit, ticket, and presentation state.

A normal Development capture will show scene activation near startup and recurring render markers afterward. Explicit model-import markers appear only when an Editor reimport is requested.

## World-Streaming Capture Recipe

Open the bundled Tracy viewer before launching this bounded Development scenario:

```bat
Arisen\Development\PackageGame\.arisen\bin\Development\Debug\PackageGame.exe --workspace Arisen\Development\PackageGame --profile Development --smoke-mode world-streaming --smoke-summary-output Arisen\Development\PackageGame\.arisen\Logs\world-streaming-summary-Development-manual.json --visual-summary --visual-summary-output Arisen\Development\PackageGame\.arisen\Logs\world-streaming-visual-Development-manual.json
```

The scenario crosses the canonical world repeatedly and exits after four complete load/unload cycles plus shutdown inspection. Use the stable world/cell identity in the following zones to attribute individual spikes:

- `WorldStreaming.CellRead/<cell-id>` encloses one cell's worker read/decode/validation and residency acquisition path.
- `WorldStreaming.Activate/<cell-id>` encloses its frame-boundary ECS activation.
- `WorldStreaming.Unload/<cell-id>` encloses its frame-boundary ECS destruction and owner release.
- `WorldStreaming.Read`, `WorldStreaming.Decode`, `WorldStreaming.Validate`, `WorldStreaming.AcquireResidency`, and `WorldStreaming.WaitForResources` separate the coarse phases.
- `WorldStreamingSmoke.AfterFrame` shows validation bookkeeping and is not production streaming work.

Correlate the zones with `WorldStreaming.*` plots for queued/active/in-flight/waiting/ready/failed/cancelled state counts, in-flight and decoded bytes, peaks, cancellations, failures, stale completions, budget stalls, and the last load/activation/unload times. `AssetResidency.*` plots report owners, resident/waiting/ready/failed resources, CPU cooked bytes, prepared GPU estimates, prepared descriptors, setup/eviction/failure/budget pressure, pending disposal, and setup time. These counters are sampled once at coarse service/frame boundaries, not inside entity or draw loops.

## Terrain-Streaming Capture Recipe

Use the dedicated terrain reliability path when profiling LOD, setup, draw pressure, and release behavior:

```bat
Arisen\Development\PackageGame\.arisen\bin\Development\Debug\PackageGame.exe --workspace Arisen\Development\PackageGame --profile Development --smoke-mode terrain-streaming --smoke-summary-output Arisen\Development\PackageGame\.arisen\Logs\terrain-streaming-summary-Development-manual.json --visual-summary --visual-summary-output Arisen\Development\PackageGame\.arisen\Logs\terrain-streaming-visual-Development-manual.json
```

The scenario follows near/boundary/far cameras, performs one origin rebase, returns to the start, and completes four load/reload/unload soak cycles. The validation-only `TerrainStreamingSmoke.AfterFrame` zone and `TerrainStreamingSmoke.*` plots identify scenario bookkeeping; exclude them when measuring production terrain cost.

Inspect these production zone groups:

- cooking/reading: `Terrain.CookAsset`, `Terrain.CookRoot`, `Terrain.CookTile`, `Terrain.CookRootPayload`, `Terrain.CookChangedTiles`, `Terrain.CookTilePayload`, `Terrain.ReadRootPayload`, and `Terrain.ReadTilePayload`;
- setup/LOD: `Terrain.ExtractVisibleTiles`, `Terrain.PrepareResources`, `Terrain.LodPlan`, `Terrain.ExpandPatches`, `Terrain.PrepareCascades`, package-owned `Terrain.SetupRoot`/`Terrain.SetupTile`, and `Terrain.ReleasePreparedResource`;
- recording: `TerrainOpaquePass.Record` and `TerrainDirectionalShadowPass.Record` on the RenderGraph worker tasks.

Correlate them with `Terrain.*` plots for extracted/visible tiles, LOD 0-12 histogram, candidate/selected/culled/overflow patches, neighbor refinements, prepared/rejected/submitted opaque draws, shadow draws/drops, and seam violations. `Terrain.Residency.*` reports root/tile counts, CPU height/weight/error bytes, prepared height/weight/error/layer bytes, descriptors, setup time, budget pressure, and pending disposal. All are sampled at coarse setup, submission, or residency boundaries; there are no per-sample, per-vertex, or per-entity counters.

## Bounded Validation And Manual Profiling

Use bounded runtime validation as the automated rendering gate:

```bat
Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1
```

The scene smoke mode may internally run enough frames to render deferred scene setup, but the caller still requests a bounded one-frame validation window. The full command also runs deterministic world- and terrain-streaming scenarios for Development and Production and repeats both from an isolated copied Production output. Those gates validate machine-readable state, memory, visual, shutdown, source-access, and Vulkan results; they do not attempt to judge timeline performance. Longer visual profiling should be a manual Tracy session from a profiler-enabled profile such as `Editor` or `Development`, not a default CI requirement. Production keeps profiler instrumentation disabled even though its functional streaming gates run.

## Launcher Integration Target

The editor/launcher should not embed Tracy's full UI yet. The near-term launcher integration should be a control surface:

- show whether the selected profile has profiler instrumentation enabled;
- launch the external Tracy Profiler viewer when available;
- launch PackageGame/editor with a profiler-enabled profile;
- list saved `.tracy` captures under workspace-local logs when capture export is added.

A custom in-editor profiler timeline can be added later for simplified engine diagnostics, but the full trace viewer should remain Tracy until Arisen has a strong reason to own that UI.

## Hot-Path Rules

- Use static zone names in tight loops where possible.
- Avoid per-entity profiler zones.
- Keep service-registry lookups out of profiled hot loops.
- Prefer coarse zones around frame setup, graph compile, task layers, pass work items, submission, and backend queue work.
- Use Tracy plots/zones for recurring frame telemetry. Ordinary warmed rendering does not emit text diagnostics.
- Set `ARISEN_RENDER_DIAGNOSTICS` before process start only for targeted text diagnosis. Categories are `frame`, `submission`, `graph`, and `passes`; comma-separated combinations and `all` are accepted. `graph` is event-bounded to initial or changed topology, while the other categories are deliberately verbose and should be enabled only for a bounded diagnosis.
- `KernelLog.Info` and the engine-wide `ILogger.Log` contract are release-visible informational channels and map to `Logger.Info`. Direct `Logger.Log` is debug-detail severity and may be filtered by the native Release logger. Lifecycle or validation evidence must use an informational-or-higher channel or a structured artifact; it must not depend on debug output surviving Release filtering.
- Do not add frame-modulo logging, direct native console output, or a timer/throttle to conceal warmed log volume. Keep validation, failure, and lifecycle diagnostics unconditional and fix recurring failures at their owner.
- Keep Production profile instrumentation disabled by default.
