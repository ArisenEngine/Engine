# Arisen Package-Oriented Engine: Next TODO Roadmap

**Date:** 2026-05-22  
**Scope:** Next implementation plan for making Arisen a robust package-oriented / microkernel engine.  
**Primary goal:** Keep the kernel thin, make packages the source of engine functionality, and make workspace manifests + package metadata the source of truth for build, boot, testing, editor tooling, and distribution.

---

## Current State Summary

The project already has the right high-level direction:

- The repository is organized around a canonical workspace: `Arisen/Development/PackageGame`.
- Runtime boot goes through `EngineBootstrapper.Run(args)` and resolves a workspace manifest.
- Packages live under `Local/com.*` and have `package.json` metadata.
- `ArisenBuildTool` generates profile-specific IDE/build outputs under `.arisen/`.
- There are clear architecture docs for project/workspace layout, package lifecycle, service registry, rendering, and build generation.
- Kernel services, package entry hooks, subsystem lifecycle, and profile-specific workspace generation already exist in early form.

The biggest next step is not to add many new engine features immediately. The priority should be to make the package model strict, deterministic, validated, and testable so future systems can safely plug into it.

---

## Guiding Rules For All Next Work

1. **Workspace is not a package.** Game/editor/runtime logic must live inside real packages under `Local/`, `.Cache/`, or a future package registry cache.
2. **Manifest + package metadata are source of truth.** Do not hand-maintain generated `.sln`, `.csproj`, or transient build outputs.
3. **Kernel remains thin.** Kernel owns boot, package loading, services, lifecycle, and diagnostics. ECS/rendering/editor/platform remain packages.
4. **Package dependencies must be explicit.** If code needs another package's types/contracts, `package.json` must declare that dependency.
5. **Services are coarse-grained boundaries.** Use `IServiceRegistry` for engine subsystem boundaries, not per-entity/per-draw hot paths.
6. **Resolved manifests should be preferred at runtime.** Runtime should use build-time topological sort whenever available.
7. **Validation must fail early.** Missing packages, bad entry classes, cycles, duplicate IDs, missing required services, and invalid profiles should be errors before engine boot continues.

---

## Milestone 1 — Make Package Resolution Strict And Deterministic

**Goal:** One reliable package graph pipeline shared conceptually by build, launcher, and runtime.

### TODO

- [ ] Define one canonical manifest schema for `manifest.json` and `package.json`.
  - [ ] Document required vs optional fields.
  - [ ] Normalize casing: `Name`/`Packages` in workspace manifests vs `id`/`dependencies` in package manifests.
  - [ ] Decide whether workspace keys stay PascalCase and package keys stay camelCase, or whether both become one style.
- [x] Add strict validation in `ArisenBuildTool` before generation.
  - [x] Duplicate package IDs fail when metadata conflicts; exact duplicate entries warn and are ignored.
  - [x] Missing package directory fails.
  - [x] Missing `package.json` fails.
  - [x] Missing dependency package fails.
  - [x] Dependency cycle fails, not just warns.
  - [x] Invalid package type fails.
  - [x] Invalid entry assembly/class metadata fails when applicable.
- [x] Upgrade topological sort behavior.
  - [x] Cycle detection should return a clear cycle chain.
  - [x] Missing dependency should produce a build error with package ID and dependency ID.
  - [x] Sort order should be stable for reproducible generated projects.
- [x] Generate `manifest.resolved.json` for every profile and configuration output directory.
  - [x] Include sorted packages.
  - [x] Include profile name.
  - [x] Include workspace root relative paths where possible.
  - [x] Include dependency metadata useful for debugging.
- [x] Make runtime boot treat `manifest.resolved.json` as authoritative when present.
  - [x] If resolved manifest exists but is invalid, fail loudly in development profile.
  - [x] Only fall back to raw `manifest.json` if explicitly allowed via `--allow-manifest-fallback`.

### Acceptance Criteria

- Building a workspace with missing/cyclic dependencies fails before project generation.
- Runtime package load order is identical to build-time resolved order.
- `manifest.resolved.json` can be inspected to understand exactly what will boot.

---

## Milestone 2 — Unify Runtime Package Loading Around `PackageSubsystem`

**Goal:** Avoid split responsibility between `EngineBootstrapper`, `EngineKernel.LoadPackages`, and `PackageSubsystem`.

### Current Concern

There are currently multiple places involved in package loading/registration:

- `EngineBootstrapper` reads manifests and passes package URLs.
- `EngineKernel.LoadPackages()` loads entry assemblies and calls `OnLoad()`.
- `PackageSubsystem` can also discover/load packages and tracks loaded packages.

This works as an early implementation, but it risks duplicated behavior, inconsistent ordering, and future bugs.

### TODO

- [x] Make `PackageSubsystem` the single owner of package mounting/loading/unloading.
- [x] Change `EngineKernel` so it asks `PackageSubsystem` to load a pre-resolved package list instead of directly loading packages.
- [x] Ensure `IPackageEntry.OnLoad(IServiceRegistry)` and `OnUnload(IServiceRegistry)` are called in deterministic order.
- [x] Ensure package unload order is reverse topological order.
- [x] Store all loaded package info in one authoritative collection.
- [x] Support managed, native, and hybrid package types explicitly.
- [x] Improve error reporting:
  - [x] Entry assembly not found.
  - [x] Entry class not found.
  - [x] Entry class does not implement `IPackageEntry`.
  - [x] `OnLoad()` exception includes package ID and class name.
- [x] Add package load context policy.
  - [x] Default context is used for `ArisenKernel.dll` and assemblies deployed under `AppContext.BaseDirectory`.
  - [x] Package-local managed assemblies use collectible `PackageLoadContext`; unloadability is best-effort after `OnUnload()` and reference cleanup.

### Acceptance Criteria

- There is one code path for package mount/load/unload.
- Package list in editor/debug UI matches actual loaded runtime packages.
- Package load/unload order can be logged and tested.

---

## Milestone 3 — Implement Service Contract Validation

**Goal:** Package `services.provides` and `services.requires` become enforceable contracts, not only metadata.

### TODO

- [x] Finalize service contract format in `package.json`.
  - [x] Support simple string form: `"ArisenKernel.Contracts.IRHIFactory"`.
  - [x] Support object form with `interface`, optional integer `priority`, optional string-array `capabilities`, requirement-only `optional`, and provider/requirement `deferred` flags.
- [x] During package resolution, validate required services can be provided by the selected package set.
- [x] During package load, validate provided services are actually registered by the package's `OnLoad()`.
- [x] During boot, validate all required services are available before dependent subsystems initialize.
- [x] Add duplicate service policy.
  - [x] Duplicate selected providers for the same service contract fail validation. Priority is accepted as metadata but is not used for automatic overrides until profile-level provider selection exists.
- [x] Add optional service semantics.
  - [x] Required services fail boot.
  - [x] Optional services log diagnostics only.
  - [x] Deferred services participate in graph validation but are skipped during initial package-mount registration checks.
- [x] Add service registry introspection for editor/debugging.
  - [x] List registered services.
  - [x] Show provider package if known.
  - [x] Show duplicate/overridden providers policy: duplicates are validation errors, so there are no runtime overrides in the current registry model.

### Acceptance Criteria

- A package declaring a required service fails validation if no selected package provides it.
- If a provider package forgets to register its promised service, boot fails with a clear message.
- Editor/package manager can show service dependency health.

---

## Milestone 4 — Make Subsystems Metadata-Driven

**Goal:** Package-defined subsystems should boot by package metadata, phase, and priority rather than manual registration scattered through code.

### TODO

- [x] Finalize initial subsystem metadata shape:
  - [x] `class`
  - [x] `phase`
  - [x] `priority`
  - [x] optional `enabledProfiles`
  - [x] optional `requiresServices`
- [x] Implement subsystem discovery from loaded package metadata.
- [x] Instantiate subsystem classes from package assemblies.
- [x] Validate subsystem classes implement `IEngineSubsystem`.
- [x] Sort subsystems by:
  1. package topological order,
  2. phase,
  3. priority,
  4. stable package/class name tie-breaker.
- [x] Make shutdown run in exact reverse initialization order.
- [x] Add phase-specific diagnostics.
- [x] Move current core/rendering package subsystems out of `OnLoad()` manual registration and into package metadata.
- [x] Decide source-generator ownership.
  - [x] If using attributes such as `[EngineSubsystem]`, generation writes package metadata consistently to `package.generated.json`.
  - [x] Avoid requiring users to manually maintain generated subsystem metadata.

### Acceptance Criteria

- Adding a subsystem to a package only requires source + package metadata/source generator output.
- No manual kernel registration is needed for normal engine packages.
- Boot logs show phase/priority/package for every subsystem.

---

## Milestone 5 — Normalize Build Tool, Launcher, And Workspace UX

**Goal:** The package-oriented model should be easy to use from the launcher and command line.

### TODO

- [x] Add `ArisenBuildTool validate --workspace <path>`.
- [x] Add `ArisenBuildTool graph --workspace <path> --profile <profile>`.
  - [x] Output readable text and optionally DOT/JSON.
- [x] Add `ArisenBuildTool generate --workspace <path> --profile <profile>` if not already formalized.
- [x] Add `ArisenBuildTool run --workspace <path> --profile <profile> --config Debug` or document why launch stays external.
  - [x] Documented launch as an external launcher/IDE/generated-host responsibility; build tool emits launch artifacts but does not own interactive process orchestration.
- [x] Improve `ArisenBuildTool test --package <id>` diagnostics.
  - [x] Log workspace/engine root, companion `.test` package, virtual test manifest contents, and missing local package guidance before generation.
- [x] Ensure the launcher uses the same validation before opening projects.
  - [x] Launcher launch path now invokes `ArisenBuildTool validate --manifest ... --profile ...` before generation, then invokes `generate` with the same selected profile.
- [x] Ensure project creation scaffolds:
  - [x] `.arisenproj`
  - [x] `manifest.json`
  - [x] `Local/com.user.project/package.json`
  - [x] starter `IPackageEntry`
  - [x] `Assets/`, `.Cache/`, `Logs/`
- [x] Ensure `.arisenproj` has no hardcoded absolute project path.

### Acceptance Criteria

- A new project can be created, generated, built, and launched without hand-editing generated files.
- Launcher and CLI report the same validation errors.
- Package graph is viewable for debugging.

---

## Milestone 6 — Package Boundary Cleanup

**Goal:** Make package dependencies reflect architecture instead of accidental direct references.

### TODO

- [x] Audit every `package.json` dependency.
  - [x] `com.arisen.taskgraph` exists and is included by ECS/rendering/packagegame where currently used.
  - [x] `com.arisen.nodecanvas` is included by `com.arisen.editor`.
  - [x] Rendering pipeline packages explicitly depend on rendering/core/native contracts they use.
- [ ] Move shared contracts into kernel or dedicated contract packages where appropriate.
- [ ] Avoid domain packages referencing concrete implementation packages unless intended.
  - [ ] Example: rendering should depend on RHI contracts, not a concrete Vulkan package.
- [x] Define clear package tiers in `PackageRegistry.md` and enforce no reverse dependencies.
- [x] Consider adding package-layer validation:
  - [x] Foundation cannot depend on Domain/Tooling/User.
  - [x] Domain cannot depend on Tooling/User.
  - [x] Tooling can depend on Domain/Foundation/Tooling.
  - [x] User can depend on public engine package layers.

### Acceptance Criteria

- Package graph matches documented layers.
- Swapping RHI package is possible in the manifest without changing rendering/editor code.
- Editor-only packages are excluded from Production profile.

---

## Milestone 7 — Native Package Runtime Model

**Goal:** Native and hybrid packages should be first-class package citizens.

### TODO

- [ ] Finalize native package metadata.
  - [x] Native library names per platform/configuration.
  - [x] Runtime DLL copy rules.
  - [ ] Native initialization/shutdown entry points if needed.
- [x] Ensure build tool deploys native runtime payloads into `.arisen/bin/{profile}/{configuration}/`.
- [ ] Ensure `com.arisen.core.native` exposes foundation services through managed contracts if needed.
- [x] Ensure `com.arisen.rhi.vulkan.native` registers RHI services through managed bridge code.
- [ ] Add native package validation:
  - [x] Missing DLL fails.
  - [x] Missing export fails if declared.
  - [x] Platform mismatch fails.
- [ ] Decide how native test packages run inside the package test workflow.

### Acceptance Criteria

- Native packages are discoverable, buildable, deployed, and loadable through the same package graph.
- Vulkan RHI can be selected by manifest/profile and validated before rendering starts.

---

## Milestone 8 — Testing And CI-Friendly Validation

**Goal:** Package behavior should be testable without relying only on full manual engine launches.

### TODO

- [ ] Add small validation fixtures/workspaces.
  - [ ] Valid minimal workspace.
  - [ ] Missing dependency workspace.
  - [ ] Cycle workspace.
  - [ ] Missing service workspace.
  - [ ] Duplicate package ID workspace.
- [ ] Add package graph tests around `ArisenBuildTool` resolution.
- [ ] Add runtime boot smoke tests with a minimal managed package.
- [ ] Add package unload order test.
- [ ] Add service contract validation tests.
- [ ] Keep package-level test workflow through `build_workspace.bat --package <id>`.
- [ ] Later, consider standard `dotnet test` for pure build tool/kernel unit tests if desired.

### Acceptance Criteria

- Core package resolution rules can be tested quickly.
- Regressions in manifest parsing, sorting, or service validation are caught early.

---

## Milestone 9 — Editor Package Manager Integration

**Goal:** The editor should become the visual control surface for the package-oriented engine.

### TODO

- [ ] Show selected workspace packages and profile packages.
- [ ] Show dependency graph.
- [ ] Show missing/cyclic/invalid package errors.
- [ ] Show service provides/requires health.
- [ ] Allow enabling/disabling packages per profile.
- [ ] Allow adding local packages through templates.
- [ ] Allow adding registry/cache packages once registry support exists.
- [ ] Add profile selector: Development / Production / testing profiles.
- [ ] Add regenerate project files action.
- [ ] Add launch action using selected profile/configuration.

### Acceptance Criteria

- A developer can inspect and fix package graph issues from the editor/launcher UI.
- Package manager uses the same validation engine as CLI.

---

## Milestone 10 — Package Registry And Distribution

**Goal:** Move beyond local-only packages toward reusable engine/game package distribution.

### TODO

- [ ] Define package registry source format.
- [ ] Define `.Cache/` package layout.
- [ ] Implement package acquisition before build/boot.
- [ ] Add version resolution policy.
  - [ ] Exact versions first.
  - [ ] Semantic ranges later if needed.
- [ ] Add package lock/resolved file for reproducibility.
- [ ] Add package integrity metadata.
  - [ ] Hashes.
  - [ ] source URL.
  - [ ] timestamp.
- [ ] Decide how local package overrides work.
- [ ] Add packaging command for publishing a package archive.

### Acceptance Criteria

- A workspace can depend on a cached/registry package, not only `file://Local/...` packages.
- Package resolution remains reproducible.

---

## Recommended Immediate Sprint

If we want the fastest path toward a solid package-oriented engine, implement in this order:

1. **Strict `ArisenBuildTool validate`** for workspace/package graph.
2. **Hard error on dependency cycles/missing dependencies** instead of warnings.
3. **Make resolved manifests authoritative** and generated for every profile.
4. **Refactor runtime package loading so `PackageSubsystem` is single owner.**
5. **Add service contract validation** for `services.provides` / `services.requires`.
6. **Make subsystems metadata-driven** from package manifests.
7. **Add minimal test workspaces** for graph validation and runtime boot.

This sprint should happen before adding more large engine features. It will make ECS, rendering, editor, resources, and native RHI packages easier to evolve safely.

---

## Suggested First Implementation Task

Start with:

> Implement `ArisenBuildTool validate --workspace <path> --profile <profile>` and make package dependency cycles/missing dependencies fatal.

Why first:

- It is isolated from runtime behavior.
- It improves every developer workflow immediately.
- It creates the foundation for launcher validation, CI checks, package manager UI, and safer boot.
- It gives us concrete data structures/errors needed by later milestones.

Expected output should include:

- selected workspace path,
- selected profile,
- packages discovered,
- resolved topological order,
- warnings,
- fatal errors,
- exit code `0` on success and non-zero on failure.
