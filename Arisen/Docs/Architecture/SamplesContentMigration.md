# Samples Content Migration

Status: **in progress**. This document exists so the migration can be resumed from the repository
rather than from a conversation. Delete it when the migration lands.

## Goal

Move the demo content out of `com.arisen.packagegame` into `com.arisen.samples` so the product package
carries the game and the gallery package carries the capability fixtures (D7). The product profiles
mount the gallery package until the game has world content of its own.

## Why this is not a file move

Generated child identity is `SHA-256(source GUID, owning package id, child kind, child key)`
(`GeneratedAssetIdentity.CreateChildGuid`). Changing the owning package therefore re-keys every
generated child, and the runtime loaders hard-fail on drift instead of tolerating it:

- `TerrainSourceAssetLoaders` recomputes each tile GUID from the asset's current owning package and
  rejects a persisted descriptor that disagrees.
- `VegetationScatterRecipeSourceAssetLoader` rejects a recipe whose `Biome.PackageId` does not match
  the recipe's own package, "so generated children retain package ownership".
- `GltfModelImportEmitter.WriteMetadata` only validates the provenance triple and then rewrites, so a
  stale `.meta` is replaced silently on the next import; the references that still name the old GUID
  are what fail.

**The world and scene identities are derived too.** The generators derive the terrain root, world,
persistent scene, cell scene, recipe, and tile GUIDs from the package id, so a re-key changes the
workspace's `StartupWorld.Guid` and `StartupScene.Guid`, not only their `PackageId`:

| Asset | Before (`com.arisen.packagegame`) | After (`com.arisen.samples`) |
|---|---|---|
| World | `38637cf8-f4a1-91f0-90b5-bf63752b4b82` | `b50c4c80-0720-4b63-b89b-af2203063180` |
| Persistent scene | `e176b519-9d88-527e-a841-c5c5d73b7477` | `6c761a9d-2d06-562e-9779-7748e8afe4d3` |
| Terrain root | `40ad1b8e-7be7-7f2c-2454-fb97874ab169` | `275cb2a7-ddce-24ac-6640-edd5aea26427` |

## Producers, and what owns which bytes

Of the 603 files that carry the old owner string: 589 are written by a producer, 12 are authored, and
2 are the package's own JSON. Only the 12 need a deliberate rewrite.

| Content | Producer | Re-key entry point |
|---|---|---|
| Terrain, world, cell scenes, scatter recipes, tile stubs | `generate_mistfall_valley.ps1`, `generate_showcase_valley.ps1` | `-PackageId`, `-PackageRoot` |
| Vegetation pages under `Vegetation/**/Generated` | the scatter baker | cook (`--arisen-cook-runtime-assets`) |
| glTF model children under `Generated/**` | `GltfModelImportEmitter` | `--arisen-reimport-models` |
| Authored references (`*.arivegetationspecies`, `*.arienvironment`, `*.ariterrainlayers`, `*.arivegetationbiome`, 4 `.meta`) | nobody | manual rewrite |

Both generators were verified to be byte-faithful reproducers: running them with their defaults
reproduces every committed file exactly, which is what makes the re-key mechanical.

## Ordering constraint

`generate_mistfall_valley.ps1` writes the scatter recipes **before** the cell scenes. The other order
deadlocks an owner change: cell scenes bind baked cluster identities that only a cook can publish,
the cook rejects recipes whose owner is stale, and the recipes are only rewritten by the generator
run that the cell scenes abort. Recipes are derived from the script's own identity and do not need
the report, so writing them first lets cook-then-rerun converge.

## Diagnostics that had to be repaired first

An owner change is the one time these paths run against a stale report, and three of them failed
uselessly:

- `Read-VegetationClusterReport` skipped entries whose `worldGuid` did not match and returned an
  empty table, so cell scenes were authored with no clusters and the script reported success. It now
  fails with the entry count and tells the caller to cook first.
- `New-CellSceneText` dereferenced that null as `$clusterRecords.Count`, which surfaced as a
  `Set-StrictMode` error instead of the intended staleness message. The call site coerces to an array.
- The recipes were written after the cell scenes, which is the deadlock above.

## State (as of this document)

Done: content moved (1309 files) into `Local\com.arisen.samples\Assets`; both generators parameterized
and re-run under the new owner for terrain, world, cell scenes, and recipes; `manifest.json` startup
package ids switched to `com.arisen.samples`; `com.arisen.samples` mounted in the base package list;
a headless `--arisen-reimport-models` command added to core and implemented for models in rendering;
`GltfModelImportEmitter` no longer writes a UTF-8 byte order mark.

Remaining:

1. Point `manifest.json` at the new world and persistent-scene GUIDs from the table above.
2. Cook the runtime assets, then re-run `generate_mistfall_valley.ps1` so the cell scenes bind the
   baked clusters of the new world.
3. Run `--arisen-reimport-models` to re-key the Lantern model children.
4. Rewrite the authored references listed above.
5. Update the tests that pin the old package paths and generated GUIDs.
6. Run `validate_fast` and `validate_runtime`.

## Traps

- A `git add -A` in the superproject can fail on a stale `.git/index.lock` and leave the index
  unchanged, so an empty `git diff --cached` proves nothing. Cross-check with `git status`, or hash the
  file with `git hash-object --path` against `git rev-parse HEAD:<path>`.
- `LClient`-style line-ending configuration makes `git status` report asset files as modified while
  `git diff` is empty. The content is identical; do not commit a normalisation pass by accident.
- The rendering test project compiles the core asset sources from an explicit `Compile Include` list,
  so a new file under `com.arisen.core\Assets` has to be added there or the test project stops
  compiling.
