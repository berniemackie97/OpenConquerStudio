# C3 checkpoint, November 3, 2026

Client PR [#216](https://github.com/berniemackie97/OpenConquerClient/pull/216) contains the
Content/Rendering C3 compatibility resource foundation and removes the tracked local content set.
The Studio source snapshot now matches that PR commit; `source.json` records every included hash.

Studio's local authoring/runtime changes add model import, timing edits, revision save/reopen,
undo/redo, deterministic compilation and compiled model loading APIs. These changes remain local
to this standalone repository. The existing desktop client connection is unchanged: character
import controls, bone inspection and animation playback UI are not connected yet.

The resource loader now references Content for model decoding. It still excludes the editor,
game application, networking and GPU assemblies.

Continue in the Studio repository. Use the existing Content model readers and Rendering preview
host; do not add another shared assembly. Keep native formats, matrix sampling and linked-part
semantics tied to `20_notes`. Source retention does not claim full render parity or freeze the
future native mesh/rig/material authoring schema.
