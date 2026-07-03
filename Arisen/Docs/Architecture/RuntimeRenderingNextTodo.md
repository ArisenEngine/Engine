# Arisen Runtime And Rendering: Next TODO Roadmap

**Date:** 2026-07-02  
**Scope:** Next implementation plan after completing the package-oriented engine foundation.  
**Primary goal:** Prove the package model by booting a real vertical engine slice: workspace -> packages -> services -> platform window -> RHI backend -> RenderGraph -> frame output -> editor/package diagnostics.

---

## Current State Summary

The package-oriented foundation is now in place:

- Workspace and package manifests are the source of truth.
- `ArisenBuildTool validate` enforces package graph, service, layer, native runtime, and manifest rules.
- Runtime boot prefers generated `manifest.resolved.json`.
- `PackageSubsystem` owns deterministic package load and unload.
- Services, subsystems, native runtimes, launcher validation, package graph UI, and registry/cache acquisition exist.
- The canonical development workspace is `Arisen/Development/PackageGame`.
- Runtime validation exists through `Arisen/Scripts/Windows/validate_runtime.bat --no-pause --config Debug --frames 1`.
- Standalone runtime window creation is owned by `IWindowProvider`; editor builds use `ARISEN_ENGINE_EDITOR` and do not create a separate native game window.
- Vulkan runtime initialization now validates the Win32 window contract, creates the selected backend against the HAL window surface id, initializes the runtime swapchain, and registers `IRHIDevice`; editor initialization remains on a virtual/device-only path until editor-hosted surfaces are implemented.
- Runtime smoke now exercises RHI warmup through a rendering-owned `PostInit` subsystem in non-editor builds. Editor builds keep hardware warmup in the editor boot pipeline.

The next risk is no longer package graph correctness. The next risk is whether the selected packages form a real working engine at runtime, especially across the platform/RHI/rendering boundary.

---

## Guiding Rules For This Roadmap

1. **Keep the package graph honest.** New runtime/rendering features must be represented by package metadata, service contracts, and explicit dependencies.
2. **The user package composes; domain packages stay backend-agnostic.** `com.arisen.packagegame` selects Vulkan for now, while rendering packages consume shared contracts.
3. **Render work goes through RenderGraph.** Avoid ad hoc direct RHI orchestration outside backend packages and render-pass execution.
4. **No hot-path service lookups.** Resolve coarse-grained services during initialization; cache typed references for frame execution.
5. **Make smoke tests executable.** Every major runtime milestone should have a CLI or package-test workflow that can run without manual editor clicking.
6. **Prefer small vertical slices.** A clear window color, one triangle, one asset, and one editor viewport are better than a large incomplete renderer.
7. **Generated outputs remain disposable.** Fix source manifests, package code, or generators instead of hand-editing `.arisen/` outputs.
8. **Editor/runtime ownership is compile-time policy.** Use `ARISEN_ENGINE_EDITOR` for editor-only ownership branches; do not revive runtime `EngineConfig.IsEditor` checks for platform/RHI behavior.

---

## Milestone 1 - Runtime Boot Smoke For PackageGame

**Goal:** Prove the default workspace can boot, initialize selected packages, and shut down cleanly from a command-line smoke path.

### TODO

- [x] Add a headless/smoke launch mode for generated PackageGame binaries.
  - [x] Support a bounded frame count such as `--frames 1` or `--smoke`.
  - [x] Ensure the default engine loop exits cleanly without requiring editor UI.
  - [x] Emit package load order, subsystem order, registered services, and shutdown order.
- [x] Add a BuildTool or script entry point for runtime smoke validation.
  - [x] Generate/build the selected profile.
  - [x] Launch the generated executable with explicit `--workspace`, `--profile`, and `--config`.
  - [x] Fail on non-zero process exit, missing executable, build error, or package/service boot error.
- [x] Add smoke coverage for all important profiles.
  - [x] Editor loads editor/tooling packages when expected.
  - [x] Development boots the standalone runtime path with profiler diagnostics enabled.
  - [x] Production excludes editor-only packages.
  - [x] RHIVulkanTesting loads Vulkan test packages and `com.arisen.testrunner`.

### Acceptance Criteria

- A single command can prove PackageGame boots and exits cleanly.
- Boot logs show deterministic package/subsystem/service state.
- Failures point to the owning package and profile.

---

## Milestone 2 - Platform Window Contract And Lifecycle

**Goal:** Make window creation a validated service boundary that rendering can rely on.

### TODO

- [x] Finalize the platform/window service contract shape.
  - [x] Confirm `IWindowProvider` ownership under `ArisenKernel.Contracts`.
  - [x] Define the minimum runtime data rendering needs: native handle, dimensions, DPI scale, surface kind, resize events.
  - [x] Document whether the editor viewport and standalone game window share the same contract or use separate adapter services.
    - Standalone runtime uses `IWindowProvider` for the main Win32 window.
    - Editor builds use `ARISEN_ENGINE_EDITOR`; the UI host owns native windows, and viewport surfaces are registered later through editor-hosted/virtual/shared-handle adapters.
- [x] Implement deterministic desktop window lifecycle.
  - [x] Create window before RHI swapchain/device initialization.
  - [x] Surface resize and close events through a low-allocation path.
  - [x] Shut down window resources after rendering/RHI shutdown.
- [x] Validate package metadata.
  - [x] `com.arisen.platform.desktop` provides the platform/window service.
  - [x] Rendering/RHI packages require only contracts, not concrete platform package types.

### Acceptance Criteria

- Runtime smoke can create and destroy a desktop window.
- RHI initialization can consume window/surface data without concrete package references.
- Production profile window boot works without editor packages.

---

## Milestone 3 - Vulkan RHI Device Bring-Up

**Goal:** Turn `com.arisen.rhi.vulkan.native` into a verified selected backend that provides a usable RHI device.

### TODO

- [x] Define editor/runtime initialization policy.
  - [x] Editor builds use `ARISEN_ENGINE_EDITOR` and do not require a standalone native window.
  - [x] Runtime builds require `IWindowProvider` to expose a valid Win32 window before Vulkan device registration.
  - [x] Editor viewport surfaces remain a later editor-hosted/virtual/shared-handle path.
- [ ] Finalize RHI service contracts.
  - [x] `IRHIBackend` advertises backend capability such as `vulkan`.
  - [x] `IRHIDevice` represents the initialized device used by rendering.
  - [x] Decide whether swapchain/surface interfaces are separate contracts.
    - For the first RenderGraph vertical slice, surfaces and swapchains remain owned by the initialized `IRHIDevice`/backend path rather than being separate services. This prevents reusable rendering packages from depending on concrete platform or Vulkan packages before editor-hosted surfaces and multi-window ownership are finalized.
    - The managed RHI wrapper now exposes native instance/surface facts needed for diagnostics: validation state, max frames in flight, surface availability, selected swapchain format, selected present mode, and linear color-space support.
- [ ] Implement Vulkan backend initialization path.
  - [x] Load native Vulkan runtime payloads through package native runtime rules.
  - [x] Create instance/device/queue/surface/swapchain from platform window data.
  - [x] Register `IRHIDevice` only after successful initialization.
  - [ ] Unregister/shutdown in reverse package/subsystem order.
- [ ] Add diagnostics.
  - [x] Log selected initialization mode: editor virtual surface or runtime Win32 window.
  - [x] Log available native RHI diagnostics: validation layer status, max frames in flight, surface availability, selected swapchain format, present mode, and linear color-space support.
  - [ ] Add native exports for selected adapter and instance/device extension state, then log them during backend initialization.
  - [ ] Fail clearly when Vulkan SDK/runtime/driver requirements are missing.
- [ ] Add RHI smoke tests.
  - [x] Device creation succeeds on supported machines.
  - [ ] Missing required native export or payload fails validation before boot.
  - [ ] Backend capability mismatch fails validation before boot.

### Acceptance Criteria

- PackageGame can select Vulkan by manifest/profile and receive an initialized `IRHIDevice`.
- Rendering/domain packages remain backend-agnostic.
- Vulkan shutdown is clean under the package lifecycle.

---

## Milestone 4 - Minimal RenderGraph Frame

**Goal:** Produce the first deterministic frame through RenderGraph, not through one-off backend calls.

### TODO

- [ ] Finalize minimal RenderGraph execution interfaces.
  - [x] Define pass inputs, outputs, resource handles, and execution context for the first frame target.
  - [ ] Keep graph compilation allocation-free after setup where practical.
  - [ ] Validate pass dependency cycles and missing resources.
- [x] Implement a clear-color frame.
  - [x] Add a presentable swapchain target resource.
  - [x] Add a clear pass.
  - [x] Add engine-owned frame target prepare/finalization passes.
  - [x] Execute for one frame in smoke mode.
    - Production smoke now registers the platform-owned Win32 surface in `RenderSubsystem`, builds the generic pipeline, records `ClearPass`, and submits the RenderGraph against the runtime swapchain.
- [x] Implement a triangle frame after clear-color works.
  - [x] Add minimal shader asset or embedded shader path.
    - Current slice uses an embedded HLSL source compiled during pipeline setup. This is temporary until Milestone 5 introduces cooked shader assets.
  - [x] Create pipeline state through the RHI abstraction.
    - `SmokeTrianglePass` creates RHI shader programs, a graphics pipeline state, swapchain color format metadata, and a cached graphics pipeline.
  - [x] Record and submit draw commands through RenderGraph execution.
    - Production smoke logs `SmokeTrianglePass` pipeline creation, `SmokeTrianglePass.Record`, and `RenderGraph` submitting five nodes.
- [ ] Add frame diagnostics.
  - [ ] Log graph pass order.
  - [ ] Log culled passes and resource transitions.
  - [ ] Capture enough metadata to debug failed frame setup.
  - [x] Add realtime profiler trace hooks for render frame timing.
    - Profiles with `EnableProfiler: true` now emit `ARISEN_PROFILER_ENABLED`.
    - Runtime frames mark `RuntimeFrame`.
    - Render snapshots plot draw count, camera count, and output size.
    - RenderGraph traces compile, record-layer, work-item, and submit spans.
    - TaskGraph traces execute/layer spans and worker-thread task spans.
- [x] Add first CPU command-list boundary.
  - [x] Introduce `RenderCommandList` as the pass-facing command API over the current RHI command buffer.
  - [x] Update built-in passes to record through `RenderCommandList`.
  - [x] Keep native/backend-specific command details behind RHI wrappers.

### Acceptance Criteria

- A one-frame smoke launch clears/presents through RenderGraph.
- A smoke launch draws a triangle through RenderGraph.
- The implementation does not bypass the package/RHI boundaries to get a frame on screen.

---

## Milestone 5 - Render Threading Architecture

**Goal:** Lock in the multi-threaded rendering shape before real scene content makes it expensive to migrate.

### TODO

- [x] Define the high-level threading model.
  - [x] Simulation/ECS produces data.
  - [x] Render extraction produces a stable frame snapshot.
  - [x] RenderGraph setup/compile owns pass/resource ordering.
  - [x] Worker threads record command lists.
  - [x] A submission/output owner submits in graph order and presents.
- [x] Add first frame snapshot contract.
  - [x] Define camera, surface, frame index, draw-list, and output metadata layout.
  - [x] Ensure render workers do not read mutable ECS state directly.
  - [x] Keep draw data contiguous and suitable for range splitting.
    - `RenderFrameSnapshot` now carries output target metadata plus camera and draw-list pointer/count pairs copied into `FrameArena`.
    - `RenderContext` forwards to the snapshot so existing passes can migrate incrementally while consuming stable frame data.
- [x] Improve command recording granularity.
  - [x] Allow heavy passes to split draw ranges into multiple record tasks.
    - `RenderPassWorkItem` defines pass-level work or draw ranges into `RenderFrameSnapshot.DrawList`.
    - `GeometryPass` splits draw commands into contiguous chunks while clear/smoke/final passes stay single work-item passes.
  - [x] Keep pass-level recording for small passes such as clear/final output.
  - [x] Validate command pool ownership per worker/surface/frame.
    - RenderGraph still keys command pools by worker thread and surface, and now releases a losing pool if workers race to create the same key.
- [ ] Harden RenderGraph execution.
  - [ ] Separate graph compile allocations from per-frame recording where practical.
  - [ ] Add pass-order diagnostics for compiled layers.
  - [x] Add realtime profiler diagnostics for compiled graph execution.
    - Tracy zones now show RenderGraph compile, record layers, work items, ordered submit, and TaskGraph worker tasks.
  - [x] Add explicit failure handling when a worker pass fails.
    - Recording tasks capture exceptions with pass/work-item context and throw an aggregate failure after the layer finishes.
- [ ] Define submission ownership.
  - [ ] Centralize swapchain acquire/present, fences, and frame-resource recycling.
  - [ ] Keep queue submission ordered by compiled graph dependencies.
  - [ ] Prepare for future graphics/compute/copy queue families without exposing backend details to passes.

### Acceptance Criteria

- Production rendering code records through `RenderCommandList`, not directly against backend/native APIs.
- Render workers consume immutable frame data.
- The design supports both pass-level and draw-range-level parallel command recording.
- Runtime smoke remains green while the architecture boundary is introduced incrementally.

---

## Milestone 6 - Asset Pipeline Vertical Slice

**Goal:** Connect package/workspace assets to runtime rendering through stable asset IDs and cooked data.

### TODO

- [ ] Finalize the first asset types.
  - [ ] Shader source/cooked shader.
  - [ ] Texture2D.
  - [ ] Mesh or simple geometry buffer.
- [ ] Implement `.meta` generation and stable GUID assignment.
  - [ ] Create missing meta files for new assets.
  - [ ] Preserve GUIDs across moves/renames.
  - [ ] Detect duplicate GUIDs.
- [ ] Implement first cooking path.
  - [ ] Cook shader assets for Vulkan.
  - [ ] Emit cooked outputs under workspace/package cache.
  - [ ] Emit an asset manifest mapping GUID to cooked artifact.
- [ ] Add runtime asset database service.
  - [ ] Load by GUID, not by source path.
  - [ ] Return typed handles suitable for rendering.
  - [ ] Track lifetime/reference state.
- [ ] Use a cooked asset in the minimal RenderGraph frame.

### Acceptance Criteria

- Runtime rendering can consume at least one cooked asset by GUID.
- Raw asset parsing is not required during frame execution.
- Asset diagnostics identify missing meta, duplicate GUIDs, and missing cooked outputs.

---

## Milestone 7 - Editor Viewport Integration

**Goal:** Make the launcher/editor a useful visual control surface for the runtime renderer.

### TODO

- [ ] Define editor viewport hosting model.
  - [ ] Decide initial path: standalone child/native window, shared texture, or staged bitmap fallback.
  - [ ] Keep the future zero-copy shared texture path documented even if first implementation is simpler.
- [ ] Add package-manager/runtime diagnostics panels.
  - [ ] Show selected profile, package load state, services, subsystem order, and RHI backend.
  - [ ] Show RenderGraph pass order for the active frame.
  - [ ] Show validation/build/boot output in one place.
- [ ] Add launch controls for smoke modes.
  - [ ] Boot one frame.
  - [ ] Boot N frames.
  - [ ] Boot editor/runtime profile with selected configuration.
- [ ] Add viewport resize handling.
  - [ ] Propagate editor viewport size changes to platform/RHI swapchain.
  - [ ] Rebuild dependent RenderGraph resources safely.

### Acceptance Criteria

- The editor can launch a runtime frame path and display useful diagnostics.
- Viewport lifecycle does not require hand-running generated executables.
- Runtime/editor integration still goes through package services.

---

## Milestone 8 - Runtime Test And CI Gate

**Goal:** Promote the vertical slice into a repeatable validation gate.

### TODO

- [x] Add `validate_runtime` or equivalent script.
  - [x] Run fast unit tests.
  - [x] Validate PackageGame profiles.
  - [x] Build required generated outputs.
  - [x] Run headless/smoke boot.
- [ ] Add artifact/log policy.
  - [ ] Store smoke logs under workspace `.arisen/Logs`.
  - [ ] Keep logs deterministic enough for diffing.
  - [ ] Capture crash/fatal package context.
- [ ] Add optional GPU-dependent test gating.
  - [ ] Allow CPU-only validation to pass on machines without Vulkan.
  - [ ] Run Vulkan smoke when Vulkan is available.
  - [ ] Report skipped GPU tests explicitly.

### Acceptance Criteria

- Developers can run one local command before committing runtime/rendering work.
- GPU-dependent failures are clear rather than mysterious.
- CI can run non-GPU validation and optionally run GPU validation on capable agents.

---

## Recommended Immediate Sprint

Implement in this order:

1. **Complete RHI service contracts** for surface and swapchain ownership.
2. **Add Vulkan diagnostics** for adapter, validation layers, extensions, and swapchain format.
3. **Render threading architecture boundary**.
4. **Profiler trace foundation for RenderGraph/TaskGraph**.
5. **First cooked shader asset path**.
6. **Editor/runtime diagnostics and viewport integration**.
7. **Runtime test/log policy hardening**.

This keeps the work vertical and testable. Each step should leave the engine in a better running state than before.

---

## Suggested Next Implementation Task

Start with:

> Finish RenderGraph execution hardening.

Why first:

- Runtime smoke now proves a shader-backed RenderGraph triangle frame can record and submit against the real platform swapchain.
- `SmokeTrianglePass` is intentionally a smoke/sample pass, not production scene rendering.
- The pass-facing command API, first frame snapshot contract, and draw-range command recording path now exist.
- Realtime Tracy trace hooks now expose frame, TaskGraph, RenderGraph, and pass work-item timing.
- The next risk is still RenderGraph compile/diagnostic quality: failed graph setup or worker recording needs precise pass/layer/resource context and resource diagnostics.

Expected output:

- Pass-order and compiled-layer diagnostics.
- Clear resource transition/culling diagnostics for the minimal frame.
- Reduced or isolated per-frame graph compile allocations where practical.
- `validate_runtime.bat --no-pause --config Debug --frames 1` remains green.
