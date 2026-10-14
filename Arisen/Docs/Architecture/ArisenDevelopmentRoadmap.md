# Architecture Roadmap: Arisen Engine And Its First Game

**Status:** Active scaffold (v0). Each track is filled in through owner review. This file is the only
active roadmap.
**Supersedes:** `VegetationOutdoorWorldNextTodo.md` (deleted; its work items now live under Track A,
Track B4, and Track G).

## How To Use This Document

- This is the single active roadmap. Do not open a second one; extend this file.
- Every track owns its goal, its work items, and its acceptance gate. An item is done only when its
  gate is green from a regenerated workspace.
- Goal and priority changes are recorded in **Owner Decisions** with a date. Prose elsewhere must not
  contradict them.
- Engine capability and game content are deliberately separate lines. A track may be deferred
  without blocking a slice, and a slice may pull a track forward.

## What Arisen Is

Arisen is a package-centric microkernel game engine under construction.

- Everything is a package. A workspace `manifest.json` plus a selected profile decide the package
  graph, and the graph is mounted once at boot.
- Native layer (`com.arisen.core.native`, `com.arisen.rhi.vulkan.native`): Core.Foundation,
  Core.HAL, Core.RHI (Vulkan), Core.ShaderCompiler, Core.Diagnostic.
- Managed layer: ECS with parallel system layers, TaskGraph, RenderGraph with parallel command
  recording, resources/residency/world streaming, terrain, vegetation, platform, Avalonia editor.
- Standing rules: data-oriented hot paths, no per-instance service lookup or allocation, no
  workaround fix that hides an invariant, validation follows observable states rather than sleeps.

## What Arisen Does Not Have Yet

Recorded here because this roadmap is requirement-driven, and these gaps decide the ordering.

- Skeletal animation, skinning, animation clips: none. glTF `skins` and `animations` are explicit
  import warnings, and the mesh vertex layout carries no joints or weights.
- Physics and collision: none.
- Navigation and AI: none.
- Audio: none.
- Runtime in-game UI: none.
- Gameplay framework: none.
- Asset kinds are limited to mesh, material, texture2D, environment texture/lighting, and shader
  recipes.
- Indirect draw is an unexposed capability, submission is single-queue, and there is no Hi-Z
  occlusion, mesh shader, virtual texturing, or virtualized geometry.
- Interior lighting is unaddressed: the render stack is outdoor-oriented (sky, atmosphere, cascaded
  directional shadows).

## Two Lines, Not One

- **Engine line:** a next-generation engine - fully data-driven, parallel, and saturating the
  hardware (GPU-driven rendering, large streaming worlds, virtualized geometry and texturing,
  multi-queue submission).
- **Game line:** a boxed-garden office action game - one office building, soulslike melee with block
  and parry, GTA-like comedic escalation, dialogue-driven satire.

The engine line does not gate the game. The game described needs one building, not a large world.
Coupling the two is what produced alternating priorities in the past.

**Rule:** the game slice pulls engine capability on demand. Engine-line tracks run in parallel under
an explicit time box and never block a slice.

## Owner Decisions

| # | Decision | Date |
|---|---|---|
| D1 | The primary driver is the game slice; engine capability is pulled on demand. | 2026-09-23 |
| D2 | Slice 1 acceptance: one room, one enemy, one block and one parry, with impact audio and hitstop; 2-3 minutes playable. | 2026-09-23 |
| D3 | Physics uses Jolt behind a package boundary; solver types never leak into gameplay packages. | 2026-09-23 |
| D4 | Large-world terrain work is Track B2, after B1, and does not start before slice 1. | 2026-09-23 |
| D5 | Target platform is PC Windows with Vulkan. Hardware tier and frame-rate targets are open. | 2026-09-23 |
| D6 | Managed C# stays the orchestration layer and per-instance work moves to the GPU. Rewriting the managed layer in C++ is not planned; managed boundary cost is measured with Tracy. | 2026-09-23 |
| D7 | The sample gallery is one `Samples` profile inside the `PackageGame` workspace, with the hub as a browsing layer only. Every sample must also boot directly, and no gate drives the hub. | 2026-09-24 |
| D8 | No runtime package load or unload. A different package set is a profile, a heavy switch restarts the process, and content scale is handled by residency streaming. | 2026-09-24 |
| D9 | Package activation is a first-class state. A dormant package stays loaded with its services registered, at zero per-frame cost and zero budget. | 2026-09-24 |
| D10 | The application shell is extracted into `com.arisen.app` and products depend on it. Startup target selection is generic `--startup-*`, because one workspace holds several products. | 2026-09-24 |
| D11 | `RenderPipelineManager` is a dead seam with no callers. It is merged or deleted, leaving only `RenderSubsystem` and `Graphics`. | 2026-09-24 |
| D12 | Exclusive resources declare ownership, and competing service registrations hard-fail at startup instead of silently resolving last-wins. | 2026-09-24 |
| D13 | Asset roots are a first-class registry rather than a filename convention. Workspace and package roots are declared once and shared by editor and runtime, and path policy queries that table instead of walking parent directories for a folder named `Assets`. | 2026-09-24 |
| D14 | Every asset root carries an edit policy, resolved as: workspace declaration, then a hard read-only rule for archive or extracted-cache mounts, then a `layer` default where `user` is writable and the rest are read-only. Read never blocks, so read-only roots stay indexed, browsable, referenceable, and cooked. | 2026-09-24 |
| D15 | The workspace `Assets` root is a scratch area, not a shared library: writable, never shipped, never a startup reference, and no shipped asset may depend on it. Shared content is expressed as a package. | 2026-09-24 |
| D16 | Slice 1 is developed in the game workspace (`com.arisen.packagegame`), not as a gallery sample. Each capability the slice pulls still gets its own gated sample in `com.arisen.samples` when its track lands, per D7. | 2026-10-14 |
| D17 | The gallery's work items are S1-S6 in Track S: the catalogue and `Samples` profile, a content sample, a capability sample, per-sample gates, the hub as a browsing layer only, and isolation profiles for the samples whose subject is isolation. | 2026-10-14 |
| D18 | Jolt is vendored under `com.arisen.physics`'s own `3rdparty` directory and built as a package-owned native payload, the way `com.arisen.core.native` vendors `spdlog`, `tracy`, and `dxc`. No system physics SDK, and no network fetch during a build. | 2026-10-14 |

## Track Structure

**Layer 0 - Platform Baseline** (existing, maintained): workspace generation, package and service
discipline, Tracy and RenderDoc, validation gates, single-source documentation.

**Track A - Rendering And Geometry**

- A1 frame budget, device capability query, multi-queue submission (async compute and copy)
- A2 backend-neutral indexed-indirect contract
  - Measure first: add the Tracy zones and plots for scatter bake, cell read/setup, extraction,
    culling, LOD, batch build, upload, opaque and shadow recording, submission, and disposal; plot
    active cells/clusters/instances, candidates/visible/culled/faded instances, the LOD histogram,
    direct batches and draws, CPU and prepared bytes, descriptors, upload bytes and time, budget
    stalls, and pending disposal.
  - Establish explicit frame budgets and capture representative near/mid/far camera paths at target
    density, then measure direct-instanced command pressure and CPU recording cost. If direct
    instancing stays inside budget, record that result and keep the smaller contract instead.
  - Expose `DrawIndexedIndirect` through managed RHI and `RenderCommandList` with validated argument
    layout, alignment, resource state, draw count, stride, and capability limits, and implement
    Vulkan execution with focused native/managed contract tests that keep Vulkan types out of
    vegetation. Start from deterministic CPU-built indirect arguments and add compute culling or
    count buffers only after a second measured bottleneck.
- A3 GPU culling feeding indirect arguments and counts
- A4 Hi-Z occlusion culling
- A5 virtualized geometry and virtual texturing, if measurement demands them
- A6 temporal antialiasing, upscaling, and a post-processing stack
- A7 global illumination, light probes, interior multi-light lighting, reflections

**Track B - World And Streaming**

- B1 world-scale parameterization: derived `LoadRadius`/`MaxActiveCells`/budgets, multiple terrain
  roots, incremental cooking
- B2 next-generation terrain, the increments beyond what already exists
- B3 urban content pipeline driven by DEM and OSM data
- B4 outdoor vegetation finishing work, folded from `VegetationOutdoorWorldNextTodo.md`, deferred
  - A package-provided bounded `vegetation-streaming` smoke: deterministic near/mid/far path, a
    cell-boundary crossing, one origin rebase, wind samples, and repeated load/reload/unload cycles,
    asserting cluster and instance identities, owner cells, generations, culling/LOD/batch parity,
    bounds, and zero stale draws.
  - Memory and shutdown checks that bound ECS clusters, cooked pages, CPU placement bytes, prepared
    buffers/textures/descriptors, mesh and material sharing, and deferred disposal, and that require
    a complete task/residency/device-resource drain after package shutdown.
  - Named color/depth/shadow captures that validate nonblank vegetation coverage, an upright horizon,
    alpha/depth consistency, wind movement between selected frames, stationary determinism, LOD
    transitions, and post-rebase similarity.
  - Integrate the package/format/scatter/residency/render/editor tests into `validate_fast.bat` and
    the profile plus relocated cooked-only Production checks into `validate_runtime.bat`, then run
    the complete Debug gate with zero source fallback, zero skipped GPU checks, bounded memory, clean
    shutdown, and empty Vulkan validation logs.

**Track C - Character And Animation**

- C1 skeleton, skinning, GPU skinning
- C2 clips, blending, state machine, root motion
- C3 inverse kinematics and foot placement
- C4 animation authoring tools

**Track D - Physics**: Jolt behind a package boundary (D3), vendored into `com.arisen.physics`
(D18).

Goal: a character and an enemy occupy one room, collide with its static geometry, and answer the
hit/hurt queries block and parry are built on. Solver types never reach a gameplay package, and the
simulation is stepped in fixed increments so a replay is reproducible.

- D1 solver selection and package boundary: the package skeleton, its vendored solver, the native
  payload contract, and the managed service a gameplay package consumes.
- D2 static collision and rigid bodies: cooked collision from the authored meshes, body creation and
  destruction owned by the simulation, and deterministic stepping.
- D3 character controller and hit/hurt queries: capsule movement against static geometry plus the
  shape and ray queries combat reads, with no per-query allocation.
- D4 ragdoll and cloth, deferred.

Acceptance gate: the track's first deliverable is a sample that boots the physics package alone
(Priority Rule 5). It steps a fixed-timestep world holding a static floor and a character capsule,
produces identical state for identical inputs across runs, keeps every body and job accounted for,
and shuts down with the solver drained.

**Track E - Gameplay And AI**

- E1 gameplay framework: attributes, state machines, events
- E2 combat framework: frame data, poise, stamina, block and parry windows, input buffering,
  hitstop, lock-on
- E3 enemy AI: perception, behaviour, archetypes
- E4 navigation and encounter scheduling

**Track F - Narrative And Presentation**

- F1 runtime UI and HUD
- F2 dialogue, quests, sequences
- F3 audio
- F4 save/load, settings, localization

**Track G - Tools**: interior level editing, lighting bake, animation preview, combat data
authoring, incremental cook.

- Vegetation authoring: species and biome inspectors with strict dependency/range validation,
  density/exclusion/species-weight paint tools, and world/terrain hit testing through the existing
  `SceneView` and terrain query contracts.
- Brush work stays bounded to affected cells and pages with deterministic brush samples, an
  immutable unsaved scatter preview through the adapter, no ECS or UI mutation from worker
  callbacks, and the previous valid preview kept visible until a complete replacement is ready.
- Diagnostics: cluster/instance overlays covering bounds, owner cell, species, LOD, accepted and
  rejected candidate counts, memory, dirty/conflict state, and selection focus.
- Editing: transaction-safe undo/redo, save, external-change conflict handling, regeneration, and
  incremental cook publication, with focused editor tests and one real-host smoke.

**Track S - Sample Gallery**: see below.

**Game Track - Slices**

- Slice 1: one room, one enemy, block and parry, impact audio, hitstop.
- Slice 2: one floor, three enemy archetypes, one dialogue scene.
- Slice 3: the whole building, a boss, an ending.
- Later: city and large world.

## Priority Rules

1. The slice leads. Engine capability is pulled only when a slice is blocked by it.
2. A track enters measurement-driven work only with saved Tracy evidence and explicit thresholds.
3. Every track has its own acceptance gate, and a green gate is required before advancing.
4. Engine-line tracks (large world, GPU-driven) run in parallel, never block a slice, and carry a
   time box.
5. The first deliverable of a track is its sample, and that sample must be gated.
6. Workaround fixes are not accepted. If a root cause is unknown, keep the task open and say so.

## Track S - Sample Gallery

D7 fixes the shape - one `Samples` profile, the hub as a browsing layer - and D17 fixes these items.
Every sample boots directly, so a catalogue entry is only useful when its gate can launch it without
the hub.

- S1 the `Samples` profile, the catalogue asset, and the hub scene. A catalogue entry declares the
  sample's identity, a one-line description, its scene, an optional world, the packages it needs,
  the expected visual, and the gate and artifact that prove it.
- S2 a content sample: the same package set with a different scene or world, selected inside the
  running process through the existing runtime world-load path.
- S3 a capability sample: a different package set or a different build switch, reached through its
  own profile and a process restart.
- S4 per-sample gates: each gate launches its sample directly and asserts the catalogued expected
  visual, and no gate drives the hub.
- S5 the hub: a browsing layer over the catalogue only. It is never the only way into a sample.
- S6 isolation profiles: a small number of profiles for the samples whose subject is isolation
  itself, never one profile per capability.

**Motivation.** Capability has no catalogue today. A profile decides the package graph at boot, so a
capability can only be seen by knowing which workspace, profile, and solution to launch.

**Design.**

- Sample content lives in `com.arisen.samples`, a package inside the `PackageGame` workspace. It is
  not a separate workspace, and it never depends on the hub to launch.
- One `Samples` profile plus one hub scene is the reviewing entry point. A sample is selected inside
  the running application, and a heavy switch restarts the process (D8).
- Every sample must also boot directly through the generic `--startup-*` selection (D10). Automated
  gates never drive the hub UI.
- Sample content is referenced only by the `Samples` profile. The base package list and the Editor,
  Development, and Production profiles are untouched by it.
- A sample declares identity, a one-line description, its scene, an optional world, its required
  packages, expected visuals, and the gate scenario and artifact that prove it.
- Sample switching between content-only differences uses the existing runtime world-load path.

**Evaluation of the single-profile hub.**

- Benefit: one launch and immediate switching, which is the best experience for reviewing
  capabilities one by one.
- Benefit: it exercises the runtime world-switch path the game needs anyway, and the hub becomes the
  first consumer of Track F1.
- Cost: every capability package is loaded in every session, so cold start, memory, and subsystem
  initialization grow with the catalogue.
- Cost: package absence cannot be tested through the hub. Isolation is itself the subject of several
  existing gates, so a capability that only works because another package happens to be present
  would be masked.
- Cost: build-level switches cannot coexist, for example a profiler-compiled build or a different
  render pipeline asset.
- Cost: a broken sample takes the hub process down with it.
- Mitigation: keep a small number of isolation profiles for the samples whose subject is isolation
  itself, not one profile per capability.

## Asset Ownership And Roots

Content ownership decides which package ships and cooks an asset, so it is recorded here rather than
left to folder layout.

The migration that applies this rule to the gallery content - moving the demo fixtures out of
`com.arisen.packagegame` and into `com.arisen.samples` - has landed: the fixtures and the tests
that pin their identities live with `com.arisen.samples`, both fixture generators take the owning
package as a parameter and default to it, and the local gates are green from the committed tree.
Two things it taught are worth remembering, because either one silently produces stale content
rather than an error: a generated child re-keys when its owner changes, so an authored file that
names one (a world naming a generated cell scene, a cell scene naming a baked cluster) has to be
re-keyed by hand from the cook report; and a producer that only publishes beside its previous
output leaves an unreachable second copy behind, which is why the scatter generator now prunes
generated directories it can prove it no longer owns.

What is still open is the D7 shape: the gallery package is currently mounted by the base package
list so every profile boots the migrated content, and Track S below is what moves it behind a
`Samples` profile of its own.

- Every asset is owned by exactly one package. Generated child assets derive their GUID from the
  source GUID, the owning package id, the child kind, and the child key
  (`GeneratedAssetIdentity.CreateChildGuid`), so moving content between owners changes the identity of
  every generated child and invalidates the cooked cache.
- The workspace `Assets` root is a scratch area (D15). It may hold experiments and throwaway imports.
  It may never be a startup reference, and no shipped asset may depend on it. A cook that reaches a
  workspace-owned node fails hard instead of dropping the asset from the deployment.
- Shared content is a package, not a shared folder. When two products genuinely share content, that
  content moves into a package every relevant profile mounts, which gives it a version, a dependency
  edge, a service identity, and a residency owner.
- Roots are declared once and queried (D13). A path is legal because it belongs to a registered root,
  not because some ancestor directory happens to be named `Assets`.
- Package assets are editable only where the workspace says so (D14). Engine packages are mounted from
  separate repositories, so editing them in place dirties a submodule; the default is read-only and a
  workspace opts specific packages into authoring.

## Open Questions

- Target hardware tier and frame-rate budget (D5).
- Whether this document stays English; the rest of `Arisen\Docs\Architecture` is English.
- Content sourcing for the eventual city: procedural, DEM/OSM, or purchased assets.

## Succession Rule

When every track the current game slice needs is complete, delete the completed tracks and rebuild
this document from measured results. The git log is the history; this file is the plan.
