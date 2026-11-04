# Project and runtime formats

## Saved project

```text
project.ocstudio                         schema 2 project index
.studio.lock                             editing lease
Assets/<asset-guid-N>/<sha256>.ocasset.json
Inputs/<sha256>.rgba                     immutable source pixels
Inputs/<sha256>.ocmodel                  immutable model and original source closure
Inputs/<sha256>.octerrain                immutable authored terrain cells (6 bytes per cell)
Builds/                                  optional local build destinations
```

The index contains `schemaVersion`, project `id`, `name`, and sorted `resources` entries
(`id`, `kind`, `sha256`). Paths derive from validated GUIDs and lowercase hashes. Each resource
revision contains `schemaVersion: 1` and `asset`; the index's kind selects its strict schema.
Resource IDs remain stable when names change. Sprite and frame IDs are stable within their
own parent assets. Resource revisions are immutable; edits publish a new revision.

| Kind | Asset fields |
| --- | --- |
| `model` | `id`, `name`, `dataDigest`, `byteLength`; the digest selects a self-contained model revision |
| `texture` | `id`, `name`, `width`, `height`, `pixelDigest`, `sourceFormat`, `sourceDigest`, `flipHorizontal`, `clockwiseQuarterTurns`, optional `sourceKey` |
| `atlas` | `id`, `name`, `textureId`, ordered `sprites`: `id`, `name`, `x`, `y`, `width`, `height`, `pivotX`, `pivotY` |
| `animation` | `id`, `name`, `ticksPerSecond`, `repeat`, ordered `frames`: `id`, `atlasId`, `spriteId`, `durationTicks` |
| `terrain` | `id`, `name`, `mapDataId`, `width`, `height`, `cellDigest`; ordered `exits` with stable `id`, signed `x`/`y`, unsigned `passwayIndex` |

Coordinates use the transformed texture's top-left pixel origin. Pivots are signed pixel
offsets from a sprite's top-left corner. Flip precedes clockwise rotation. Changing texture
transforms must leave every referenced rectangle valid; region coordinates are not silently remapped.
Animation durations are positive integer ticks. Frames cover half-open intervals; a boundary
selects the following frame. Repeating clips wrap by total duration; once clips retain the
last frame. Timing is authored independently of rendering frequency. It does not claim ANI
files contain a universal playback rate.

Sources are never overwritten. Explicit texture replacement retains the resource identity,
authored transforms and dependent references. Replacing with identical source content and
metadata makes no edit and preserves redo history; invalid replacements leave the
project unchanged. `Inputs` contains top-left row-major straight-alpha RGBA8.
ANI source refresh preserves the clip, frame, atlas, sprite and texture identities and
authored timing. It accepts only the original ordered source-frame keys and frame count;
changed source structure requires a new import. Referenced image updates validate and
apply as one transaction. Reimporting identical frames preserves saved state and redo.
Source formats are `tga`, `dds`, `rgba`, or `created`. File imports hash encoded source bytes;
decoded adapters/generated images hash source pixels. Source keys are import associations,
not game resource bindings. Filesystem source paths are not persisted.

TGA import uses the existing 32-bit RLE subset: no color map/image ID, zero origin, bottom-left
orientation, descriptor `0x08`. DDS supports the existing DXT1/DXT3 subset. Unsupported mipmaps,
volume textures, compression formats, and other semantics are rejected.

### Authored terrain

A terrain uses a stable resource GUID and a separate positive, project-unique MapDataId.
Its cell payload contains row-major, top-left-origin cells: six little-endian bytes per cell,
encoding unsigned 16-bit surface ID, unsigned 16-bit raw passability flag and signed 16-bit
elevation. The payload's SHA-256 is verified on reopen; edits create immutable revisions and
participate in project undo/redo and atomic save. Source map exit markers, scenery, animation
layers, world bindings and authored terrain visualizations are not represented yet.

The current authoring budget is 262,144 cells per project, with width and height limited to
1–1,024 cells each. This is a foundation safety limit, not a target-world performance budget.
The `export-base-terrain` command emits the server tool's supported version 1 source JSON:
`formatVersion`, `mapDataId`, `width`, `height`, ordered `cells` containing `surfaceId`,
`passabilityFlag`, and `elevation`, followed by `exits: []`. It omits Studio resource
IDs, display names, absolute paths and timestamps. Export requires a saved project and a
new destination. Separate processes produce identical output bytes for the same cell data.
The source export is not a binary writer; exits cannot yet be authored.

`build-terrain` invokes an explicitly selected server tool through a bounded external process,
using one temporary source revision. The server produces and validates `.ocbt` and an empty-override
`.ocmc`, and Studio publishes their containing directory only after successful completion and
expected-output checks. The temporary source and failed output are removed. A successful build
contains `terrains/<MapDataId>.ocbt` and `collisions/<MapDataId>.ocmc`; it is not a
complete world catalog or client visual-map build. This integration requires a trusted built
server tool whose `build-authored-terrain` command supports the source contract.

Studio's existing full content `build` still rejects projects containing terrain until paired
server/client content compilation exists. The `.octerrain` payload is authoring data, **not**
the server's `.ocbt` artifact.

### Budgets and validation

- 256 resources, including at most 64 textures; 64 MiB aggregate source pixels.
- 4096 pixels per axis and 16 MiB pixels/encoded input per texture.
- 1024 sprites per atlas, 4096 sprites per project; 4096 frames per clip, 8192 per project.
- Clip timebase: 1–1,000,000 ticks/second; each duration: 1–2,147,483,647 ticks.
- Pivots: −4096 to 4096 pixels. Rectangles must fit their referenced texture.
- 1 MiB project index; 2 MiB per asset revision; 8 MiB aggregate active revision JSON.
- Names: 1–128 characters, no control characters or surrounding whitespace.

These are bounded foundation limits, not measured optimal limits for large productions.
Decoding, transforms, history, bitmaps, runtime validation, and GPU uploads consume additional
bounded memory. They are not a claim about total process residency.

Readers reject duplicate/unknown/missing JSON fields, malformed identities, unsupported versions,
invalid digests, missing/wrong-type references, inconsistent dimensions, and exceeded budgets.
Serialization uses fixed property order and GUID ordering; sprite/frame order remains authored.
No timestamps, machine paths, or random build values enter compiled output.

### Saves, recovery, and migration

An exclusive lease prevents simultaneous Studio/CLI sessions. Saves verify the last index digest,
write and flush immutable pixel/revision files, then atomically replace the flushed root index.
The index is the commit point. Failed/canceled publication leaves the previous committed graph
readable and retains modified session state. Unreferenced revisions are safe to retain; automatic
revision collection is deferred. Existing immutable revisions are verified before reuse.

Version 1 texture projects must be explicitly copied with `migrate <source> <empty-destination>`.
IDs, metadata, edit settings, and pixels survive. Source bytes are unchanged. No automatic or
in-place migration occurs. A failed migration can leave unreferenced files in its destination;
use a new empty destination for retry. Version 1 content builds should be regenerated from the
migrated project; the version 2 package loader rejects old manifests.

Existing symbolic links/reparse points in paths or ancestors are rejected. Use real local paths;
macOS temporary paths may need `/private` spelling. Storage must be user-controlled and provide
local locking/rename semantics. Checks do not defend against a hostile process with equal
filesystem privileges racing path replacement or deleting leases. Flush/rename provides atomic
visibility, not a universal power-loss recovery guarantee.

## Compiled content

Compilation requires a saved session. Output stages beside its new destination, validates through
`ContentPackage.Load`, then publishes by non-overwriting directory rename. Failure/cancellation
removes staging. Validation checks the exact file set, bytes, identities, dimensions, dependency
coverage, and the decoded runtime graph. Existing output directories are never replaced.

`content.json` schema 2 contains `projectId`, ordered `textures`, and ordered sprite `resources`.
Texture entries carry `id`, `name`, `width`, `height`, `length`, `sha256`. Sprite entries carry
`id`, `name`, `kind`, `length`, `sha256`, and sorted direct `dependencies`. Compiled paths derive
only from GUID and kind: `.octex`, `.ocatlas`, `.ocanim`. The manifest cannot inject paths.

### Texture transport

| Offset | Bytes | Meaning |
| --- | --- | --- |
| 0 | 4 | ASCII `OCTX` |
| 4 | 4 | Version 1, little-endian int32 |
| 8 | 16 | Asset GUID, big-endian GUID byte order |
| 24 | 4 | Width, little-endian int32 |
| 28 | 4 | Height, little-endian int32 |
| 32 | 4 | Pixel byte count, little-endian int32 |
| 36 | 32 | SHA-256 of pixels |
| 68 | width × height × 4 | Top-left RGBA8, straight alpha |

### Atlas and animation transport

| Offset | Bytes | Meaning |
| --- | --- | --- |
| 0 | 4 | ASCII `OCAS` |
| 4 | 4 | Version 1, little-endian int32 |
| 8 | 4 | Kind: 1 atlas, 2 animation, little-endian int32 |
| 12 | 16 | Asset GUID, big-endian GUID byte order |
| 28 | 4 | Payload byte count, little-endian int32 |
| 32 | 32 | SHA-256 of payload |
| 64 | declared count | Payload; total file limit 2 MiB |

Atlas payload: texture GUID, int32 sprite count, then ordered entries of sprite GUID,
uint16 UTF-8 name byte count, strict UTF-8 name, and six int32 values (x, y, width, height,
pivotX, pivotY). Animation payload: int32 timebase, int32 repeat (0/1), int32 frame count,
then ordered entries of frame/atlas/sprite GUIDs and int32 duration. All integers are
little-endian; all GUIDs use big-endian GUID byte order.

Decoders bound counts before allocating, require exact byte coverage, validate semantic data,
and leave caller streams open. The manifest digest binds entire files, including headers.
Hashes detect corruption; they do not authenticate publishers. These uncompressed transports
are not frozen universal game formats. Compression, color-space policy, streaming, GPU formats,
and large-world layouts need actual consumers, measurements, and compatibility review.

## C3 compatibility models

`.ocmodel` is implemented by the existing Content project. Version 2 adds explicit part-local
matrices, ancestor attachment bindings and per-part clip motion paths. Version 1 remains readable;
writing an edited decoded model upgrades its container to version 2 while preserving every source
file byte. See [C3 asset contract](c3-assets.md). The authoring sidecar is committed through the
same revision/index save transaction as textures and sprites. Undo history includes model bytes.

Model sources have a 64 MiB per-resource and 128 MiB aggregate project budget. Decoding additionally
bounds source files, tracks, vertices, matrix channels and keyframe indexes before allocation.
Limits are foundation safeguards, not production corpus-sizing benchmarks.
