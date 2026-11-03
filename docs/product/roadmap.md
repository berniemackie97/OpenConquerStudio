# OpenConquer Studio/Engine roadmap

Status: active product plan, November 3, 2026. Milestones are dependency gates, not delivery-date estimates.

## Product contract

OpenConquer Studio/Engine is one integrated game-development product. The desktop workspace, authored project model, asset compiler, reusable content/runtime contracts, rendering integration and release tooling must work as one pipeline. OpenConquer is the first complete production target. Broader game support is a direction, not an implemented feature or justification for speculative adapters.

The editor owns authoring and developer tooling. Games continue to own their execution loops, gameplay, networking and server authority. The present Runtime assembly loads and samples compiled content; it is not yet a full scene/game runtime. A standalone scene execution path and real game adoption are explicit future gates. The Studio application must never be a required game-runtime dependency.

A resource type is complete only when it can be created or imported, edited, validated, undone, saved, reopened, compiled, loaded by its intended consumer and tested. Existing compatibility data must not be discarded or reinterpreted without evidence. Supported output formats are versioned according to actual consumers; a provisional format is not a release contract.

See [asset requirements](../studio/asset-platform-plan.md), [map contracts](../studio/map-content-contract.md), [ownership](../studio/architecture.md) and [acceptance ledger](acceptance.md) for details. Those documents specify mechanisms; this roadmap defines delivery order and exit gates.

## Completion rules

- **Implemented:** the feature exists in the current code, possibly with known limitations.
- **Verified:** its documented behavior passed the specified automated and hardware or integration checks.
- **Accepted:** its complete workflow passed the milestone exit gate, with links to evidence, supported platforms and unresolved limitations.
- A passing CI build is not proof of live desktop interaction, graphics output, import fidelity or production performance. No milestone advances to Accepted on implementation alone.
- Each change follows audit, evidence/design closure, implementation, testing, re-audit, documentation, CI and review. Defects found in a slice are resolved before acceptance. Pull requests are not merged automatically.
- Each deliverable is a coherent working vertical slice. No placeholder editors, duplicate loaders, alternate renderers, speculative plugin system or unmeasured performance claims.
- All performance and resource-budget claims require a representative workload, test hardware and recorded measurements.

## M0 — Product contract and reliable workspace

Goal: a consistent product boundary and trustworthy daily editing workflow.

- [ ] **M0.1** Align README, architecture and technical plans with the single Studio/Engine product and current implementation limits.
- [ ] **M0.2** Audit each current capability and record implementation, verification, acceptance and missing evidence.
- [ ] **M0.3** Verify project create/open/close/switch, Save/Discard/Cancel and invalid-edit recovery.
- [ ] **M0.4** Verify edit persistence, undo/redo, selection changes, navigation and document dirty state.
- [ ] **M0.5** Verify browsing, inspector/preview synchronization, error navigation and keyboard focus.
- [ ] **M0.6** Run actual desktop dialogs, graphics, resizing and context lifecycle on Windows, macOS and Linux.
- [ ] **M0.7** Measure startup, editor latency, memory and build baselines on defined workloads.

**Exit:** Create/import/edit/undo/save/reopen/build is reliable on supported platforms and cannot silently lose authored work.

## M1 — Team projects, asset graph and reproducible builds

Goal: authoring data stays stable, reconstructible and safe as projects grow.

- [ ] **M1.1** Finalize the versioned team project layout and migrate existing local authoring projects without loss.
- [ ] **M1.2** Preserve stable assets and subresource IDs across rename, move, replace, reimport and build.
- [ ] **M1.3** Record import settings, source identity and unsupported-feature diagnostics with an explicit reimport review.
- [ ] **M1.4** Validate typed references, transitive dependencies, invalidation and deletion impact.
- [ ] **M1.5** Verify atomic save, crash recovery, revision retention/compaction and multi-resource undo.
- [ ] **M1.6** Support indexed search, previews and bounded resource residency on representative large libraries.
- [ ] **M1.7** Implement dependency-aware incremental compilation with complete versioned build keys.
- [ ] **M1.8** Verify deterministic content in separate processes and clean checkouts.

**Exit:** A team project can be cloned, edited, migrated, rebuilt and recovered without identity churn, hidden local paths or missing resources.

## M2 — Terrain, maps and world authoring

Goal: create a world from scratch and preserve the semantics of supported imported worlds.

- [ ] **M2.1** Define typed terrain, world, tile, layer, placement and portal resources with distinct identities.
- [ ] **M2.2** Import complete map dependency graphs and report unresolved/unsupported records without dropping them.
- [ ] **M2.3** Edit surface, elevation, traversal and collision data with undo and consistent validation.
- [ ] **M2.4** Paint tile palettes, puzzle grids, scrolling/animated layers, scenery and ordered placements.
- [ ] **M2.5** Provide viewport selection, transforms, layer tools, multi-edit and saved camera/environment settings.
- [ ] **M2.6** Author world IDs, terrain bindings, entrances, exits and portal destination policies.
- [ ] **M2.7** Benchmark region access, memory, integrity and loading before choosing final compiled-map layouts.
- [ ] **M2.8** Produce linked client visual and server terrain/collision outputs from one authored revision.
- [ ] **M2.9** Verify required behavior on map data 1010, 1929 and 1999, including explicit rejection of unresolved semantics.

**Exit:** A new and an imported world can be edited, reopened and compiled, and the client/server consumers agree on the same world revision.

## M3 — Characters, meshes and animation

Goal: complete characters are editable and animate correctly inside authored worlds.

- [ ] **M3.1** Validate mesh import, topology, material bindings, submesh identity and geometry inspection.
- [ ] **M3.2** Author multi-part bodies, appearance variants, headwear, equipment and attachment bindings.
- [ ] **M3.3** Support rig/channel and skin inspection/editing only where imported semantics are verified.
- [ ] **M3.4** Author clip timing, motion tracks, action mappings, events, transitions and blending.
- [ ] **M3.5** Add a timeline with playback, scrub, stepping, diagnostics and independent instance clocks.
- [ ] **M3.6** Validate transforms, full-matrix sampling, skinning, world ordering and attached effects.
- [ ] **M3.7** Compile self-contained character resources and load them without original source files.

**Exit:** Multiple equipped animated characters render correctly inside an authored map after save/reopen/build and runtime loading.

## M4 — Scene composition, graphics and effects

Goal: the editor and games use one coherent scene/rendering contract.

- [ ] **M4.1** Author scene hierarchies, stable entity identities, components, transforms and grouping.
- [ ] **M4.2** Add reusable scenes/prefabs and explicit, identity-preserving instance overrides.
- [ ] **M4.3** Implement viewport selection, snapping, gizmos, duplication and multi-object operations.
- [ ] **M4.4** Verify shared rendering of sprites, meshes, depth, transparency, layering and shadows.
- [ ] **M4.5** Author material/shader parameters and validate supported render-target capabilities.
- [ ] **M4.6** Author effects, lifetime policies, attachment behavior and animation clocks.
- [ ] **M4.7** Profile graphics ownership, GPU memory, context loss, scaling and large scenes.

**Exit:** Authored scenes and compiled scenes present equivalent supported content through the same runtime rendering mechanisms.

## M5 — Game interface, fonts and audio

Goal: produce game presentation assets through the same complete asset workflow.

- [ ] **M5.1** Define typed UI hierarchy, layout, states, interaction bindings and references.
- [ ] **M5.2** Edit layout, anchors, styles, hit regions and responsive sizes in the viewport.
- [ ] **M5.3** Preview interface states and animations at representative game resolutions.
- [ ] **M5.4** Import fonts with verified metrics, glyph coverage, fallback and distribution rights.
- [ ] **M5.5** Import and edit audio playback, gain, loop and spatial settings.
- [ ] **M5.6** Compile, load and compare UI, font and audio output in the target game.

**Exit:** A representative complete game interface builds and functions using compiled resources without editor dependencies.

## M6 — Runtime adoption and game integration

Goal: Studio-built content is the actual content consumed by running games.

- [ ] **M6.1** Define versioned runtime ownership, loading, resource and scene execution contracts.
- [ ] **M6.2** Bind authored resources explicitly to OpenConquer client and server identities.
- [ ] **M6.3** Consume compiled maps, collision, characters, effects and UI with no hidden source fallback.
- [ ] **M6.4** Implement bounded asynchronous loading, streaming, cache ownership and cancellation where measured.
- [ ] **M6.5** Verify game startup, world entry, traversal, transitions, shutdown and incompatible releases.
- [ ] **M6.6** Execute a minimal Studio-authored scene independently of original OpenConquer source assets.

**Exit:** A running game uses the same validated compiled revision; standalone authored-scene execution demonstrates the integrated Studio/Engine boundary.

## M7 — Build, package and publish

Goal: a validated project becomes a trusted, recoverable game release.

- [ ] **M7.1** Finalize versioned target capabilities, profiles, resource closure and release identity.
- [ ] **M7.2** Verify deterministic separate-process and cross-platform builds.
- [ ] **M7.3** Validate complete artifacts, corruption handling, dependency closure and compatibility.
- [ ] **M7.4** Integrate the existing signing, packaging and trusted release pipeline.
- [ ] **M7.5** Exercise installation, update, repair, interruption, rollback and mixed-version refusal.
- [ ] **M7.6** Produce actionable build diagnostics, manifests and release evidence.

**Exit:** A game release can be built, authenticated, installed, launched, updated, repaired and rolled back.

## M8 — Production certification and version 1.0

Goal: a stable toolchain for sustained team development.

- [ ] **M8.1** Audit ownership, concurrency, cancellation, disposal, validation and security.
- [ ] **M8.2** Resolve all release-blocking issues from representative content and adversarial tests.
- [ ] **M8.3** Meet recorded CPU/GPU memory, load, edit, streaming and build performance budgets.
- [ ] **M8.4** Pass desktop, graphics, accessibility and scaling checks on supported platforms.
- [ ] **M8.5** Pass clean installation, migration, interrupted-operation and recovery scenarios.
- [ ] **M8.6** Publish user documentation, supported format/API policy and release/rollback procedures.
- [ ] **M8.7** Complete and archive an end-to-end OpenConquer production acceptance project.

**Exit:** The release candidate passes independent automated and desktop acceptance, has no known release-blocking defects and is published with verifiable artifacts.

## Delivery order

M0 and M1 are prerequisites for accepting large-world authoring; their bounded foundation code can evolve alongside a complete M2 vertical slice. M2 is the first major product proof: create an empty world, edit it visually, compile it and run it. M3–M5 expand the supported authored content, M6 makes the compiled outputs operational, and M7–M8 certify delivery. Existing features in later milestones may progress earlier, but cannot be marked Accepted without their full exit gates.

Track each micro-goal in a focused issue or PR, linked from [acceptance.md](acceptance.md). A PR may close more than one goal only when all of them pass their own criteria. Checkboxes change only after reviewed and merged acceptance evidence.
