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

## Author terrain cells

Choose **Assets → New terrain**, select a unique MapDataId and set dimensions and initial raw
surface/passability/elevation values. Select the terrain in **Authored**. Its inspector can
navigate to cell X/Y, edit a cell's three fields, rename the resource, and commit those edits
with **Apply terrain edits**, navigation or **Save**. Undo and Redo apply to these changes.
The project stores terrain cells in an immutable, digest-verified payload.

The Studio CLI can export saved terrain as source JSON for the server's existing version 1
`generate-base-terrain` interface:

```sh
dotnet run --project tools/OpenConquerStudio.Tool -c Release --no-build -- export-base-terrain /path/to/project <terrain-guid> /path/to/new-terrain.json
```

This source can be passed to the server-owned
`OpenConquer.GameData.Tool generate-base-terrain <source.json> <terrain.ocbt>` command.
The export contains ordered cells and an empty exit list because exit authoring is not yet
available. It is not a complete client/server world build.

This is the first terrain-authoring foundation, not a finished world editor. It does not yet
display painted terrain in the viewport, author exits or scenery, or compile paired client/server
map outputs. **Save and build** rejects projects containing terrain rather than publishing an
incomplete game build. Actual desktop/GPU world validation remains a separate gate.

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
