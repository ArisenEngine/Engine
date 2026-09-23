# Architecture Roadmap: Arisen Engine And Its First Game

**Status:** Active scaffold (v0). Each track is filled in through owner review. This file is the only
active roadmap.
**Supersedes:** `VegetationOutdoorWorldNextTodo.md`, folded into Track B4 during the B-track review.

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
| D7 | A Samples workspace provides the capability gallery: one `Samples` profile with an in-app hub as the primary path, and every sample launchable directly through `--sample <id>` so no gate depends on the hub. | proposed |

## Track Structure

**Layer 0 - Platform Baseline** (existing, maintained): workspace generation, package and service
discipline, Tracy and RenderDoc, validation gates, single-source documentation.

**Track A - Rendering And Geometry**

- A1 frame budget, device capability query, multi-queue submission (async compute and copy)
- A2 backend-neutral indexed-indirect contract
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

**Track C - Character And Animation**

- C1 skeleton, skinning, GPU skinning
- C2 clips, blending, state machine, root motion
- C3 inverse kinematics and foot placement
- C4 animation authoring tools

**Track D - Physics**

- D1 solver selection and package boundary
- D2 static collision and rigid bodies
- D3 character controller and hit/hurt queries
- D4 ragdoll and cloth, deferred

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

## Track S - Sample Gallery (proposed)

**Motivation.** Capability has no catalogue today. A profile decides the package graph at boot, so a
capability can only be seen by knowing which workspace, profile, and solution to launch.

**Design.**

- Samples live in their own workspace with its own manifest, separate from the game workspace.
- One `Samples` profile plus one hub scene is the primary entry point. A sample is selected inside
  the running application.
- Every sample must also boot directly through `--sample <id>`. Automated gates never drive the hub
  UI.
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

## Open Questions

- Target hardware tier and frame-rate budget (D5).
- Whether this document stays English; the rest of `Arisen\Docs\Architecture` is English.
- Whether slice 1 is developed inside the game workspace or as the S6 sample first.
- Content sourcing for the eventual city: procedural, DEM/OSM, or purchased assets.

## Succession Rule

When every track the current game slice needs is complete, delete the completed tracks and rebuild
this document from measured results. The git log is the history; this file is the plan.
