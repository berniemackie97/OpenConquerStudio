# Asset platform target and delivery plan

OpenConquer Studio/Engine is one integrated game-development product with a separate repository and desktop application. Its target workflow spans asset/scene authoring, deterministic compilation, reusable runtime consumption and deployment. OpenConquer is the first complete game target; broader support is not yet implemented. The current application provides a content-authoring foundation, not a complete standalone game runtime.

Game execution loops, gameplay, networking and server authority remain in their respective projects. Shared codecs and rendering mechanisms belong to their current owners; the desktop is not a runtime dependency. See the [product roadmap](../product/roadmap.md) for acceptance gates.

The executable foundation
currently implements texture, atlas, and sprite-animation projects. The asset families, editors, and production gates
below define subsequent implementation work; their presence in this plan does not make
them supported by the current application.

OpenConquerStudio owns its authoring documents, content build orchestration, resource codecs,
import adapters and workspace. OpenConquerClient is the first target. A target adapter defines
which resource types, capabilities and output versions its game accepts. Do not add a speculative
plugin framework before there is a second implemented target.

## Evidence and current capability

The extraction baseline is client commit `78d752c2da39ca8f9f45f64377844efacce42d17`
and server commit `a16cae6a2942dc2883131645b766b89e73ae145e`. The client now shares puzzle,
terrain, animated-scenery and footprint-ordering code in `OpenConquer.Rendering/World`, and has bounded
C3 geometry/motion decoding, matrix sampling/skinning and indexed OpenGL mesh submission.
PR #213 has since merged initial body composition, world depth and geometric shadows. The
current source snapshot includes it; multipart characters and action-driven animation remain incomplete.
Studio still previews source maps, not authored maps or compiled `.ocmap` graphs. Scrolling
puzzle playback and positioned animated overlay coverage remain explicit gaps.

The native evidence corpus has much more mesh, motion, attachment, and draw-order information
than the current Studio implementation. Its historical managed implementation references
belong to the separate reconstruction workspace. They must be audited before extraction;
they are not existing Studio features.

Existing sources to reuse or deliberately extract:

| Concern | Current owner and evidence |
| --- | --- |
| DMap grids, scenery records, puzzle resources, ANI sections, WDF access, terrain scenes | `OpenConquer.Content/Map`, `Ani`, and `Wdf`; the existing content tool's closure/import validation. |
| Camera, puzzle clocks, scenery/terrain ordering, map texture preparation | The existing `OpenConquer.Rendering` project, under its `World` folder; Studio records a source snapshot of this project. |
| OpenGL resources, sprites, masks, text, logical targets | `OpenConquer.Rendering`; extend its host contract and verify context ownership rather than forking it. |
| Mesh geometry, matrix motion, character composition, resource lookup, world ordering | Dedicated `20_notes/Rendering` and `20_notes/Game_Map` contracts; fresh Binary Ninja verification for any missing or conflicting behavior. |
| Compiled client maps | PR #196 is merged. `.ocmap` v1 remains a candidate: its compiler rejects exit markers and unsupported records, dependent resources are not fully compiled, and the game has not switched to it. |
| Server terrain and collision | Existing `.ocbt`/`.ocmc` readers, writers, fingerprinting, composition and build tools. Studio must use those owners rather than copying a second implementation. |
| Release verification, installation, repair, rollback | Existing launcher and Product Tool contracts. |

The 2026-11-02 live Binary Ninja check reconfirmed full-matrix motion interpolation and
clarified signed frame comparisons and required import validation. The raw captures and
new findings are maintained in the native workspace's
`20_notes/Rendering/c3_core_dll/13_motion_sampling_and_blend.md` and its linked evidence.
There is no fresh claim of complete character or mesh parity from that focused check.

## Saved assets

Authoring data and compiled runtime data have different responsibilities.
The map-specific contract and current evidence are in [Map asset contracts](map-content-contract.md).
That contract takes precedence over historical statements that PR #196 is unmerged or that
Studio must be the game engine. No binary schema is frozen by this plan.

The version-2 project schema uses readable, versioned asset documents, immutable pixel
payloads, and a project asset index. The implemented layout is documented in [formats](formats.md).
The broader target layout below extends that model; version-1 texture projects migrate by explicit copy. The following layout is the target, not the current layout:

```text
project.ocstudio                      project identity, roots, profiles, schema version
Assets/<author-organized-path>/
  <name>.ocasset.json                  editable typed document with stable asset GUID
Payloads/<content-digest>              lossless image, geometry, motion, or grid payload
Sources/<import-id>/                  owned originals, or a digest-pinned acquisition recipe
ImportMetadata/<asset-guid>.json      source associations, importer version, settings, reimport map
.studio/                             local indexes, cache, journals, recovery state
Builds/<target>/<build-revision>/     immutable compiled output and manifests
.user/                               selection, windows, workspace state; not game content
```

Asset GUIDs and stable subresource IDs carry identity. Paths and display names organize
files. Renames and moves do not rewrite every reference. Reimport retains identity through
an explicit source-to-subresource mapping and reports removed resources before applying
changes. A new source file can replace content while keeping the authored resource identity.

Every typed asset document carries a schema version, asset GUID, type, display name,
typed references, and type-specific authored data. The JSON is readable and deterministic;
large numeric arrays and pixel buffers live in lossless payloads. Each payload declares
its encoding, count, dimensions/layout, units, and digest. Hashes locate content revisions;
they do not replace stable asset identities or authenticate a publisher.

The current sprite-animation revision is concrete:

```json
{
  "schemaVersion": 1,
  "asset": {
    "id": "e2356225-f267-43b9-878d-ab42036c6ab4",
    "name": "Lantern idle",
    "ticksPerSecond": 1000,
    "repeat": true,
    "frames": [
      {
        "id": "37929468-44cd-4bac-857d-302a25a35a5e",
        "atlasId": "90d57283-f8cf-4799-8ee8-82710e304914",
        "spriteId": "5d7be9d5-a3fe-4a81-8f05-c0b2c597b8a5",
        "durationTicks": 100
      }
    ]
  }
}
```

The project index supplies resource kind and revision digest. Referenced atlas/sprite IDs must
resolve in that project. Future scene, character, and other schemas need a validator, migration,
serializer, editor, compiler, runtime reader, and conformance tests together.

Save transactions must cover related asset edits, their index, and new payload references.
The implemented immutable revision store uses one atomic root-index replacement, so related
resource changes share one commit point without overwriting previous revisions. Any later
mutable multi-file storage must add an explicit recovery protocol. Crash testing must verify
that opening observes either the previous complete graph or the next complete graph. Multi-resource undo/redo uses the same domain edit
transactions. Automatic recovery snapshots are separate from an explicit user save.

Project files, owned sources or reproducible acquisition recipes, import metadata, and authored
payloads are versioned team data. Local cache, user workspace state, and build products are
disposable. The existing `.studio/authoring` auto-workspace is a local scratch workflow; moving
the default to a versioned project needs a migration that preserves existing authored work.
Deleting a referenced asset requires an explicit impact report; runtime and import identifiers
must never be silently reassigned to an unrelated replacement.

## Resource families and editor requirements

| Asset family | Saved semantic data | Required authoring/preview behavior |
| --- | --- | --- |
| Texture | Source/pixel payload, color-space declaration, alpha interpretation, import settings, platform compression/mipmap policy | Inspect channels, dimensions and alpha; replace pixels; preview sampling; compare authored and compiled results. Missing color semantics must be resolved before production compilation. |
| Sprite and atlas | Texture references, rectangles, pivots, logical bounds, optional nine-slice borders, stable sprite IDs | Slice/reorder/edit regions; visualize anchors, bleed/padding, and mip edges; preserve region IDs across atlas rebuilds. |
| Sprite animation | Ordered sprite references, integer/rational timebase, frame durations, loop policy, named events, declared clock policy | Play/pause, scrub, step, inspect events, edit durations and order, compare save/reload and compiled playback. |
| Tile palette | Stable tile IDs, sprite/animation references, collision/traversal associations, visual anchors | Paint with named tiles; validate references; changing packing never changes tile identity. |
| Puzzle layer | Grid/palette references, empty-cell semantics, dimensions, origin, scroll/time policy, parallax and component placement | Import supported PUZZLE/PUZZLE2 data; inspect/paint cells; preserve scrolling and animated tiles; preview within the containing map. |
| Map/world | Traversal grid, elevations and surfaces, bounds, layered scene references, portals/entrances, terrain footprints, environment and camera settings | Grid/collision/elevation editing, layer control, placement, selection, transforms, dependency inspection, real world preview, and portal validation. |
| Scene/prefab | Stable entities and components, transforms, parenting, render groups, resource references, instance overrides | Create/select/move/delete/duplicate objects; edit components and hierarchy; preserve overrides and stable references. |
| Mesh | Vertex attributes, submeshes, topology, bounds, material slots, skin influences, attachment markers, explicit coordinate conventions | Inspect geometry/materials/normals/weights; select submeshes; validate indices and bounds; preview the actual compiled mesh. |
| Rig and matrix channels | Authored hierarchy when established, stable joint/channel IDs, bind data, attachment frames, coordinate conventions | Pose and inspect rig/channel data; validate hierarchy and skin binding. An imported indexed matrix channel set must not invent a source hierarchy. |
| Motion/animation graph | Explicit matrix or TRS tracks, sampling/interpolation policy, rational timebase, events, transitions and blending rules | Scrub and compare poses; inspect transitions; preserve matrix interpolation when required; make optimization error visible and bounded. |
| Character assembly | Mesh/material/rig/motion references, named equipment slots, layered appearance, attachment bindings, action mapping and variation rules | Assemble and swap parts; preview actions and attachments; place multiple independently animated characters inside a map. |
| Material/shader | Typed shader contract, parameters, texture references, render state, target capabilities | Inspect/edit materials; compile with diagnostics; preview actual blend/depth behavior and reject incompatible targets. |
| Effect | Emitters, animated resources, transforms, attachment references, timing/events and deterministic random policy | Timeline/playback tools; attach to world objects or character slots; validate lifetime and resource ownership. |
| UI document | Hierarchy, layout/anchors, styles, sprite/font references, interaction/state bindings, animation references | Layout editing, multiple viewport sizes, text and hit regions, state previews, and compiled-layout conformance. |
| Font | Licensed source/font references, glyph coverage, fallback and shaping policy, metrics and target rasterization settings | Inspect language coverage and fallback, preview layout, validate reproducible rendering and redistribution rights. |
| Collision/navigation | Shapes or traversal grids, height/surface data, agent/profile rules, connectivity and query settings | Inspect/edit obstacles and traversability; compare generated data with runtime queries and server authority where applicable. |
| Audio | Source reference, channels/rate, loop regions, gain and spatial policy | Playback, loop editing, diagnostics, target encoding, and lifetime validation. |
| Game/build profile | Entry scene, resource/catalog roots, platform capabilities, compiler settings, build and release identity | Validated configuration, dependency closure, build reports, explicit packaging/signing/publication workflows. |

This is the required scope of the product roadmap. Each family becomes supported only with
its working editor, persistence, compiler/runtime path, and tests. Do not add empty assemblies
or placeholder panels to represent rows that have not been implemented.

## Compatibility import and semantic preservation

Source formats stay at the import boundary. Imported map IDs, appearance IDs, model IDs,
texture IDs, action IDs, and composite motion keys have distinct meanings; they cannot be
collapsed into a universal numeric asset identifier. Game-specific adapters resolve their
relationships into Studio resource references and retain useful source metadata separately.

Required fidelity rules:

- Resolve the full map dependency closure: DMap, puzzle layers, ANI sections/frames, terrain
  scenes, and their textures. Preserve component insertion order, parallax, anchors, offsets,
  footprints, cell traversal/elevation semantics, exits, and supported animation behavior.
- Keep puzzle render-driven advancement, elapsed-time terrain animation, and character action
  clocks explicit. One global animation timer cannot replace those different policies.
- Preserve full-matrix motion tracks alongside authored TRS tracks. Imported matrix sampling
  and blending cannot be silently replaced by quaternion interpolation or orthonormalization.
- Do not infer skinning from a PHY layout discriminator. Preserve and validate actual vertex
  influences, submesh-to-motion channels, attachment names, and the verified transform chain.
- Separate ancestor-link accumulation used for drawing from any extra target-submesh sampling
  performed by a transform-query operation. They are different consumers.
- Reconstruct multipart character resource bindings before rendering a loose mesh as a character.
  Texture/attachment labels are not automatically texture paths. Empty or missing optional
  source records need explicit adapter fallback rules and diagnostics.
- Use one world ordering system for actors, terrain, animated covers, items, and effects.
  Validate footprint adoption, insertion order, ties, clipping, and transparency; a global
  screen-Y sort alone does not establish conformance.
- Keep unknown source bytes and unresolved semantics in import evidence. Block promotion when
  dropping them would change behavior. Do not advertise synthetic-only format coverage as
  verified coverage of shipped assets.

The maintained source contracts include `20_notes/Rendering/c3_core_dll/05_phy.md`,
`12_motion_load_formats.md`, `13_motion_sampling_and_blend.md`, the corrected
`role3d_dll/12_linked_motion_chain_resolution.md`, RoleView motion/action/transform notes,
and `20_notes/Game_Map/world_render_contract_5517.md` and `pux_aoxpuzzle_terrain.md`.
Conflicting historical paragraphs must be resolved against raw assembly and corrected notes.
Any new verified findings belong in `20_notes` before driving an importer implementation.

## Runtime compilation and storage

Compilation resolves a typed resource graph and emits only the selected game/build closure.
Build-order cycles are errors. Validated logical world/portal references can be mutually
referential without forcing every destination map to load eagerly. Missing required
references, ambiguous subresources, invalid structural cycles, and unsupported semantics fail
the build with asset/field diagnostics.

The build key must bind source revisions, authored documents, importer/compiler versions,
target profile, and relevant settings. Host paths, timestamps, directory enumeration order,
or unrecorded machine configuration must not affect output. Reproducible builds require
independent processes and the same input snapshot on the supported platforms.

Compiled resources use explicit versions, typed identifiers, bounded counts, canonical
numeric/string encodings, exact section coverage, and checked offsets. Shared immutable
resources are deduplicated; per-instance character/animation state is never shared as mutable
asset data. Readers validate resource types and lengths before allocating or using offsets.

The runtime layout must support the access patterns that measurements justify: map region
reads, reusable geometry/motion sections, asynchronous CPU decoding, and budgeted GPU uploads.
Before choosing chunk geometry, compression, texture formats, quantization, or bundle sizes,
measure representative scenes, update workloads, storage, CPU/GPU memory, cold/warm loading,
and cancellation. PR #196 and `.octex` are existing candidates with limited consumers; neither
is a frozen universal production format.

Server-facing collision/map artifacts require a build adapter and conformance against the
server's existing data contracts. One content build binds its client and server projections to
the same authored revision and tool versions. Map instance identity, terrain identity, and
Studio asset identity remain distinct. The server must not depend on editor or import assemblies.

Build output has an indexed dependency manifest and immutable content revisions. Existing
release manifests, signing, trusted installation, repair, and rollback remain the publication
path. Private keys stay outside projects and builds. Asset-integrity hashes and signed
publisher authenticity remain separate guarantees.

## Shared rendering and professional workspace

Each preview target must preserve the target game's resource, animation, material, projection
and ordering behavior. Reuse the game's existing mechanisms where possible; a target-specific
preview adapter is not a new game runtime. The editor supplies camera/tools/selection overlays;
game network sessions, player input, and gameplay authority remain outside Studio.

The OpenGL host adapter must explicitly handle the editor framebuffer, context ownership,
thread affinity, resizing, display scaling, render-target capacity, context loss, upload budgets,
and cleanup. Device limits are renderer mechanisms; game-specific background/camera constraints
remain policy supplied by the game. The editor must still support authoring outside map bounds.
Adapting these contracts is required before embedding map/character rendering in Avalonia.
Do not implement another mesh/scene renderer inside GUI code.

The finished workspace needs functional project/asset exploration, scene hierarchy, layers,
inspectors, viewport tools, animation timeline, dependency/error navigation, and build output.
Selection must remain synchronized across these views. Undo/redo, multi-object operations,
shortcuts, focus, dirty-state feedback, cancellation, recovery, and accessible controls are
acceptance requirements. Workspace customization, visual consistency, readable diagnostics,
and native dialogs must be tested on Windows, macOS, and Linux. Incomplete features do not
appear as inert controls.

Resource, frame, memory, loading, and editing budgets must be defined against recorded
reference hardware and representative projects before a milestone is called production-ready.
Profiling must separate CPU work, GPU uploads/draws, memory residency, import/build work,
and UI responsiveness. Optimization claims require measurements and conformance results.

## Delivery milestones

| Milestone | Concrete deliverable | Completion evidence |
| --- | --- | --- |
| Foundation | Texture project, import/edit/replace/preview/save/reopen/compile workflow | Existing persistence, corruption, lifecycle, CLI, headless UI, native macOS smoke, and CI checks. |
| Resource graph and animation | Version-2 migration, stable typed/subresource references, import/reimport review, sprite/atlas/animation authoring, edit transactions and shared-renderer host adapter | Identity-preserving migration/reimport, undo/redo/recovery tests, animation scrubbing/timing conformance, real embedded viewport on each platform. |
| Puzzle and map authoring | Supported puzzle/ANI import, authored tile palettes, map cells/collision/elevation, terrain and animated layers, entrances/exits, parallax/scrolling, viewport editing | Create a map without source assets; import supported reference maps; edit/save/reopen/compile; compare traversal, layers, clocks, scrolling, and rendered output. |
| Characters in maps | Verified geometry/motion import, material and attachment resolution, matrix sampling/skinning, multipart assembly, action preview, unified world ordering | First prove an approved static fixture; then animated multipart characters in a map, independent instance clocks, equipment changes, correct occlusion, deterministic pose and render comparisons. |
| Broader authoring and game integration | Materials/shaders, effects, UI layout, audio, reusable scene data, content build profiles, explicit game resource bindings | Each editor authors assets accepted by its target game; no source-loader fallback hiding missing compiled resources; integration preserves required semantics. |
| Production release | Complete packaging/signing/install/update/repair/rollback integration, operating documentation, recovery and performance gates, polished platform UX | Real authenticated release tests, adversarial input/failure tests, published performance evidence, platform manual sign-off, dependency/licensing review, and clean release artifacts. |

Every milestone includes source audit, evidence closure, implementation, tests, re-audit,
formatting/analyzers, CI, documentation, and review. Coverage is tracked per format feature
and representative fixture. A passing build does not establish full map, character, or
production readiness. The delivery unit is a complete usable workflow with verified limits.
