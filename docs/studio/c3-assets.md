# C3 asset contract

Studio saves a compatibility resource with an editable name, clips and explicit native part/motion
bindings. Complete original C3, texture and catalog bytes accompany that metadata. Unsupported
source fields are retained, including unknown sections, transient PHY regions, serialized count
pairs, source encodings, key arrays and optional STEP/2SID/BIL* tags. Retention does not imply
that every retained effect is rendered by the current preview.

The current workflow uses the existing Content readers, full-matrix sampler and CPU skinner,
then the existing Rendering indexed-mesh draw. No second decoder or rendering assembly exists.

## Container

The 48-byte little-endian header contains magic `OCM3`, container version, JSON byte length,
payload byte length and 32-byte SHA-256 of JSON plus payload. Metadata version must match the
header. Sorted source records contain virtual path, length and SHA-256; exact bytes follow in
that order. No timestamps, machine paths or random build values are added. Hashes establish
integrity, not publisher authenticity.

Version 2 contains the model GUID, native part descriptors, ordered clips, virtual mesh names,
source records and part bindings. Part bindings identify child/parent part ordinals, parent
submesh identifier and a row-major 16-float local matrix (empty means identity). Clips declare
name, primary motion path, frame milliseconds, repeat and optional per-part motion-path overrides.
The character source association records appearance, hair style and the chosen body/headwear catalog IDs.
Part ordinals are stable within this resource; the editor does not reorder or delete parts.
Version 1 loads with identity locals and no ancestor/per-part overrides. Edits write version 2.

## Native semantics

- PHY vertices, normals, UVs, raw two-influence indices/weights, object matrices and 16-bit indices
  use the existing readers. Weights are not normalized into another convention.
- MOTI KKEY/ZKEY/XKEY and dense source bytes remain intact. Sampling uses full 4×4 matrices and
  element-wise interpolation; no TRS decomposition or destructive resampling occurs.
- MOTI tracks bind by ordinal submesh position. Additional external motion tracks can exist,
  but missing required tracks fail validation. Adding motion does not retarget a rig.
- Virtual names such as `v_armet` remain identifiers. A child draw samples bone zero of each
  ancestor anchor, transforms that translation by the ancestor's part-local matrix, and
  pre-multiplies the result into the accumulated world matrix. The child's local matrix precedes
  that chain. The separate transform-query target leg is not inserted into drawing.
- Matrix channels do not establish a parented bone hierarchy. The inspector shows channels and
  channel origins. Only explicit linked-part ancestry is stored and rendered.
- Body/initial-idle selection and 66 ms frames follow WORLD-021. Optional hair selection follows
  WORLD-022. Other action selectors and role variants are not guessed from those formulas.

## Validation and limits

128 source files and 64 MiB payload per resource; four parts, 64 clips, 16 motion tracks per
source, one million vertices, one million matrices and one million key indexes. JSON is at most
1 MiB. Allocation budgets are checked before expanding motion matrices/indexes. Vertex attributes,
matrices and supported frame positions must be finite/in range. Motion keys must be present and
strictly ordered. Required files, motion coverage and attachment anchors must resolve; cyclic or
ambiguous links fail. No virtual path is extracted to disk during runtime loading.

A project permits 128 MiB model payloads. Immutable revisions and the root index provide the same
save boundary as other Studio assets. Compile/load checks identity, payload digest and declared
coverage. New output directories are staged before publication. No client installation is needed
when reopening a saved model or loading its compiled resource.

## Boundaries

The current preview supports body and linked hair, clip sampling, matrix inspection, source map
camera/depth integration and geometric shadows. Primary body/headwear catalog overrides are supported. Full equipment/garment/weapon/mount composition,
material blending, PHY alpha/draw/texture keys, UV steps, billboards and effects remain incomplete.
The source container preserves those fields for subsequent implementation.

This is not a frozen universal mesh/rig format. Future native authoring for new games must add
editable geometry, rigs and materials without discarding these semantics. Promotion requires
real-corpus coverage, render comparisons, profiling and a versioned migration path.
