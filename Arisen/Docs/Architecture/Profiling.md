# Architecture Spec: Profiling And Runtime Tracing

Arisen uses Tracy as the first production-grade realtime profiler backend. The engine should emit low-overhead zones, plots, and frame marks from package code, then inspect the timeline in the external Tracy Profiler viewer.

## Current Policy

- `com.arisen.core.native` contains the Tracy client dependency and the native `Core.Diagnostic` profiler bridge.
- `com.arisen.core` exposes the managed facade: `Profiler.Zone`, `Profiler.FrameMark`, `Profiler.FrameMarkNamed`, `Profiler.PlotValue`, and `Profiler.SetThreadName`.
- Profiles with `EnableProfiler: true` define `ARISEN_PROFILER_ENABLED`.
- Profiles with `EnableProfiler: false` do not define profiler instrumentation.
- The regular Arisen build links the Tracy client, but it does not build the Tracy Profiler viewer executable.

## Timeline Workflow

1. Build and run a profiler-enabled profile such as `Editor` or `Development`.
2. Open the matching bundled Tracy Profiler viewer:
   - `Arisen\Scripts\Windows\open_tracy_profiler.bat`
3. Connect to the running Arisen process from the Tracy start/connect screen.
4. Inspect the timeline.

Tracy viewer and client protocols must match. Do not use an arbitrary installed Tracy version when connecting to Arisen. If the viewer reports `Incompatible protocol`, rebuild and launch the bundled viewer with `open_tracy_profiler.bat`.

Expected first timeline markers:

- `RuntimeFrame` frame marks.
- `RenderSubsystem.Tick`.
- `RenderPipeline.SetupGraph`.
- `RenderGraph.Compile`.
- `RenderGraph.RecordLayer`.
- `TaskGraph.Execute`.
- `TaskGraph.Layer`.
- `ArisenWorker-N` worker threads.
- Per-task spans such as `ClearPass[0]`, `SmokeTrianglePass[0]`, and `GeometryPass[N]`.
- Plots for render graph pass/layer/work-item counts and render snapshot size/counts.

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
