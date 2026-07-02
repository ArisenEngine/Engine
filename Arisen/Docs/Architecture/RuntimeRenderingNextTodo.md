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

---

## Milestone 1 - Runtime Boot Smoke For PackageGame

**Goal:** Prove the default workspace can boot, initialize selected packages, and shut down cleanly from a command-line smoke path.

### TODO

- [ ] Add a headless/smoke launch mode for generated PackageGame binaries.
  - [ ] Support a bounded frame count such as `--frames 1` or `--smoke`.
  - [ ] Ensure the default engine loop exits cleanly without requiring editor UI.
  - [ ] Emit package load order, subsystem order, registered services, and shutdown order.
- [ ] Add a BuildTool or script entry point for runtime smoke validation.
  - [ ] Generate/build the Development profile.
  - [ ] Launch the generated executable with explicit `--workspace`, `--profile`, and `--config`.
  - [ ] Fail on non-zero process exit, missing resolved manifest, or package/service boot error.
- [ ] Add smoke coverage for all important profiles.
  - [ ] Development loads editor/tooling packages when expected.
  - [ ] Production excludes editor-only packages.
  - [ ] RHIVulkanTesting loads Vulkan test packages and `com.arisen.testrunner`.

### Acceptance Criteria

- A single command can prove PackageGame boots and exits cleanly.
- Boot logs show deterministic package/subsystem/service state.
- Failures point to the owning package and profile.

---

## Milestone 2 - Platform Window Contract And Lifecycle

**Goal:** Make window creation a validated service boundary that rendering can rely on.

### TODO

- [ ] Finalize the platform/window service contract shape.
  - [ ] Confirm `IWindowProvider` ownership under `ArisenKernel.Contracts`.
  - [ ] Define the minimum runtime data rendering needs: native handle, dimensions, DPI scale, surface kind, resize events.
  - [ ] Document whether the editor viewport and standalone game window share the same contract or use separate adapter services.
- [ ] Implement deterministic desktop window lifecycle.
  - [ ] Create window before RHI swapchain/device initialization.
  - [ ] Surface resize and close events through a low-allocation path.
  - [ ] Shut down window resources after rendering/RHI shutdown.
- [ ] Validate package metadata.
  - [ ] `com.arisen.platform.desktop` provides the platform/window service.
  - [ ] Rendering/RHI packages require only contracts, not concrete platform package types.

### Acceptance Criteria

- Runtime smoke can create and destroy a desktop window.
- RHI initialization can consume window/surface data without concrete package references.
- Production profile window boot works without editor packages.

---

## Milestone 3 - Vulkan RHI Device Bring-Up

**Goal:** Turn `com.arisen.rhi.vulkan.native` into a verified selected backend that provides a usable RHI device.

### TODO

- [ ] Finalize RHI service contracts.
  - [ ] `IRHIBackend` advertises backend capability such as `vulkan`.
  - [ ] `IRHIDevice` represents the initialized device used by rendering.
  - [ ] Decide whether swapchain/surface interfaces are separate contracts.
- [ ] Implement Vulkan backend initialization path.
  - [ ] Load native Vulkan runtime payloads through package native runtime rules.
  - [ ] Create instance/device/queue/surface/swapchain from platform window data.
  - [ ] Register `IRHIDevice` only after successful initialization.
  - [ ] Unregister/shutdown in reverse package/subsystem order.
- [ ] Add diagnostics.
  - [ ] Log selected adapter, validation layer status, instance/device extension state, and swapchain format.
  - [ ] Fail clearly when Vulkan SDK/runtime/driver requirements are missing.
- [ ] Add RHI smoke tests.
  - [ ] Device creation succeeds on supported machines.
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
  - [ ] Define pass inputs, outputs, resource handles, and execution context.
  - [ ] Keep graph compilation allocation-free after setup where practical.
  - [ ] Validate pass dependency cycles and missing resources.
- [ ] Implement a clear-color frame.
  - [ ] Add a presentable swapchain target resource.
  - [ ] Add a clear pass.
  - [ ] Add a present pass.
  - [ ] Execute for one frame in smoke mode.
- [ ] Implement a triangle frame after clear-color works.
  - [ ] Add minimal shader asset or embedded shader path.
  - [ ] Create pipeline state through the RHI abstraction.
  - [ ] Record and submit draw commands through RenderGraph execution.
- [ ] Add frame diagnostics.
  - [ ] Log graph pass order.
  - [ ] Log culled passes and resource transitions.
  - [ ] Capture enough metadata to debug failed frame setup.

### Acceptance Criteria

- A one-frame smoke launch clears/presents through RenderGraph.
- A later smoke launch draws a triangle through RenderGraph.
- The implementation does not bypass the package/RHI boundaries to get a frame on screen.

---

## Milestone 5 - Asset Pipeline Vertical Slice

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

## Milestone 6 - Editor Viewport Integration

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

## Milestone 7 - Runtime Test And CI Gate

**Goal:** Promote the vertical slice into a repeatable validation gate.

### TODO

- [ ] Add `validate_runtime` or equivalent script.
  - [ ] Run fast unit tests.
  - [ ] Validate PackageGame profiles.
  - [ ] Build required generated outputs.
  - [ ] Run headless/smoke boot.
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

1. **Runtime boot smoke mode** for PackageGame.
2. **Platform/window contract cleanup** so RHI initialization has stable input.
3. **Vulkan `IRHIDevice` bring-up** with clear diagnostics.
4. **RenderGraph clear-color frame**.
5. **RenderGraph triangle frame**.
6. **First cooked shader asset path**.
7. **Editor/runtime diagnostics and viewport integration**.

This keeps the work vertical and testable. Each step should leave the engine in a better running state than before.

---

## Suggested First Implementation Task

Start with:

> Add a bounded runtime smoke mode for generated PackageGame binaries and wire it into a script.

Why first:

- It proves the completed package infrastructure under real process boot.
- It creates the validation harness needed for Vulkan, RenderGraph, assets, and editor viewport work.
- It is lower risk than starting directly inside Vulkan or RenderGraph.
- It gives us clean logs for package load order, service registration, subsystem initialization, and shutdown.

Expected output:

- A command or script that launches PackageGame for a bounded run.
- Exit code `0` on clean boot/shutdown and non-zero on boot failure.
- Logs showing workspace, profile, resolved manifest, packages, services, subsystems, and shutdown order.
