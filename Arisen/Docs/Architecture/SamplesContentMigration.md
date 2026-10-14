# Samples Content Migration

Status: **functionally complete, uncommitted**. Both local gates pass with the content owned by
`com.arisen.samples`: `validate_fast` (all four profile manifests, the build tool, the kernel, the
launcher, and the rendering contracts) and `validate_runtime` for `Editor`, `Development`,
`Production` (including the copied/relocated Production closure), and `RHIVulkanTesting`. What is
left is packaging the change, not migrating it. Delete this document once the commits land.

## Goal

Move the demo content out of `com.arisen.packagegame` into `com.arisen.samples` so the product package
carries the game and the gallery package carries the capability fixtures (D7). `com.arisen.packagegame`
now holds only its entry assembly, its subsystems, and its package metadata; the whole `Assets` tree
lives in `com.arisen.samples`, which the workspace mounts in its base package list.

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

`manifest.json` carries the new identities and the new owning package; `StartupWorld.Guid` names the
regional world and `StartupScene.Guid` names its persistent scene.

## Producers, and what owns which bytes

Of the files that carried the old owner string, most are written by a producer and only a handful are
authored. The authored ones are what need a deliberate rewrite.

| Content | Producer | Re-key entry point |
|---|---|---|
| Terrain, world, cell scenes, scatter recipes, tile stubs | `generate_mistfall_valley.ps1`, `generate_showcase_valley.ps1` | `-PackageId`, `-PackageRoot` |
| Vegetation pages under `Vegetation/**/Generated` | the scatter baker | cook (`--arisen-cook-runtime-assets`) |
| glTF model children under `Generated/**` | `GltfModelImportEmitter` | `--arisen-reimport-models` |
| Authored references (`*.arivegetationspecies`, `*.arivegetationscatter`, `*.arienvironment`, `*.ariterrainlayers`, `*.arivegetationbiome`, `LanternWorld.arisenworld`, `TeapotWestCell`/`SmokeScene`/`LanternShowcaseScene`, four `.meta`) | nobody | manual rewrite |

Both generators were verified to be byte-faithful reproducers, and both now default to
`com.arisen.samples`, which is the package their fixtures are committed under. `PackageId`,
`-PackageRoot` and the new defaults are the only way the fixture identity is spelled.

## Hand-authored references to derived identities

Two authored fixture files reference generated children, so an owner change re-keys the reference,
not just the `PackageId` beside it:

- `TeapotCenterCell.arisenscene` binds four baked clusters. `generate_showcase_valley.ps1` rewrites
  only the terrain-tile entities in that scene and preserves every other entity verbatim, so the
  vegetation-cluster section is authored, not produced. The cluster GUID, the biome/species package
  ids, and the bounds/instance counts were re-synced from the cook report
  (`.arisen/Cache/vegetation-clusters.json`). The owning-cell GUID is package-independent
  (`WorldCellIdentity.Create` hashes the world GUID, cell coordinate, and layer only), so it did not
  change.
- `LanternWorld.arisenworld` is hand-authored, unlike the generator-written
  `MistfallValley.arisenworld`, and its cell `(1, 0, 0)` names the generated Lantern scene. That
  reference was re-keyed to the scene's new identity. Nothing produces this file, so an owner change
  can only be repaired here.

Neither case is visible to a sweep for the old owner string, because the stale part is a GUID with no
package name beside it. The audit that finds them is: recompute each generated child's previous GUID
by deriving it again with the old package id, then sweep every authored file for those values.

## Unreachable generated output

A recipe publishes into `<recipe folder>/Generated/<recipe GUID>`, and the recipe GUID is derived from
the owning package id. Re-keying therefore published a new directory beside the previous one, and the
previous directory stayed behind as an unreachable second copy of every page that had ever been baked
(768 files under `Vegetation/MistfallValley/Generated`).

`VegetationScatterRecipeGenerator.PruneUnreachableGeneratedDirectories` now states the matching
invariant - a recipe folder's `Generated` directory holds only generated sources owned by that
package - and removes, after a successful publish, each sibling directory whose every generated source
records a different owner, refreshing the source index for the removed directory. A directory with no
generated provenance and a directory that still holds one of this package's sources are both left
alone. `VegetationScatterRecipeCookingTests.RuntimeCook_RemovesGeneratedSourcesOwnedByAnotherPackage`
covers the removal, the surviving same-owner directory, and the untouched unproven directory.

The 48 orphan directories in the repository were also deleted from the working tree with the same
metadata-driven sweep, so the tracked fixture set is exactly the 1309 files the migration moved.

## Ordering constraint

`generate_mistfall_valley.ps1` writes the scatter recipes **before** the cell scenes. The other order
deadlocks an owner change: cell scenes bind baked cluster identities that only a cook can publish,
the cook rejects recipes whose owner is stale, and the recipes are only rewritten by the generator
run that the cell scenes abort. Recipes are derived from the script's own identity and do not need
the report, so writing them first lets cook-then-rerun converge.

## Diagnostics that had to be repaired first

An owner change is the one time these paths run against a stale report, and the failure modes had to
be usable:

- `Read-VegetationClusterReport` skipped entries whose `worldGuid` did not match and returned an
  empty table, so cell scenes were authored with no clusters and the script reported success. It now
  fails with the entry count and tells the caller to cook first.
- `New-CellSceneText` dereferenced that null as `$clusterRecords.Count`, which surfaced as a
  `Set-StrictMode` error instead of the intended staleness message. The call site coerces to an array.
- The recipes were written after the cell scenes, which is the deadlock above.

## Residual packaging steps

1. Commit and push `Local/com.arisen.samples` (its `package.json`, `README`, and the whole `Assets`
   tree - 1309 files) so the content has a revision to mount.
2. Commit and push the sweep in `Local/com.arisen.vegetation`, then bump both submodule pointers in
   the workspace.
3. Commit the workspace changes: `manifest.json`, the fixture generators and
   `validate_relocated_production.ps1` under `Arisen/Scripts/Windows`, the rendering contract tests,
   and the architecture notes that name the fixture owner.
4. Re-run `validate_fast` and `validate_runtime` from the committed tree.

## Traps

- A `git add -A` in the superproject can fail on a stale `.git/index.lock` and leave the index
  unchanged, so an empty `git diff --cached` proves nothing. Cross-check with `git status`, or hash the
  file with `git hash-object --path` against `git rev-parse HEAD:<path>`.
- `LClient`-style line-ending configuration makes `git status` report asset files as modified while
  `git diff` is empty. The content is identical; do not commit a normalisation pass by accident.
  `TeapotCenterCell.arisenscene` and `SmokeScene.arisenscene` are LF while `TeapotWestCell.arisenscene`
  and `LanternShowcaseScene.arisenscene` are CRLF; edit them without rewriting the line endings.
- The rendering test project compiles the core asset sources from an explicit `Compile Include` list,
  so a new file under `com.arisen.core\Assets` has to be added there or the test project stops
  compiling. The core `Automation`/`Memory`, `dag`, `taskgraph`, `vegetation`, and `terrain` sources
  are globbed by contrast; `generic-renderpipeline` is listed one file at a time.
- The submodule working tree is what a cook reads. `Local/com.arisen.samples` has an untracked
  `Assets` tree until step 1 of the residual packaging steps, so a fresh clone has no fixture content
  to mount.
