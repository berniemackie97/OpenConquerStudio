# Studio ownership

`OpenConquerStudio.slnx` builds the editor, authoring libraries, content runtime,
tooling and rendering dependencies that currently form the Studio/Engine foundation.
Studio/Engine is one product, with OpenConquer as its first complete target. The
solution does not build the Client, Launcher or server applications. The current
`Runtime` assembly is a bounded content reader/sampler, not yet a scene execution
runtime; that integration is a [future acceptance gate](../product/roadmap.md).

Studio includes source snapshots of the client's existing Content and Rendering projects under
`dependencies/OpenConquerClient`. These are regular tracked files, not a client submodule or a
reference into a neighboring checkout. `source.json` records the source base commit and SHA-256
of each included file. The current snapshot includes the local consolidation of the image and
world-rendering code. Updates are explicit copies reviewed and tested in Studio; fixes do not
propagate automatically between repositories. No new shared assemblies or package pipeline are
required for this arrangement.

`Content/Images` owns TGA/DDS decoding and decoded pixels. `Rendering/World` owns map projection,
puzzle clocks, scenery ordering, texture preparation and world drawing. Rendering references
Content. Studio Authoring uses Content for image import; the desktop uses Rendering for previews.
Neither Studio nor those libraries references the game applications, networking or server.

The original extraction provenance remains in `extraction.json`. Actual client integration data
is a separate test fixture, selected through `OPENCONQUER_CLIENT_ROOT` or `.fixtures/client`.
CI downloads an exact published client revision containing the original content and repository
markers; those game sources are never built by Studio.

| Component | Owns |
| --- | --- |
| Runtime | Validated pixels, atlas regions, sprite clips and sampling, bounded resource codecs, build loading and graph validation. No editor/import/game dependencies. |
| Content | Existing source content readers, image decoding and included artwork. |
| Rendering | Existing OpenGL resources, sprites, text, presentation and world drawing. |
| Authoring | Project identity, immutable edit transactions, history, typed dependencies, source import, revision storage, migration and deterministic compilation. |
| ConquerImport | Client connections, bounded source inventory, artwork catalog and image/ANI import through Content. |
| Desktop | Project orchestration, dialogs, selection, inspectors, playback controls, bitmap and GPU-host lifecycle. |
| Tool | Command-line access to authoring and compilation. |
| RenderCheck | Real client image/map import, compilation and embedded OpenGL framebuffer validation. |

Runtime owns compiled format decoding; the compiler validates its output through that same loader.
Runtime's bounded filesystem/JSON utilities are shared with Authoring through explicit assembly access.

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

RenderCheck validates actual imported client images and source maps through real framebuffer readback. Headless
control tests cover authoring interactions and source pixels; they do not prove GPU output.
Native dialogs, accessibility, display scaling, and driver-specific behavior require platform
manual validation. Automated GPU coverage is listed separately in the validation record.

## Compatibility and release boundaries

Image algorithms and world behavior remain in their existing namespaces. The image type is
again defined directly in Content; no forwarding assembly is needed. Ownership changes require
the client regression suite and Studio actual-asset checks. Studio extends the recorded source copies with explicit part bindings and per-part motion paths.
Body/hair preview follows the current native contracts; the complete role state machine remains outside Studio.
Client gameplay, networking and server contracts remain outside Studio.
Integrity hashes do not establish publisher authenticity. Existing launcher trust, signed release
metadata, and Product Tool packaging remain the release path. Client resource bindings,
multi-game release identity, protected signing, publication, maps, and characters need later
coordinated slices. Imported image associations do not automatically replace client resources.
