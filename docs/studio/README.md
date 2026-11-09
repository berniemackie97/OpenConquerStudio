# OpenConquerStudio

Content authoring, previews and deterministic builds in a separate repository.
Supports client-content browsing, shared map previews, puzzle inspection, textures, sprite
atlases, and sprite animation. Map editing and characters remain in the [asset plan](asset-platform-plan.md).

## Run

Install the .NET SDK pinned in `global.json`. From this repository:

```sh
dotnet restore OpenConquerStudio.slnx --locked-mode
dotnet build OpenConquerStudio.slnx -c Release --no-restore
dotnet run --project src/OpenConquerStudio -c Release --no-build
```

Windows, macOS, and Linux are supported. The preview viewport requires desktop OpenGL
3.3 Core and GPU texture sharing. The **Pixels** tab inspects image data independently.

## Make or open a project

Launching Studio within a game checkout opens that client project automatically. Launching from
the standalone Studio solution starts the new/open workflow; the codec/rendering sources are not selected as your game.
A containing `project.ocstudio` takes priority. An explicit project-directory argument overrides discovery;
standalone installations outside a project retain the new/open workflow.

To work with **OpenConquerClient**, choose **Open client project** and select its repository
directory. Studio opens a persistent workspace under `.studio/authoring` and connects to
`content/retail-5517/payload`. The **Client content** tab browses actual maps, ANI catalogs,
images, and puzzle files. Select a map for the shared client-renderer preview; use the camera
tile controls to navigate. Select an ANI section or image, then **Import editable copy**.
Use **Client content folder** to attach an original installation instead. Source files remain
unchanged. Imported copies save and compile through the regular authoring workflow.
To refresh an authored ANI clip, select that animation in **Authored**, return to **Client
content**, select the matching ANI section and choose **Refresh selected authored animation**.
Refresh preserves the authored frame durations and resource IDs and requires the same ordered
source-frame identities and count. Import a new clip if the source structure has changed.

The automatically created `.studio/authoring` workspace is local and ignored by Git.
For a version-controlled game-asset project, use **New project** in your chosen content
directory, then **File → Client content folder** to attach the client sources.

1. **New project**: enter a name and choose the parent folder. Studio creates the project folder. **Open project**: choose `project.ocstudio`.
2. **Assets → Import texture**: select a supported TGA/DDS. **Client artwork** imports an editable copy from the included catalog.
   **New texture** creates a solid image without original game files.
3. Select a texture. Edit its name, flip, or rotation; click **Apply asset edits**.
   **Replace source image** preserves its identity and edit settings. Referencing atlases must still fit the result.
4. **Save** commits and persists visible field edits. **Undo** and **Redo** restore editing transactions within the current session.
5. **Build** saves, compiles and validates a new content directory under `Builds/` in the project. The Activity panel shows its path. Existing builds are retained.

Original import files stay unchanged. Apply/Rename buttons provide an explicit preview update;
Save and navigation also commit visible fields. Clearing an authored selection commits its
visible draft and clears the inspector; an invalid draft retains the selection for correction.
Undo first reverts pending fields, then restores project transactions. Closing or
switching projects offers Save, Discard and Cancel, including when a field is invalid.
When opening or creating a project, choose its destination before deciding what to do with
unsaved changes; canceling the picker leaves current edits untouched. Reconnecting client sources
clears the previous source selection and map context.
Only one Studio window or command-line operation can edit a project at a time.

To open a project at startup:

```sh
dotnet run --project src/OpenConquerStudio -c Release --no-build -- /path/to/project
```

Version 1 projects require an explicit migration copy. Close the source project first:

```sh
mkdir migrated-game
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- migrate old-game migrated-game
```

## Extract a source map's editable terrain

Open a map in **Client content** and select **Extract editable terrain grid**. This copies
the complete parsed surface/passability/elevation grid and ordered raw exit markers into
one new authored terrain resource, with a stable GUID and the map's MapDataId. The source
map remains read-only. The result opens in the same terrain-grid editor used for newly
created terrains and can be painted, undone, saved, and exported for server terrain builds.

This command **does not import a complete visual map**. Puzzle art, scenery objects, animations,
overlay records, transition destinations, and the client-rendered scene remain source-only.
The terrain record captures the source map path, referenced puzzle path, catalog/header
fields and the counts of scenery and overlay groups for diagnostics; these references are
not a lossless copy of the unresolved records. Do not delete or relocate original source
content expecting those components to be reconstructible from the terrain resource.
The editor rejects source grids above the authored 262,144-cell budget.

To refresh extracted terrain after the source map changes, select the target terrain under
**Authored**, return to the matching map under **Client content**, then choose
**Refresh selected authored terrain**. This explicitly replaces the extracted grid and raw
exit values in one project revision while preserving the terrain GUID, name, MapDataId,
and world references. Unchanged raw exit records retain their marker IDs even when their
order changes. Replaced or removed exit records do not retain their old identities.
The command rejects mismatched map-data IDs or an authored terrain that was never extracted.

The same operations are available to scripted authoring sessions once the project has a
connected content folder:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- extract-client-terrain /path/to/project 1010
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- refresh-client-terrain /path/to/project <terrain-guid> 1010
```

The source-map extraction now retains an immutable, digest-verified copy of the
**decoded map bytes** in the project under `Inputs/`. This preserves ordered exit,
scenery and overlay records without relying on the original map archive after saving.
The terrain inspector shows whether an owned map snapshot exists. The project-owned
snapshot does **not** include referenced puzzle, texture, animation, or scene files:
these are still unresolved external dependencies, not editable or compiled game assets.

To inspect the exact ordered scene references offline after Save and reopen, run:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- export-map-source-inventory /path/to/project <terrain-guid> /path/to/new-inventory.json
```

The inventory includes the ground puzzle, every parsed scenery record type and payload
digest, every positioned overlay group and its insertion metadata, and ordered puzzle,
scene, and animation references. Repeated source references remain repeated. A missing
source snapshot is rejected explicitly; the exporter does not reconstruct one from
metadata counts or paths.

Captured terrain uses resource-document schema version 3. Previously extracted terrain
(schema 2) and manually authored terrain (schema 1) remain readable; schema-2 extraction
must be refreshed from source to acquire an owned snapshot. Saving never rewrites the
original map or puzzle files. Terrain/collision builds still use the authoritative server
tool and do not include the unresolved visual dependencies.

## Author terrain cells

Choose **Assets → New terrain**, select a unique MapDataId and set dimensions and initial raw
surface/passability/elevation values. Select the terrain in **Authored**. Its inspector can
navigate to cell X/Y, edit a cell's three fields, rename the resource, and commit those edits
with **Apply terrain edits**, navigation or **Save**. In the central **Terrain grid**, click or
drag to select a rectangle; selection coordinates can also be entered directly. Adjust the
**View X/Y** controls to inspect different parts of large grids and **Cell size** to change
the view scale. Enter Surface ID, raw passability flag and signed elevation in the inspector,
then choose **Paint selected cells**. This applies one validated region edit, including any
pending terrain name or exit-marker changes, as a single undoable revision. Surface colors
identify groups of raw IDs; they are not map textures or traversal rules. Both single-cell
and region edits preserve an immutable, digest-verified terrain payload.

The same atomic rectangle operation is available in the CLI for automated project authoring:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- terrain-region /path/to/project <terrain-guid> 2 3 8 10 25 1 -5
```

The coordinates select inclusive corners. The remaining values are the raw surface ID,
passability flag and signed elevation. The command validates the entire region before
saving, with one project revision.

The Studio CLI can export saved terrain as source JSON for the server's existing version 1
`generate-base-terrain` interface:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- export-base-terrain /path/to/project <terrain-guid> /path/to/new-terrain.json
```

With the versioned server tool built, Studio can also generate the **validated base-terrain
and static-collision pair** directly:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- build-terrain /path/to/project <terrain-guid> /path/to/OpenConquer.GameData.Tool.dll /path/to/new-output-directory
```

Studio invokes only the explicitly supplied server tool. It exports a saved terrain revision
to a temporary source, runs the server's `build-authored-terrain` command, checks the required
output names, and publishes a new directory containing `terrains/<MapDataId>.ocbt` and
`collisions/<MapDataId>.ocmc`. The server tool writes and validates both artifacts using its
own production readers. Tool failures, cancellation, and invalid output do not publish a build.
The tool assembly must come from a trusted server build; Studio does not authenticate it.

Terrain inspectors can also add, inspect, edit, remove, and reorder **exit markers**.
Select a marker, then choose **Inspect selected marker** before changing its signed X/Y and
unsigned passway index. Apply edits, Save, and navigation use the same pending-draft and undo
workflow as terrain cells. Markers retain stable IDs across edits and moves. They may lie outside
the base grid; their order and raw values are preserved in the server's source JSON and
compiled terrain. They are not world portal destinations.

The generated collision has zero overrides and does not account for authored scenery.
This is not a complete client/server world build.

This remains a terrain-data editor, not a complete world scene editor. The grid shows raw
surface values and selected cells, not game scenery or puzzle layers. Portal destinations,
scenery placement, client visuals and paired client/server map builds are still unimplemented.
**Save and build** rejects projects containing terrain rather than publishing incomplete game
content. Actual desktop/GPU world validation remains a separate gate.

## Author world definitions

Create a terrain first, then choose **Assets → New world**. Enter a unique positive world
MapId, select the existing terrain resource, and enter the complete 64-bit flags value
explicitly (unsigned decimal or `0x` hexadecimal). A world MapId is not a MapDataId.
Several authored worlds may reference one terrain. The world inspector can rename a world,
change its map ID, choose a different terrain, and edit its full flags value; Apply, Save,
Undo, and Redo preserve resource identity. Deleting a referenced terrain is blocked.

The CLI has equivalent actions:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- new-world /path/to/project "New world" 1002 <terrain-guid> 0x4000000000
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- export-map-definitions /path/to/project /path/to/new-world-source.json
```

The source JSON follows the version 1 `generate-map-definitions` input contract of
`OpenConquer.GameData.Tool`. It exports ordered world entries with MapId, the linked
terrain's MapDataId, and exact 64-bit flags, without embedding display names, resource GUIDs
or guesses about historical assignments. The server tool may compile that source into its
canonical `map-definitions.json`, which its game server consumes. Studio CI verifies this
interface against a pinned server revision.

World definitions do not author transition destinations, game-state flags beyond the
supplied raw mask, scenery, or client visual maps. The ordinary full-content build remains
blocked when authored terrain is present, pending complete paired release integration.

## Author sprites and animation

1. Select a texture, then **Assets → New atlas**. The initial sprite covers the whole texture.
2. Select the atlas. Edit sprite rectangles and pixel pivots; **Apply sprite edits**.
   **Add sprite** adds another region. Remove unused regions with **Remove sprite**.
3. Select the atlas, then **Assets → New animation**. Its sprites become ordered frames.
4. Select the animation. Choose each frame's sprite and duration; **Update frame**.
   Add, remove, or reorder frames with the frame controls.
5. Set ticks per second and repeat mode; **Apply clip settings**. At 1000 ticks/second,
   100 ticks means 100 milliseconds. This is authored timing, independent of rendering rate.
6. Use **Play/Pause**, **Stop**, and the position slider in the preview viewport. Zoom adjusts the preview around the sprite pivot.
7. Save, reopen, and build. The dependency inspector shows resources used and resources affected by changes.
   Deleting a referenced resource is blocked; fix its dependents first.

## C3 models, clips and bone channels

The client connection recognizes supported base body entries when `ini/c3.wdb` is available.
Select **character** in the browser. Choose body 1–4, optionally set the native hair-style suffix,
then preview and import. Hair uses the recorded `armet`/`v_armet` link, not an inferred skeleton.
Missing catalog records fail explicitly. Optional body/headwear catalog-ID overrides select primary
armor and headwear directly. Weapons, full material/effect playback and all role variants remain incomplete.

Use **Import C3** for a standalone geometry file. A motion-only file belongs in **Add C3 motion**
on an existing model. Track counts and ordinal bindings must match; no automatic retargeting occurs.
A C3 file alone does not identify every catalog texture, so raw imports can be untextured.

The model inspector selects embedded pose or an imported clip. Edit milliseconds per frame,
repeat and the model name with **Apply model edits**. Play/pause, stop and scrub operate on the
selected clip. Choose a submesh track and bone channel to inspect its full sampled 4×4 matrix.
**Show bone channel origins** marks channel translations; it does not draw an invented joint hierarchy.
View rotation and zoom are inspection settings. Bone matrices and vertex geometry are not editable yet.

For world context, open a map in **Client content**, select an authored model, then use
**Preview on last map**. The source camera tile anchors the preview character. Drawing uses the
existing map camera, scenery ordering and geometric shadows. This placement is not a saved map edit.

Saved models keep exact original source bytes, material IDs, virtual submesh names, part-local
matrices, ancestor links, per-part motion bindings and clip timing. Reopen or load a compiled
build without the original client installation. See [C3 format](c3-assets.md) for supported fields
and remaining native-rendering boundaries.

CLI equivalents:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- import-character my-game /path/to/OpenConquerClient 2 335
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- model-clip my-game MODEL_GUID "Actor" 0 66 true
```

## Use with OpenConquerClient or another game

Keep each game's Studio project in its own content directory. Builds contain `.octex`,
`.ocatlas`, `.ocanim`, `.ocmodel`, and `content.json`. Do not load the editable project from a shipping game.

A .NET game references `src/OpenConquerStudio.Runtime/OpenConquerStudio.Runtime.csproj`.
Use `ContentPackage.Load(buildDirectory)` to validate and load the complete resource graph.
`package.Sprites.Sample(animationId, elapsedTicks)` returns the texture and sprite rectangle
at the clip's authored tick position. Upload texture pixels once using the existing
`OpenGLGraphicsDevice`, then draw through `OpenGLRenderer` with the returned rectangle and pivot.
Runtime loading has no Studio GUI or Conquer importer dependency.

Studio uses a recorded copy of the client image decoding and rendering code. It **does not yet
redirect its game resources to compiled Studio assets**. Importing artwork does not change
client layouts, states, maps, or installed files. Client adoption needs explicit resource bindings
and the existing authenticated content-release pipeline. Another game can consume the runtime
resources directly without any Conquer assets.

## Automation and checks

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- build my-game compiled-content
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- verify compiled-content
# Set OPENCONQUER_CLIENT_ROOT to your actual client checkout before integration checks.
dotnet test OpenConquerStudio.slnx -c Release --no-build --no-restore
python3 -m unittest discover -s tests/development -p test_studio_cli.py
```

Set `OPENCONQUER_CLIENT_ROOT` to a real client checkout with `content/retail-5517` before integration
tests. CI downloads an exact client fixture revision into `.fixtures/client`; no client project is built.
RenderCheck also accepts the client directory as its sole argument.

Run the tool without arguments for all commands. Close the project in Studio before editing
it from the tool. Build destinations must be new; in-project builds belong under `Builds/`.
Hashes check integrity, not publisher authenticity. Signing and publishing remain future work.

Details: [formats and safety](formats.md), [architecture](architecture.md), [audit](audit.md).

The Studio CI matrix builds, tests, and publishes on Windows, macOS, and Linux. Actual image/map/C3 OpenGL gates run locally with a supplied client content set. Source-only CI reports missing integration assets as skipped. Hosted macOS
provides no GPU interop; run `dotnet run --project tools/OpenConquerStudio.RenderCheck -c Release`
on a graphics-capable Mac for that separate hardware gate. A skipped hardware gate is not a
claim of GPU validation on the hosted runner.
