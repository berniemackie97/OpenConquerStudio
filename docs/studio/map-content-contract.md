# Map content contract and migration plan

Status: target design and migration gates, November 3, 2026. This does not freeze a new compiled
map schema. Studio/Engine includes bounded authored terrain cells, single-revision region painting
and ordered exit markers, with an explicit server tool bridge for terrain/collision pair generation.
The terrain grid is a raw-data editing view, not a game visual scene. World portal destinations,
visual maps and fully paired game content outputs remain outstanding. Client and server retain
game execution and authority.

## Current formats and consumers

| Data | Current owner | Meaning and boundary |
| --- | --- | --- |
| DMap, PUL, scene, ANI, WDF and source textures | Client Content/import tools; independent server import tooling where already implemented | Compatibility inputs and evidence. Preserve bytes, original paths, lookup policy and unresolved semantics. Do not make them the canonical format for newly authored games. |
| `.ocmap` v1, magic `OCMP` | Client `MapAssetCodec`, Reader/Writer and Content Tool compiler; merged PR #196 | Indexed visual-map candidate: references, grid, objects and layers. Compiler rejects exits and unmodeled scenery. Dependencies still need compilation. Current game/Studio map rendering does not consume this format. |
| `.ocbt` v1, magic `OCBT` | Server base-terrain reader, writer and fingerprint | Ground grid plus ordered exit markers for a MapDataId. Studio exports source JSON and invokes an explicitly supplied server tool to generate and validate the terrain/collision pair. Studio never writes `.ocbt` binaries. |
| `.ocmc` v1, magic `OCMC` | Server collision composer, reader and writer | Ordered sparse static-cell overrides bound to the exact base-terrain fingerprint. Includes the effect of supported scenery composition. Required even when the override list is empty. |
| Map definitions and transitions | Server-owned, versioned catalogs | World mapId/flags and terrain bindings; resolved or explicitly unresolved portal destinations. Do not infer destinations from visual layers or another world using the same terrain. |
| `project.ocstudio`, `.ocasset.json`, immutable inputs | Studio Authoring | Editable textures, sprite animation, meshes and bounded terrain-cell revisions. World/scene graphs and paired compiled maps are not yet authored. |

The source inspection used client `78d752c2da39ca8f9f45f64377844efacce42d17` and server
`a16cae6a2942dc2883131645b766b89e73ae145e`. Server tooling verified all 185 required base
terrains against the local original installation, recomposing 82,387 terrain attachments and
20,275 effective collision overrides. Rebuilt collision artifacts were compared with the
checked-in release; see [evidence](evidence/map-contract-2026-11-03.json). This establishes
agreement with the current implemented source readers, not universal native parity.

The existing client compiler was also exercised against the same original installation:

| MapDataId | Candidate compilation | Meaning |
| --- | --- | --- |
| 1010 | Succeeds | Supported source records encode; visual/runtime equivalence still needs its own gate. |
| 1929 | Succeeds | Supported source records encode; this does not establish every animation/layer behavior. |
| 1999 | Rejects exit markers | Confirms the known missing exit representation blocks this map; no records were discarded. |

## One authoring source, separate target outputs

An authored world references terrain, visual layers, placements, environment and world-specific
portal data through stable resource identities. A content build resolves one immutable authored
revision and produces the required client and server projections. Neither consumer independently
reimports original DMaps during a production build or at runtime.

Keep these identities distinct:

- **Asset GUID:** stable across rename, move and reimport; includes a project/resource namespace.
- **Terrain/MapDataId:** numeric compatibility binding for the existing game; several worlds may use it.
- **World mapId:** authoritative location/instance definition, flags and transition source identity. Studio now authors its explicit binding to one terrain resource and full raw flags value; transition destinations are separate.
- **Revision digest:** exact content version; changes when relevant data or build inputs change.
- **Source identity:** original path, package member and source digest retained by the importer; never silently used as a universal authored ID.

Current `.ocmap` reference strings are derived by removing extensions from normalized source
paths. The importer detects collisions, but this is not stable rename-independent identity.
Introduce an explicit source-to-asset mapping before canonical authored map compilation. Preserve
existing references through a migration; do not replace them with a new identity on every import.

## Authored terrain and map data

Specify typed resources before selecting their final on-disk layout:

- Terrain dimensions, grid origin and axes, tile/world units, signed elevation units, surface
  identities and traversal semantics. Preserve raw compatibility fields in import metadata
  when they have not been translated into a proven semantic model.
- Stable tile/palette entries and puzzle-layer resources, empty cells, component placement,
  insertion order, offsets, parallax, scroll velocity and explicit animation-clock policy.
- Static scene placements, terrain footprints and their composition order. A terrain change
  must invalidate collision even if its visible texture did not change.
- World identity/terrain binding, flags, exit markers, portal references and destination policy.
  Preserve legitimate out-of-grid source exit markers; they are not ordinary paintable cells.
- Visual environment/camera defaults and reusable object references, independent of server
  combat, account state, occupancy and runtime movement eligibility.
- Import diagnostics and provenance with feature coverage: supported, preserved but unresolved,
  or rejected. Unknown behavior that could change meaning blocks promotion.

Declare coordinate handedness, axis direction, origin, scale, matrix convention, cell order and
tile-to-screen projection for each resource family. Imported matrix animation must retain its
verified sampling policy; do not force every track into TRS or one global clock.

Base terrain and final static collision are distinct views. Preserve the current server's
composition and fingerprint rules. An artist's visibility toggle must not implicitly remove a
collision footprint. In the current server contract `.ocmc` is terrain-scoped: worlds sharing
a MapDataId share its collision. A world-specific collision edit needs a distinct terrain
binding or a deliberately versioned server-contract change; it cannot silently mutate all worlds.

## Versioned project, cache and compiled artifacts

Team-owned data comprises the project descriptor, typed authored revisions, lossless payloads,
import mappings/settings and owned source files or a digest-pinned acquisition recipe. A clean
clone must rebuild without another user's absolute paths, local connection file or thumbnail cache.
Keep `.user`, locks, recovery/cache data and builds separate. The current ignored
`.studio/authoring` workspace must be migrated explicitly before it can become the default team project.

Preserve Studio's atomic index commit and immutable resource revisions. Related terrain, layer
and portal edits form one undo/save transaction. Safe compaction must retain revisions reachable
from committed indexes and supported recovery history; do not delete old revisions during an
incomplete save. Large grids need bounded region editing and lazy payload access, not millions
of tiny JSON files or an eagerly loaded whole-game object graph.

A target build manifest binds the authored revision to:

- Explicit importer, compiler, schema and target-profile versions and relevant settings.
- Required dependency identities/revisions and deterministic transitive closure.
- Client map/resource outputs and server terrain, collision and catalog outputs, each with length and SHA-256.
- The exact base-terrain fingerprint used by each collision file and the world-to-terrain bindings.
- Feature/capability requirements and a compatibility version for the paired game release.

The build key includes all inputs affecting output. Host paths, clock time, directory enumeration
order and unrecorded machine configuration must not affect content bytes. Build into a new
staging directory, validate through the existing target readers, then commit the whole result.
Never update a client artifact while leaving its server projection at the previous revision.

The manifest is a target output contract, not a second publisher trust scheme. Existing signed
release metadata and authenticated installation remain the delivery boundary. Embedded hashes
detect corruption; they do not authenticate a publisher. Do not put private signing keys in a project.

## Reader, performance and security rules

Retain explicit schema versions, checked offsets, canonical encodings, strict counts and exact
section coverage. Reject unknown required features and unsupported versions. Bind dependencies
to resource type as well as ID. Detect missing references, structural cycles, duplicate bindings
and unsafe paths before committing an import or build. World portal cycles can be valid and must
not force eager loading of every reachable destination.

`.ocmap` window reads currently hash the entire cells section on each access. That is bounded
integrity checking, not inexpensive spatial streaming. Benchmark representative map windows
before choosing chunk geometry, digest granularity, compression or bundles. A future cached
validation result must bind an immutable revision/handle; do not trust a path whose bytes can change.

Use indexed/lazy metadata and budgeted payload loading. CPU reads/decodes run off the UI/render
thread; uploads/disposal run under graphics ownership. Set bounded queues, cache residency and
cancellation behavior. Profile cold/warm I/O honestly, peak CPU/GPU memory, UI latency, build
incrementality, update reuse and crowded map/character rendering. The existing 64-texture/64-MiB
project limits are foundation safety bounds, not an enterprise asset-library sizing model.

## Shared ownership and repository extraction

The new Studio repository owns authoring, orchestration, its content reader library and GUI.
Recorded snapshots of the existing Content and Rendering projects supply import readers and preview backends.
The server continues to own and validate its `.ocbt`/`.ocmc` consumers and generation tools.
Studio's server build adapter must invoke an explicitly versioned tool/output contract, or use
a deliberately extracted shared codec package. It must not reference a neighboring server
checkout in MSBuild or copy its binary serializer into the editor.

Client source snapshots are ordinary tracked files with commit/hash provenance, not new shared
assemblies or a whole-client submodule. Update them with actual-asset tests. PR #213 has since
merged initial character-body drawing; full multipart and action-driven animation remain incomplete.

## Delivery gates

1. **Repository extraction:** standalone build, project workflow, actual import/preview, tested
   source provenance, clean publish, and preserved client rendering CI. Keep the extraction reviewable
   separately from map schema changes.
2. **Versioned project and terrain editing:** migrate existing scratch projects without loss;
   create a new terrain without original assets; edit surface/elevation/traversal, undo, save,
   reopen and compile a minimal server artifact accepted by the authoritative reader.
3. **Map/puzzle authoring:** typed palettes/layers/placements/portals; support exits and the full
   required dependency closure before replacing `.ocmap` v1. Validate map IDs 1010, 1929 and 1999
   independently. Unsupported records must fail explicitly, never disappear from a successful build.
4. **Paired content acceptance:** build both game projections twice in separate processes;
   compare bytes and normalized semantics through existing readers. Check every required cell,
   ordered exit and collision override, not only sample portal tiles. Tamper or swap a terrain,
   collision, binding or manifest and require rejection.
5. **Runtime adoption and characters:** compare source and compiled visuals, layering, timing,
   scrolling, traversal and portal behavior; then use the same target resources for rig/motion,
   multipart character assembly, equipment/attachments and map preview. Mesh decoding or a
   frame-zero body is not complete character authoring.
6. **Release sign-off:** paired compatibility manifest, clean-clone reproducibility, source-free
   runtime loading, crash/cancellation recovery, measured resource budgets and platform UX/GPU
   checks, plus authenticated deployment and rollback exercises.

The next implementation milestone is versioned project ownership and a small terrain
edit/save/reopen/build workflow. Do not freeze all future formats, create an engine framework,
or raise safety limits merely to label the platform production-ready.
