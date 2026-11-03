namespace OpenConquer.Content.Map;

internal static class MapAssetWriter
{
    public static void Write(MapAsset map, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanRead || !destination.CanWrite || !destination.CanSeek ||
            destination.Length != 0 || destination.Position != 0)
        {
            throw new ArgumentException("Map output must be an empty, readable, writable, seekable stream.", nameof(destination));
        }
        if (map.MapDataId == 0)
        {
            throw new InvalidDataException("Map asset ID must be positive.");
        }

        string[] references = CollectReferences(map);
        Dictionary<string, int> indices = references.Select((name, index) => (name, index))
            .ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        MapSectionEntry[] entries = new MapSectionEntry[MapAssetFormat.SectionCount];
        using BinaryWriter writer = new(destination, MapAssetFormat.Utf8, leaveOpen: true);
        writer.Write(new byte[MapAssetFormat.HeaderSize]);

        WriteSection(0, w => WriteReferences(w, references));
        WriteSection(1, w => WriteCells(w, map, indices));
        WriteSection(2, w => WriteObjects(w, map.Objects, indices));
        WriteSection(3, w => WriteLayers(w, map.Layers, indices));

        destination.Position = 0;
        MapAssetFormat.WriteHeader(writer, map.MapDataId, entries);
        writer.Flush();
        destination.Position = destination.Length;

        void WriteSection(int index, Action<BinaryWriter> write)
        {
            long start = destination.Position;
            write(writer);
            writer.Flush();
            long length = destination.Position - start;
            if (length <= 0 || destination.Length > MapAssetFormat.MaxBytes)
            {
                throw new InvalidDataException("Compiled map section exceeds its size limit.");
            }

            byte[] digest = MapAssetFormat.HashSection(destination, start, length);
            entries[index] = new MapSectionEntry(start, length, digest);
            destination.Position = start + length;
        }
    }

    private static string[] CollectReferences(MapAsset map)
    {
        SortedSet<string> ids = new(StringComparer.Ordinal);
        Add(map.GroundPuzzleAssetId);
        foreach (MapObject obj in map.Objects)
        {
            switch (obj)
            {
                case MapTerrainObject terrain:
                    Add(terrain.SceneAssetId);
                    break;
                case MapAnimatedObject animation:
                    Add(animation.AnimationAssetId);
                    break;
                default:
                    throw new InvalidDataException("Map object type is unsupported.");
            }
        }

        foreach (MapLayer layer in map.Layers)
        {
            foreach (MapLayerEntry item in layer.Entries)
            {
                switch (item)
                {
                    case MapPuzzleLayerEntry puzzle:
                        Add(puzzle.PuzzleAssetId);
                        break;
                    case MapAnimationLayerEntry animation:
                        Add(animation.AnimationAssetId);
                        break;
                    default:
                        throw new InvalidDataException("Map layer entry is unsupported.");
                }
            }
        }

        return [.. ids];

        void Add(string id)
        {
            MapAssetId.Validate(id);
            ids.Add(id);
            MapAssetFormat.CheckCount(ids.Count, MapAssetFormat.MaxReferences, "reference");
        }
    }

    private static void WriteReferences(BinaryWriter writer, string[] references)
    {
        writer.Write(references.Length);
        foreach (string reference in references)
        {
            MapAssetFormat.WriteString(writer, reference);
        }
    }

    private static void WriteCells(BinaryWriter writer, MapAsset map, Dictionary<string, int> ids)
    {
        int count = MapAssetFormat.CellCount(map.Width, map.Height);
        if (map.Cells.Count != count)
        {
            throw new InvalidDataException("Map cell count does not match dimensions.");
        }

        writer.Write(ids[map.GroundPuzzleAssetId]);
        writer.Write(map.Width);
        writer.Write(map.Height);
        foreach (GameMapCell cell in map.Cells)
        {
            writer.Write(cell.SurfaceId);
            writer.Write(cell.PassabilityFlag);
            writer.Write(cell.Elevation);
        }
    }

    private static void WriteObjects(BinaryWriter writer, IReadOnlyList<MapObject> objects, Dictionary<string, int> ids)
    {
        MapAssetFormat.CheckCount(objects.Count, MapAssetFormat.MaxObjects, "object");
        writer.Write(objects.Count);
        foreach (MapObject obj in objects)
        {
            switch (obj)
            {
                case MapTerrainObject terrain:
                    writer.Write((byte)1);
                    writer.Write(ids[terrain.SceneAssetId]);
                    writer.Write(terrain.TileX);
                    writer.Write(terrain.TileY);
                    break;
                case MapAnimatedObject animated:
                    writer.Write((byte)2);
                    writer.Write(ids[animated.AnimationAssetId]);
                    MapAssetFormat.WriteString(writer, animated.SectionName);
                    writer.Write(animated.TileX);
                    writer.Write(animated.TileY);
                    break;
                default:
                    throw new InvalidDataException("Map object type is unsupported.");
            }
        }
    }

    private static void WriteLayers(BinaryWriter writer, IReadOnlyList<MapLayer> layers, Dictionary<string, int> ids)
    {
        MapAssetFormat.CheckCount(layers.Count, MapAssetFormat.MaxLayers, "layer");
        writer.Write(layers.Count);
        foreach (MapLayer layer in layers)
        {
            writer.Write(layer.InsertIndex);
            writer.Write(layer.ParallaxXPercent);
            writer.Write(layer.ParallaxYPercent);
            MapAssetFormat.CheckCount(layer.Entries.Count, MapAssetFormat.MaxEntries, "layer entry");
            writer.Write(layer.Entries.Count);
            foreach (MapLayerEntry item in layer.Entries)
            {
                switch (item)
                {
                    case MapPuzzleLayerEntry puzzle:
                        writer.Write((byte)1);
                        writer.Write(ids[puzzle.PuzzleAssetId]);
                        break;
                    case MapAnimationLayerEntry animation:
                        writer.Write((byte)2);
                        writer.Write(ids[animation.AnimationAssetId]);
                        MapAssetFormat.WriteString(writer, animation.SectionName);
                        writer.Write(animation.TileX);
                        writer.Write(animation.TileY);
                        writer.Write(animation.OffsetX);
                        writer.Write(animation.OffsetY);
                        writer.Write(animation.FrameIntervalMilliseconds);
                        writer.Write(animation.DrawParameter);
                        break;
                    default:
                        throw new InvalidDataException("Map layer entry is unsupported.");
                }
            }
        }
    }
}
