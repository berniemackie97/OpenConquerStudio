# OpenConquerStudio

Content authoring, asset inspection, previews and deterministic builds for our games.
Studio is a separate application and repository. It is not a general-purpose game engine.

## Build and run

Install the .NET SDK pinned in `global.json`:

```sh
dotnet restore OpenConquerStudio.slnx --locked-mode
dotnet build OpenConquerStudio.slnx -c Release --no-restore
dotnet run --project src/OpenConquerStudio -c Release --no-build
```

Recorded source snapshots of Content and Rendering provide codecs and previews.
The Client and Launcher applications are not included or built. No server checkout is needed to run Studio.

## Open a game

- **Open client project**: select your OpenConquerClient checkout. **File → Client content folder** can attach an original source installation.
- **New project**: name your project, then choose its parent folder. Use **Assets** to import or create textures, atlases and animations.
- **Open project**: choose `project.ocstudio`. An explicit directory argument also works: `dotnet run --project src/OpenConquerStudio -- /path/to/game-project`.

The automatic connected workspace remains local and ignored. Use a normal project for team assets.
**Save** includes visible field edits; a separate Apply click is optional. Invalid fields stay visible until fixed or reverted with **Undo**. **Build** creates a new validated output under the project’s `Builds/` folder.

Source files are preserved. Map/puzzle inspection and map previews work. Map editing and complete
character composition remain in development. Compiled assets do not yet replace client resources automatically.

## C3 characters

1. Open your client project with local C3 assets installed. In **Client content**, filter **character** and select a body.
2. Set hair style if needed; **Preview character**, then **Import editable copy**.
3. In **Authored**, select the model. Choose a clip, play/scrub it, inspect bone matrix channels, and edit timing/repeat.
4. **Add C3 motion** attaches a compatible motion file. **Build** saves and writes a self-contained `.ocmodel`.
5. For map context, open a source map, return to the authored model, and choose **Preview on last map**.

**Assets → Import C3** accepts a mesh file. Catalog character import supplies actual textures;
raw files without texture bindings show untextured geometry. Original files remain unchanged.

[Usage](docs/studio/README.md) · [Asset and map plan](docs/studio/asset-platform-plan.md) ·
[Ownership and extraction](docs/studio/architecture.md)

## Verify

```sh
# Set OPENCONQUER_CLIENT_ROOT to your actual client checkout before integration checks.
dotnet test OpenConquerStudio.slnx -c Release --no-build --no-restore
python3 -m unittest discover -s tests/development -p test_studio_cli.py
dotnet run --project tools/OpenConquerStudio.RenderCheck -c Release --no-build --no-restore
```

Integration tests and RenderCheck read actual client content from `OPENCONQUER_CLIENT_ROOT`
(or `.fixtures/client` in CI). For example, in a POSIX shell: `export OPENCONQUER_CLIENT_ROOT=/path/to/OpenConquerClient`.
The last check needs local assets and working desktop OpenGL. Source-only CI reports unavailable
asset tests as skipped; it never downloads retail content from historical commits.
See [formats](docs/studio/formats.md) for existing saved assets and [extraction record](extraction.json) for source provenance.
Local checks and remaining gates: [workflow validation](docs/studio/workflow-validation-2026-11-03.md).
