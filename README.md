# OpenConquerStudio

Content authoring, asset inspection, previews and deterministic builds for our games.
Studio is a separate application and repository. It is not a general-purpose game engine.

## Build and run

Install the .NET SDK pinned in `global.json`:

```sh
git submodule update --init --recursive
dotnet restore OpenConquerStudio.slnx --locked-mode
dotnet build OpenConquerStudio.slnx -c Release --no-restore
dotnet run --project src/OpenConquerStudio -c Release --no-build
```

The pinned client dependency provides existing codecs and preview rendering. It is read-only
here; the Client and Launcher applications are not built. No server checkout is needed to run Studio.

## Open a game

- **Open client project**: select your OpenConquerClient checkout. **Client content folder** can attach an original source installation.
- **New project**: choose an empty, version-controlled game-content directory. Import or create textures, atlases and sprite animations; save and build.
- **Open project**: choose a directory containing `project.ocstudio`. An explicit directory argument also works: `dotnet run --project src/OpenConquerStudio -- /path/to/game-project`.

The automatic connected workspace remains local and ignored. Use a normal project for team assets.
Source files are preserved. Map/puzzle inspection and map previews work; map editing and complete
character authoring are not implemented. Compiled assets do not yet replace client resources automatically.

[Usage](docs/studio/README.md) · [Asset and map plan](docs/studio/asset-platform-plan.md) ·
[Ownership and extraction](docs/studio/architecture.md)

## Verify

```sh
dotnet test OpenConquerStudio.slnx -c Release --no-build --no-restore
python3 -m unittest discover -s tests/development -p test_studio_cli.py
dotnet run --project tools/OpenConquerStudio.RenderCheck -c Release --no-build --no-restore
```

The last check needs working desktop OpenGL. CI uses Mesa on Linux; hosted macOS does not provide GPU interop.
See [formats](docs/studio/formats.md) for existing saved assets and [extraction record](extraction.json) for source provenance.
