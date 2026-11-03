using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

internal static class WorldSceneryDrawList
{
    internal static IReadOnlyList<WorldScenerySprite> Build(GameMapContent map,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain,
        WorldSceneryIndex scenery,
        IReadOnlyDictionary<GameMapSceneryRecord, (int Width, int Height)> frameExtents,
        WorldPuzzleProjection projection, uint nowMilliseconds, uint sceneStartMilliseconds) =>
        BuildCore(map, terrain, scenery, frameExtents, projection, nowMilliseconds,
            sceneStartMilliseconds, null, out _);

    internal static IReadOnlyList<WorldScenerySprite> BuildWithRole(GameMapContent map,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain,
        WorldSceneryIndex scenery,
        IReadOnlyDictionary<GameMapSceneryRecord, (int Width, int Height)> frameExtents,
        WorldPuzzleProjection projection, uint nowMilliseconds, uint sceneStartMilliseconds,
        int tileX, int tileY, out int roleIndex) =>
        BuildCore(map, terrain, scenery, frameExtents, projection, nowMilliseconds,
            sceneStartMilliseconds, (tileX, tileY), out roleIndex);

    private static IReadOnlyList<WorldScenerySprite> BuildCore(GameMapContent map,
        IReadOnlyList<GameMapTerrainScenePlacement> terrain,
        WorldSceneryIndex scenery,
        IReadOnlyDictionary<GameMapSceneryRecord, (int Width, int Height)> frameExtents,
        WorldPuzzleProjection projection, uint nowMilliseconds, uint sceneStartMilliseconds,
        (int X, int Y)? roleTile, out int roleIndex)
    {
        WorldSceneryDepthOrder order = new();
        foreach (GameMapSceneryAniBinding binding in scenery.Collect(projection))
        {
            AppendAnimated(map, binding, frameExtents, projection, nowMilliseconds, sceneStartMilliseconds, order);
        }

        foreach (GameMapTerrainScenePlacement placement in terrain)
        {
            int groupX = placement.Scenery.TileX!.Value;
            int groupY = placement.Scenery.TileY!.Value;
            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                GameMapTerrainObject item = binding.Terrain;
                int tileX = checked(groupX + item.AnchorOffsetX);
                int tileY = checked(groupY + item.AnchorOffsetY);
                if (!IsVisible(map, projection, tileX, tileY, 8))
                {
                    continue;
                }

                (int worldX, int worldY) = WorldCameraProjection.ProjectTile(map, tileX, tileY);
                (int x, int y) = ProjectSprite(projection, worldX, worldY,
                    item.RenderOffsetX, item.RenderOffsetY);
                uint interval = (uint)Math.Max(1, item.FrameIntervalMilliseconds);
                int frameIndex = (int)((unchecked(nowMilliseconds - sceneStartMilliseconds) / interval) %
                    (uint)binding.Animation.FrameCount);
                order.Insert(new WorldScenerySprite(binding.Animation, frameIndex,
                    x, y, projection.ScaleNumerator, projection.ScaleDenominator,
                    tileX, tileY, item.GridWidth, item.GridHeight), terrain: true);
            }
        }

        if (roleTile is { } tile)
        {
            order.InsertRole(tile.X, tile.Y);
        }

        return order.ToDrawList(out roleIndex);
    }

    private static void AppendAnimated(GameMapContent map, GameMapSceneryAniBinding binding,
        IReadOnlyDictionary<GameMapSceneryRecord, (int Width, int Height)> frameExtents,
        WorldPuzzleProjection projection, uint nowMilliseconds, uint sceneStartMilliseconds,
        WorldSceneryDepthOrder order)
    {
        GameMapSceneryRecord record = binding.Scenery;
        GameMapSceneryAnimation animation = record.Animation ??
            throw new InvalidDataException("Animated scenery is missing its decoded fields.");
        int tileX = record.TileX!.Value;
        int tileY = record.TileY!.Value;
        int width = animation.FootprintWidth;
        int height = animation.FootprintHeight;
        int offsetX = animation.RenderOffsetX;
        int offsetY = animation.RenderOffsetY;
        int interval = animation.FrameIntervalMilliseconds;
        if (interval <= 0)
        {
            throw new InvalidDataException("Map scenery animation interval must be positive.");
        }

        int margin = 30;
        if (frameExtents.TryGetValue(record, out (int Width, int Height) extent))
        {
            long scaledWidth = (long)projection.ScaleNumerator * extent.Width / projection.ScaleDenominator;
            long scaledHeight = (long)projection.ScaleNumerator * extent.Height / projection.ScaleDenominator;
            margin = checked((int)Math.Max(scaledWidth / 64 + 9, scaledHeight / 32 + 9));
        }

        (int worldX, int worldY) = WorldCameraProjection.ProjectTile(map, tileX, tileY);
        (int offsetTileX, int offsetTileY) = ProjectToTile(map, worldX - offsetX, worldY - offsetY);
        if (!IsVisible(map, projection, offsetTileX, offsetTileY, margin) ||
            !IsVisible(map, projection, tileX, tileY, margin))
        {
            return;
        }

        (int x, int y) = ProjectSprite(projection, worldX, worldY, -offsetX, -offsetY);
        int frameIndex = (int)((unchecked(nowMilliseconds - sceneStartMilliseconds) / (uint)interval) %
            (uint)binding.Animation.FrameCount);
        order.Insert(new WorldScenerySprite(binding.Animation, frameIndex,
            x, y, projection.ScaleNumerator, projection.ScaleDenominator,
            tileX, tileY, width, height, record));
    }

    private static (int X, int Y) ProjectSprite(WorldPuzzleProjection projection,
        int worldX, int worldY, int offsetX, int offsetY)
    {
        long x = (long)worldX - projection.CenterX + offsetX;
        long y = (long)worldY - projection.CenterY + offsetY;
        x = (long)projection.ScaleNumerator * (x - projection.ScreenWidth / 2) /
            projection.ScaleDenominator + projection.ScreenWidth / 2;
        y = (long)projection.ScaleNumerator * (y - projection.ScreenHeight / 2) /
            projection.ScaleDenominator + projection.ScreenHeight / 2;
        return (checked((int)x), checked((int)y));
    }

    private static (int X, int Y) ProjectToTile(GameMapContent map, int worldX, int worldY)
    {
        double normalizedX = ((double)worldX - (long)map.CellGrid.Width * 32) / 64;
        double normalizedY = ((double)worldY - 16) / 32;
        int x = HalfStepRound(normalizedX + normalizedY);
        int y = HalfStepRound(normalizedY - normalizedX);
        return (Math.Clamp(x, 0, map.CellGrid.Width - 1), Math.Clamp(y, 0, map.CellGrid.Height - 1));
    }

    private static int HalfStepRound(double value)
    {
        int truncated = (int)value;
        return (int)(value + 0.5) > truncated ? truncated + 1 : truncated;
    }

    private static bool IsVisible(GameMapContent map, WorldPuzzleProjection projection,
        int tileX, int tileY, int margin)
    {
        (int worldX, int worldY) = WorldCameraProjection.ProjectTile(map, tileX, tileY);
        long x = (long)worldX - projection.CenterX;
        long y = (long)worldY - projection.CenterY;
        long marginX = (long)margin * 64 * 256 / projection.ScaleNumerator;
        long marginY = (long)margin * 32 * 256 / projection.ScaleNumerator;
        return x >= -marginX && x <= projection.ScreenWidth + marginX &&
            y >= -marginY && y <= projection.ScreenHeight + marginY;
    }
}
