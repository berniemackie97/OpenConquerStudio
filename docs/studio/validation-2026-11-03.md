# Standalone preparation validation

November 3, 2026. Local preparation checkpoint `1a8de59`; paired client extraction `8b61fdcd`
on `bernie/studio-content-plan`. The dependency is client commit
`78d752c2da39ca8f9f45f64377844efacce42d17`; server inspection/tooling used
`a16cae6a2942dc2883131645b766b89e73ae145e`. Neither primary checkout was changed.

| Check | Result |
| --- | --- |
| Standalone locked restore and Release build | Pass, zero warnings/errors |
| Fresh recursive clone, locked restore and Release build | Pass without references to sibling checkouts |
| Studio tests | 88 passed, zero failed/skipped |
| Independent-process CLI workflows | Five passed, including deterministic builds and interrupted save |
| Formatting/analyzers | Studio and client extraction pass |
| Client extraction Release build and tests | Zero warnings/errors; 3,402 tests passed, zero failed/skipped |
| Native macOS GPU check | Actual Control.ani import/build/runtime load/readback, map 1010, camera, resize and context recreation pass on Apple M4 |
| Studio publish | No Client/Launcher/Networking assembly or loose WDF/DMap/TGA/DDS/BNDB source; required FreeType notices retained |
| Authoritative server source verification | 185 terrain identities, 82,387 terrain attachments, 20,275 effective collision overrides |
| Collision regeneration | All 185 generated `.ocmc` files exactly match the checked-in server release |
| Client candidate map compiler | Maps 1010 and 1929 succeed; 1999 rejects unsupported exits explicitly |

Source/collision verification uses the original local installation as read-only input. Outputs
were generated in a fresh temporary directory and compared byte for byte, not copied into
the server repository. The maintained evidence is [map-contract-2026-11-03.json](evidence/map-contract-2026-11-03.json).
These checks prove agreement with the current implemented readers, not complete native parity.

The new repository and client extraction are local commits. No GitHub repository was created,
no branch was pushed, and no PR was merged. CI configuration was transferred/adapted, including
recursive pinned dependency checkout, platform builds/tests, Linux rendering, dependency review
and dependency submission. GitHub checks have not run for this new repository. Native Windows/Linux
UX and fresh hosted-runner sign-off remain outstanding. The client's indexed-mesh GPU gate moved
to its own CI workflow so extracting Studio does not remove that acceptance check.

The dependency remains source-owned by the client repository. Studio's solution builds its
four shared library projects, not the game applications or historical Studio copies present
in the pinned snapshot. The submodule was clean after validation. After the paired extraction
is merged, advance the pin to the removal commit through a reviewed integration update.

Current runtime formats and authoring limits were not changed. Map editing, the paired build
manifest/server export adapter, stable import identity migration, versioned default team projects,
streaming, and safe revision compaction remain implementation milestones. This checkpoint is a
working repository extraction and an audited content plan, not final production certification.
