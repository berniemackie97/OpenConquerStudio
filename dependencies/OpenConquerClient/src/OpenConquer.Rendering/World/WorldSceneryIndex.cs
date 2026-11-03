using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

internal sealed class WorldSceneryIndex
{
    private const int BucketPixels = 256;
    private readonly Dictionary<(int X, int Y), List<GameMapSceneryAniBinding>> _buckets = [];

    internal WorldSceneryIndex(GameMapContent map, IReadOnlyList<GameMapSceneryAniBinding> bindings)
    {
        foreach (GameMapSceneryAniBinding binding in bindings)
        {
            int tileX = binding.Scenery.TileX!.Value;
            int tileY = binding.Scenery.TileY!.Value;
            (int x, int y) = WorldCameraProjection.ProjectTile(map, tileX, tileY);
            (int X, int Y) bucket = (x / BucketPixels, y / BucketPixels);
            if (!_buckets.TryGetValue(bucket, out List<GameMapSceneryAniBinding>? records))
            {
                records = [];
                _buckets.Add(bucket, records);
            }

            records.Add(binding);
        }
    }

    internal IEnumerable<GameMapSceneryAniBinding> Collect(WorldPuzzleProjection projection)
    {
        int centerX = (projection.CenterX + projection.ScreenWidth / 2) / BucketPixels;
        int centerY = (projection.CenterY + projection.ScreenHeight / 2) / BucketPixels;
        int radiusX = (projection.ScreenWidth / BucketPixels + 3) / 2;
        int radiusY = (projection.ScreenHeight / BucketPixels + 3) / 2;

        for (int x = Math.Max(centerX - radiusX, 0); x <= centerX + radiusX; x++)
        {
            for (int y = Math.Max(centerY - radiusY, 0); y <= centerY + radiusY; y++)
            {
                if (_buckets.TryGetValue((x, y), out List<GameMapSceneryAniBinding>? records))
                {
                    foreach (GameMapSceneryAniBinding record in records)
                    {
                        yield return record;
                    }
                }
            }
        }
    }
}
