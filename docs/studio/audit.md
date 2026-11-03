# Foundation audit

Historical foundation audit. Current project ownership is in [architecture](architecture.md):
image readers now remain in Content and world drawing in Rendering. The earlier Imaging and
ConquerRendering assembly extractions described here have been removed.

Historical foundation record. Current product ownership and extraction are described in
[architecture](architecture.md); the current map plan is [Map asset contracts](map-content-contract.md).
Statements below about earlier PR states and the unified-engine direction describe their dated checkpoints.

The initial ownership decision used these clean, verified repository snapshots on 2026-11-02:

| Source | Revision |
| --- | --- |
| OpenConquerClient main | `ac1a63ca6b6e1aeeefdebdfe6072282f33c83034` |
| OpenConquerServer main | `a16cae6a2942dc2883131645b766b89e73ae145e` |
| Client PR #196 head | `8401a8265f95dc2af2740d1abf4eac39ec3e8cd9` |

GitHub main matched the two local checkouts. Work takes place in an isolated client worktree.
This work does not edit synced project sources or the original client/server worktrees. PR #196 was
read without modifying or merging its branch. The source archives remain reference inputs;
this audit does not claim a new exhaustive analysis of their contents or binary formats.

## Ownership findings

| Evidence inspected | Finding and decision |
| --- | --- |
| Client project references, `Content/Images`, ANI loaders, WDF source, image tests, WORLD-007 | TGA/DDS decoding and RGBA pixels already exist. Move those implementations into shared Imaging; retain existing Content adapters and tests. |
| `Content/Media/EmbeddedMedia.cs`, account artwork APIs and embedded resources | Existing converted client images have explicit dimensions and integrity checks. Import supported images through those APIs; do not duplicate their bytes, metadata hashes, or decompression implementation. |
| `Rendering/OpenGL/OpenGLGraphicsDevice.cs`, `OpenGLRenderer.cs`, texture and sprite resources | Reusable OpenGL 3.3 Core rendering exists. Presentation targets framebuffer zero and assumes host-context ownership; embedded scene preview needs a deliberate host adapter and conformance work. Use an actual pixel preview for the texture slice. |
| `Platform/Windowing/Desktop`, `Platform/OpenGL` | The game host owns a standalone window, input devices, frame pacing, and context lifecycle. Keep that host out of the desktop authoring domain. |
| Launcher project, desktop tests, package versions | Avalonia is already pinned and used. Reuse that dependency baseline and interaction-testing approach. |
| Launcher managed manifest/signature/verifier, Product Tool, content-distribution contract | Existing authenticated release and installation ownership must remain authoritative. Defer signing/publishing integration; add no competing trust scheme. |
| PR #196's model, reader/writer, compiler, changed-file inventory, body and discussion | Indexed map sections, SHA-256 checks, deduplicated resource table, bounded writing, and window reads exist on the branch. The importer rejects exits; dependent asset migration and runtime adoption remain open. Whole-section validation still applies to small windows. Do not freeze or import that candidate as Studio's universal map model. |
| Server project references, game-data tool, map terrain/collision importers, source-directory checks, content docs | Server map definitions, collision, exits, and gameplay identity have separate authoritative consumers. Studio has no server dependency; do not generalize those models into an editor scene or copy their parsers. |
| Both repositories' build props, lockfiles, formatting/analyzer rules, client workflows | Preserve .NET 10/C# 14, warning-as-error builds, locked dependencies, formatting, and existing client gates. Add Studio workflow tests on all three desktop operating systems. |

## Review scope

The implementation review covers immutable identity and edit state, strict JSON input,
bounded decoding, source preservation, atomic-save commit order, exclusive session ownership,
staged non-overwriting builds, cancellation, bitmap disposal, operation serialization, and
shutdown. Automated coverage includes generated TGA/DDS files, existing supported artwork
imports, known pixel transforms, persistence, corruption, invalid metadata, failed writes,
duplicate IDs, linked paths, content reproducibility, CLI subprocesses, and desktop controls.

Native macOS smoke validation opened a project through the folder picker, previewed existing
client artwork, applied and saved an edit, built and validated content, created a new texture,
and saved it through the close dialog. A separate CLI process reopened the saved result and
validated both desktop and subsequent CLI builds. The final build also replaced an imported
artwork image through the native file picker, preserving its GUID, edit settings, and original
source association through save and a separate CLI reopen/build.

The client base first advanced to `ee9cd034432fee67c4f92583e7f57e6598d4b82b` during implementation.
The PR later received upstream `f9afe146` through merge `1d06bc81`, adding authored-background
and GPU render-target capacity checks. Those changes were retained and inspected; they do
not move game camera policy into Studio or change shared decoder ownership.

The extraction must pass the full existing client solution; Studio tests alone are insufficient.
Headless UI tests are distinct from native desktop smoke checks. Windows link creation is
skipped when its required privilege is not provisioned. Native file pickers, accessibility,
display scaling, and future GPU viewport behavior require platform-specific manual checks.

The next integration must establish explicit game resource bindings and preserve UI semantics
before the client consumes Studio-built replacements. Imported images are not a completed
UI, map, animation, packaging, or release migration.

## Recorded validation

At the foundation boundary, macOS Apple Silicon passed locked restores, formatting,
warning-as-error builds, 3,323 existing client tests, 60 Studio tests, and three CLI subprocess
workflows. The unchanged decoder algorithms were compared against the original source, and
existing external package versions/hashes were checked for equality. Studio CI additionally
runs build, tests, independent content compilation/verification, and publish-boundary checks
on Windows, macOS, and Linux. CI and manual desktop checks are separate evidence.

After integrating upstream render-target/background constraints, the complete local client
suite passed 3,330 tests; the 60 Studio tests and three CLI workflows also passed again.
The expanded asset platform plan uses existing map/puzzle code and maintained native evidence.
A focused live motion-sampling audit added raw byte-checked captures and importer constraints
to the separate native workspace's `20_notes`; those research files are not runtime assets.

## Client-connected authoring and rendering

The next slice starts from main `c1ac806b49b31496b1cb51a6a904cfa7f39b8d26`, after the
foundation PR was merged. PR #196 remains independent; no candidate map format is adopted.

Studio can open the actual client repository, retain its authoring workspace under `.studio/authoring`,
and browse the existing curated content payload. Local `.user/client.json` records relative
repository/content connections. An original installation can be attached explicitly. Browsing
is read-only; importing creates canonical editable resources. Source ANI ordering is retained,
with authored timing chosen explicitly. Missing source frames fail before any resources are added.
WDF-backed ANI frames use the existing packaged content source; anonymous WDF hash entries do
not become invented named assets in the browser.

Nine existing world-rendering files moved into the internal ConquerRendering component. Client
and Studio now own that implementation jointly. Projection, puzzle clocks, terrain clocks,
footprint ordering, and draw-list behavior were retained. Studio adds a bounded camera-tile
control and supplies preview texture budgets; client loading/session/network logic stays in Client.
The renderer's framebuffer overload and queried texture capacity are additive shared mechanisms.

The map browser uses the real map archive, cell grid, puzzle resources, scene files, and images.
Map source metadata is inspectable but not converted into an editable map schema. Positioned
animated overlays omitted by the existing client renderer are reported. Scrolling puzzle
semantics that it rejects remain explicit failures. Characters, map editing, and automatic
client consumption of authored replacements are not claimed.

Local Apple M4 validation rendered actual `Control.ani` action-button frames after import,
compilation, and independent runtime loading. Actual map 1010 rendered puzzles and terrain;
camera movement changed framebuffer pixels, and resize/context recreation passed. Generated
fixtures remain unit-test inputs, not evidence of client-asset compatibility. The platform GPU
validation executable now uses checked-in client assets. No proprietary fixtures were copied
or added, and no new native format interpretation was introduced in this slice.

The client source set contained 3,936 files; its browser inventory also contains map-catalog
records, including records whose archives are missing. Missing data is labelled or reported;
this inventory does not claim every original-client asset has been converted.

Main advanced to `80b8721a` during this slice. Its PR #204 animated-scenery activation and
PR #205 footprint-child ordering were integrated before the stopping point. The current
shared component contains those implementations, rather than retaining the earlier terrain-only
draw list. Positioned animated overlay children remain a separate unsupported path.

The automatic client workspace is deliberately local and ignored by Git. A version-controlled
game-asset project can be created separately and attached to the same client sources. Automatic
client resource replacement, final project-layout policy, map editing, characters, and release
integration remain follow-up work. This checkpoint is not a production-complete AAA toolchain.

At this stopping point, macOS Apple Silicon passed 82 Studio tests, all 3,340 current client
tests, five CLI subprocess workflows (including forced save-process termination), and actual
client-asset GPU validation after integrating main. The GPU gate imported/compiled/loaded real
Control.ani frames and rendered map 1010 with the shared current world implementation.
Windows GPU/native UX, Linux native UX, final project-layout policy, and production release
integration remain explicit gates; CI status is tracked on the draft PR.

CI host correction: Linux rendering validation explicitly permits Mesa's software OpenGL
driver in the validation executable only. Studio retains Avalonia's normal driver policy.
The hosted macOS VM does not expose the required GPU interop, so its required job covers
build/tests/publish and records the hardware gate explicitly. Actual Mac GPU validation
remains the same failing-on-error executable, run on graphics-capable hardware.
