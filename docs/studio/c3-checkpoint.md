# C3 authoring checkpoint, November 3, 2026

The public standalone repository owns the Studio desktop workflow. Client remains unchanged by
this slice. The Content/Rendering source copies record their original commit plus Studio-owned
changes in `dependencies/OpenConquerClient/source.json`.

Implemented: catalog body/hair import, standalone C3 mesh import, clip timing and repeat edits,
compatible additional motion import, play/scrub, matrix-channel inspection, standalone/map GPU
preview, revision save/reopen, undo/redo and deterministic self-contained compilation/loading.
Original source bytes remain intact. The README contains the normal user workflow.

Native contracts: WORLD-021/022 and `20_notes/Rendering/c3_core_dll/05_phy.md`,
`12_motion_load_formats.md`, `13_motion_sampling_and_blend.md`, and
`role3d_dll/12_linked_motion_chain_resolution.md`. No new native finding was made by this slice.

Remaining: mesh/bone/keyframe editing, complete equipment and role variants, material/key/effect
playback, map authoring, private asset provisioning and automatic game adoption of compiled builds.
This checkpoint does not claim final production certification or complete native rendering parity.
