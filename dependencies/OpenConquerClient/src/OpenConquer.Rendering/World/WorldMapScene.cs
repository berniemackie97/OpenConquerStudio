using System.Numerics;
using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Map;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Rendering.World;

internal readonly record struct WorldMapLayer(
    IReadOnlyList<WorldPuzzleTile>? Puzzle = null, IReadOnlyList<WorldScenerySprite>? Scenery = null);

public sealed class WorldMapScene
{
    private readonly GameMapContent _map;
    private readonly GameMapPuzzleResources _puzzles;
    private readonly IReadOnlyList<GameMapTerrainScenePlacement> _terrain;
    private readonly WorldSceneryIndex _sceneryIndex;
    private readonly Dictionary<GameMapSceneryRecord, (int Width, int Height)> _sceneryFrameExtents =
        new(ReferenceEqualityComparer.Instance);
    private readonly WorldPuzzleAnimationClock _groundClock;
    private readonly Dictionary<GameMapPuzzleOverlayEntry, WorldPuzzleAnimationClock> _overlayClocks =
        new(ReferenceEqualityComparer.Instance);
    private int _heroX;
    private int _heroY;
    private int _heroTileX;
    private int _heroTileY;
    private int _roleDrawIndex;
    private int _zoomPercent = 100;
    private readonly uint _startMilliseconds;
    private readonly LogicalRenderSize _backdropExtent;
    private IReadOnlyList<WorldMapLayer> _layers = [];
    private WorldPuzzleProjection _projection;

    internal WorldMapScene(GameMapContent map, GameMapPuzzleResources puzzles,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain,
        IReadOnlyList<GameMapSceneryAniBinding> animatedScenery,
        int heroTileX, int heroTileY, uint puzzleIntervalMilliseconds, uint startMilliseconds)
    {
        _map = map;
        _puzzles = puzzles;
        _terrain = terrain;
        _sceneryIndex = new WorldSceneryIndex(map, animatedScenery);
        (_heroX, _heroY) = WorldCameraProjection.ProjectTile(map, heroTileX, heroTileY);
        (_heroTileX, _heroTileY) = (heroTileX, heroTileY);
        _groundClock = new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds);
        _startMilliseconds = startMilliseconds;
        int backdropWidth = checked(puzzles.GroundColumns * 256);
        int backdropHeight = checked(puzzles.GroundRows * 256);
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    _overlayClocks.Add(puzzle, new WorldPuzzleAnimationClock(puzzleIntervalMilliseconds));
                    if (record.InsertIndex == 0)
                    {
                        GameMapPuzzleBitmap bitmap = puzzles.GetRequired(puzzle.PuzzlePath);
                        backdropWidth = Math.Min(backdropWidth, checked(bitmap.Columns * 256));
                        backdropHeight = Math.Min(backdropHeight, checked(bitmap.Rows * 256));
                    }
                }
            }
        }

        _backdropExtent = new LogicalRenderSize(backdropWidth, backdropHeight);
    }

    public LogicalRenderSize BackdropExtent => _backdropExtent;
    public GameMapContent Map => _map;

    public (Matrix4x4 Main, Matrix4x4 Shadow) HeroMatrices =>
        WorldRoleCameraMatrices.Create(_map, _puzzles, _projection,
            _heroX, _heroY, rotationDegrees: -45f, scale: 0.75f);

    public void SetCameraTile(int tileX, int tileY)
    {
        if ((uint)tileX >= (uint)_map.CellGrid.Width || (uint)tileY >= (uint)_map.CellGrid.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(tileX), "The camera tile must lie within the map grid.");
        }
        (_heroX, _heroY) = WorldCameraProjection.ProjectTile(_map, tileX, tileY);
        (_heroTileX, _heroTileY) = (tileX, tileY);
    }

    public void SetZoomPercent(int zoomPercent)
    {
        if (zoomPercent is < 10 or > 800)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomPercent), "Map zoom must be between 10 and 800 percent.");
        }
        _zoomPercent = zoomPercent;
    }

    public static WorldMapScene Load(IClientContentSource source, GameMapCatalog catalog,
        uint mapDataId, ushort heroTileX, ushort heroTileY, uint startMilliseconds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        return Load(source, map, heroTileX, heroTileY, startMilliseconds, cancellationToken);
    }

    public static WorldMapScene Load(IClientContentSource source, GameMapContent map,
        ushort cameraTileX, ushort cameraTileY, uint startMilliseconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        cancellationToken.ThrowIfCancellationRequested();
        AniSectionResolver animations = new(source, ContentLookupMode.LooseOnly);
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map, animations);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<GameMapSceneryAniBinding> scenery = GameMapSceneryAniBindings.Load(source, map.Scene, animations);
        cancellationToken.ThrowIfCancellationRequested();
        uint puzzleInterval = PuzzleFrameIntervalConfiguration.Load(source);
        cancellationToken.ThrowIfCancellationRequested();
        return new WorldMapScene(map, puzzles, terrain.Placements, scenery, cameraTileX, cameraTileY,
            puzzleInterval, startMilliseconds);
    }

    internal IReadOnlyList<WorldMapLayer> BuildFrame(int screenWidth, int screenHeight, uint nowMilliseconds)
    {
        _projection = WorldCameraProjection.FromHeroWorldPosition(_map, _puzzles,
            _heroX, _heroY, screenWidth, screenHeight, zoomPercent: _zoomPercent);

        IReadOnlyList<WorldPuzzleTile> ground = WorldPuzzleDrawList.Ground(
            _map, _puzzles, _projection, _groundClock.FrameForRender(nowMilliseconds));
        IReadOnlyList<WorldScenerySprite> scenery = WorldSceneryDrawList.BuildWithRole(
            _map, _terrain, _sceneryIndex, _sceneryFrameExtents,
            _projection, nowMilliseconds, _startMilliseconds,
            _heroTileX, _heroTileY, out _roleDrawIndex);

        List<WorldMapLayer> layers =
        [
            new(Puzzle: ground),
            default,
            default,
            new(Scenery: scenery),
            default,
        ];

        foreach (GameMapPositionedOverlayRecord record in _map.Scene.PositionedOverlays)
        {
            List<WorldPuzzleTile> puzzleTiles = [];
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is not GameMapPuzzleOverlayEntry puzzle)
                {
                    continue;
                }

                uint frame = _overlayClocks[puzzle].FrameForRender(nowMilliseconds);
                puzzleTiles.AddRange(WorldPuzzleDrawList.PositionedPuzzle(
                    _map, _puzzles, _projection, record, puzzle, frame));
            }

            layers.Insert(Math.Clamp(record.InsertIndex, 0, layers.Count), new(Puzzle: puzzleTiles));
        }

        _layers = layers;
        return layers;
    }

    public bool Prepare(WorldMapTextureRenderer textures, int screenWidth, int screenHeight, uint nowMilliseconds,
        int textureBudget = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textureBudget);
        BuildFrame(screenWidth, screenHeight, nowMilliseconds);
        int remaining = textureBudget;
        bool complete = true;
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle && !textures.Prepare(puzzle, ref remaining))
            {
                complete = false;
            }
            if (layer.Scenery is { } scenery)
            {
                if (!textures.Prepare(scenery, ref remaining))
                {
                    complete = false;
                }

                foreach (WorldScenerySprite sprite in scenery)
                {
                    if (sprite.Source is { } record && !_sceneryFrameExtents.ContainsKey(record) &&
                        textures.TryGetFrameExtent(sprite.Animation.FramePaths[sprite.FrameIndex],
                            out int width, out int height))
                    {
                        _sceneryFrameExtents.Add(record, (width, height));
                    }
                }
            }
        }

        return complete;
    }

    public void Draw(WorldMapTextureRenderer textures, OpenGLRenderer renderer,
        Action<OpenGLRenderer>? drawRole = null)
    {
        foreach (WorldMapLayer layer in _layers)
        {
            if (layer.Puzzle is { } puzzle)
            {
                textures.Draw(renderer, puzzle, SpriteColor.White);
            }

            if (layer.Scenery is { } scenery)
            {
                textures.Draw(renderer, scenery, _projection, SpriteColor.White, 0, _roleDrawIndex);
                drawRole?.Invoke(renderer);
                textures.Draw(renderer, scenery, _projection, SpriteColor.White,
                    _roleDrawIndex, scenery.Count - _roleDrawIndex);
            }
        }
    }
}
