# Architecture Spec: Profiling And Runtime Tracing

Arisen uses Tracy as the first production-grade realtime profiler backend. The engine should emit low-overhead zones, plots, and frame marks from package code, then inspect the timeline in the external Tracy Profiler viewer.

## Current Policy

- `com.arisen.core.native` contains the Tracy client dependency and the native `Core.Diagnostic` profiler bridge.
- `com.arisen.core` exposes the managed facade: `Profiler.Zone`, `Profiler.FrameMark`, `Profiler.FrameMarkNamed`, `Profiler.PlotValue`, and `Profiler.SetThreadName`.
- Profiles with `EnableProfiler: true` define `ARISEN_PROFILER_ENABLED`.
- Profiles with `EnableProfiler: false` do not define profiler instrumentation.
- The regular Arisen build links the Tracy client, but it does not build the Tracy Profiler viewer executable.

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

## Bounded Validation And Manual Profiling

Use bounded runtime validation as the automated rendering gate:

```bat
Arisen\Scripts\Windows\validate_runtime.bat --no-pause --config Debug --smoke-mode scene --frames 1
```

The scene smoke mode may internally run enough frames to render deferred scene setup, but the caller still requests a bounded one-frame validation window. Longer visual profiling should be a manual Tracy session from a profiler-enabled profile such as `Editor` or `Development`, not a default CI requirement.

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
- Keep Production profile instrumentation disabled by default.
