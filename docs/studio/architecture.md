# Studio ownership

`OpenConquerStudio.slnx` builds the standalone content-authoring platform. It does not build
the Client, Launcher or server applications. The `Runtime` assembly is a bounded content
reader/sampler library, not a general-purpose game engine.

The client repository remains the source owner of Content, Rendering, Imaging and
ConquerRendering during extraction. `dependencies/OpenConquerClient` is an exact, detached
Git submodule revision. Studio builds only those library projects; no code is copied into
a parallel parser or renderer. Change shared implementations in their owning repository,
then advance this pin with a reviewed integration test. Do not use a branch-following checkout
or an MSBuild reference into an arbitrary sibling directory.
Git records a submodule dependency as a specific commit, keeping its history separate;
see the [Git submodule contract](https://git-scm.com/docs/gitsubmodules).

This is a source dependency, not complete architectural independence from the client repository.
The pinned snapshot still contains historical Studio files; the solution never builds those
copies. The paired client extraction removes their maintained ownership there. After that change
is merged, advance the dependency to the removal commit. Shared libraries may later move to
versioned packages once publishing, native notices, compatibility and independent CI are ready.
Do not create an unrelated engine repository just to remove the submodule.

`extraction.json` records the original commit and each moved file's Git blob and SHA-256 before
standalone adaptations. Client main and the open character-rendering PR remain separate work;
the extraction branch changes ownership without rewriting their rendering mechanisms.

```mermaid
flowchart TD
    Desktop[Studio desktop] --> Authoring
    CLI[Studio command line] --> Authoring
    Desktop --> Adapter[Conquer import adapter]
    CLI --> Adapter
    Adapter --> Authoring
    Adapter --> Content[Existing Content]
    Authoring --> Runtime[Studio Runtime]
    Authoring --> Imaging[Studio Imaging]
    Content --> Imaging
    Desktop --> Compatibility[ConquerRendering]
    Compatibility --> Content
    Compatibility --> Rendering[Existing Rendering]
    Desktop --> Rendering
    Game[OpenConquerClient] --> Rendering
    Game --> Content
```

| Component | Owns |
| --- | --- |
| Runtime | Validated pixels, atlas regions, sprite clips and sampling, bounded resource codecs, complete build loading and graph validation. No editor/import/game dependencies. |
| Imaging | Authoritative existing TGA/DDS decoders and decoded-image type, shared with the client. |
| Authoring | Project identity, immutable edit transactions, history, typed dependencies, source import, revision storage, migration, deterministic compilation. |
| ConquerImport | Client-repository connections, bounded source inventory, existing artwork catalog, and image/ANI import through authoritative loaders. |
| ConquerRendering | Shared client map projection, puzzle and scenery clocks, footprint ordering, texture preparation, and drawing. |
| Desktop | Project orchestration, dialogs, selection, inspectors, playback controls, bitmap and GPU-host lifecycle. |
| Tool | Command-line access to authoring and compilation. |
| RenderCheck | Platform validation executable using generated assets, compiled runtime loading, and actual embedded framebuffer readback. |

These are internal components. No independent engine framework, plugin system, or speculative
scene hierarchy is introduced. Runtime owns compiled format decoding; the compiler validates
its output through that same runtime loader. Runtime's internal bounded filesystem/JSON
utilities are shared with Authoring through explicit assembly access.

## Editing and dependencies

A session owns its exclusive project lease and immutable pixel data. Hosts serialize operations;
`StudioProject` is single-owner and is not thread-safe. Every edit validates the complete proposed
document before replacing the current snapshot. Failed edits do not enter history.

Textures have no dependencies. An atlas references one texture; frames reference an atlas and
one of its stable sprite IDs. This typed ordering makes cycles unrepresentable in this schema.
Delete impact includes transitive dependents. Removing referenced sprites or resizing a texture
beyond atlas bounds fails before mutation. Future resource kinds must extend graph validation,
including cycle detection when their reference rules permit cycles.

Undo/redo retains at most 50 transactions and 128 MiB of unique pixel payloads across history
and the active snapshot. Metadata is bounded per project. Editing after undo drops redo history.
Saved-state feedback follows the saved snapshot; returning to that snapshot clears modified state.
History is session-local, not a persisted recovery log.

Each resource saves as an immutable, digest-addressed JSON revision under its stable asset GUID.
Pixels are immutable blobs. The root index commits all revisions with one atomic replacement.
This avoids a multi-file overwrite journal. Failed writes can leave unreferenced revisions;
opening follows only the last committed index. Schema 1 migration creates an explicit independent
copy and preserves project/resource IDs. No source project is upgraded in place.

## Desktop and GPU ownership

Avalonia initially uses the audited client's pinned version. Built-in controls provide desktop dialogs,
layout, splitters, accessibility, high-DPI support, and headless interaction testing. No docking
library or second graphics engine is needed for this slice.

`AtlasEditor` edits rectangles and pixel pivots. `AnimationEditor` edits stable frame references,
order, duration, timebase, and repeat mode. `AnimationTransport` samples elapsed time through
Runtime; frame timing does not depend on render frequency. `AssetPreview` owns source-pixel
inspection and the preview viewport. Large import/transform/save/build work runs off the UI thread.

`StudioViewport` uses `OpenGlControlBase` and the existing `OpenGLGraphicsDevice`/`OpenGLRenderer`.
The renderer now accepts a caller-owned presentation framebuffer. Its existing no-argument
`EndFrame()` retains framebuffer-zero behavior for the client. Nonzero destinations are checked
for completeness and single sampling on every presentation; Avalonia can replace attachments.

Desktop OpenGL profiles are selected explicitly: WGL on Windows, GLX on Linux, and OpenGL on
macOS. Other compositor backends cannot be assumed to provide the required context sharing.
Source-pixel inspection remains available when a compatible GPU context cannot be created;
the viewport reports that failure instead of claiming a software image is engine rendering.

Only init/render/deinit callbacks issue GL calls. CPU frame snapshots survive context loss;
GPU names are discarded without issuing deletion calls on a lost context. Detachment releases
textures, renderer, and device while the context is current. Textures upload lazily once per
selected preview snapshot, not every animation tick. Resize and display scaling affect the
host framebuffer; the editor's 512 × 512 logical preview uses the shared presentation rules.

RenderCheck validates generated compiled assets through real framebuffer readback. Headless
control tests cover authoring interactions and source pixels; they do not prove GPU output.
Native dialogs, accessibility, display scaling, and driver-specific behavior require platform
manual validation. Automated GPU coverage is listed separately in the validation record.

## Compatibility and release boundaries

Shared image algorithms remain unchanged. Their original namespace and `RgbaImage` type
forwarder preserve client compatibility. The shared renderer extension requires the complete
client regression suite in addition to Studio checks. Client gameplay, networking, and server contracts stay outside Studio. World rendering now has
shared ownership through ConquerRendering; main's animated-scenery and footprint ordering fixes
from PRs #204/#205 were retained during extraction.

Integrity hashes do not establish publisher authenticity. Existing launcher trust, signed release
metadata, and Product Tool packaging remain the release path. Client resource bindings,
multi-game release identity, protected signing, publication, maps, and characters need later
coordinated slices. Imported image associations do not automatically replace client resources.
