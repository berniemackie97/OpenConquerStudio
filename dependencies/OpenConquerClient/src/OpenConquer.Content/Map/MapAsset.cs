namespace OpenConquer.Content.Map;

public abstract record MapObject;

public sealed record MapTerrainObject(string SceneAssetId, int TileX, int TileY) : MapObject;

public sealed record MapAnimatedObject(string AnimationAssetId, string SectionName, int TileX, int TileY) : MapObject;

public abstract record MapLayerEntry;

public sealed record MapPuzzleLayerEntry(string PuzzleAssetId) : MapLayerEntry;

public sealed record MapAnimationLayerEntry(string AnimationAssetId, string SectionName,
    int TileX, int TileY, int OffsetX, int OffsetY, int FrameIntervalMilliseconds,
    int DrawParameter) : MapLayerEntry;

public sealed record MapLayer(int InsertIndex, int ParallaxXPercent, int ParallaxYPercent,
    IReadOnlyList<MapLayerEntry> Entries);

public sealed record MapAsset(uint MapDataId, string GroundPuzzleAssetId, int Width, int Height,
    IReadOnlyList<GameMapCell> Cells, IReadOnlyList<MapObject> Objects,
    IReadOnlyList<MapLayer> Layers);
