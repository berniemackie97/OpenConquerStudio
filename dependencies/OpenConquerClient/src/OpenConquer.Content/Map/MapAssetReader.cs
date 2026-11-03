namespace OpenConquer.Content.Map;

public sealed class MapAssetReader : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly MapSectionEntry[] _sections;

    public uint MapDataId
    {
        get;
    }

    public MapAssetReader(Stream source, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        _stream = source;
        _leaveOpen = leaveOpen;
        (MapDataId, _sections) = MapAssetFormat.ReadHeader(source);
    }

    public MapAsset Read()
    {
        string[] references = ReadReferences();
        (string puzzle, int width, int height, GameMapCell[] cells) = ReadCells(references);
        MapObject[] objects = ReadObjects(references);
        MapLayer[] layers = ReadLayers(references);
        return new MapAsset(MapDataId, puzzle, width, height,
            Array.AsReadOnly(cells), Array.AsReadOnly(objects), Array.AsReadOnly(layers));
    }

    public GameMapCell[] ReadCellWindow(int x, int y, int width, int height)
    {
        string[] references = ReadReferences();
        MapSectionEntry section = Verify(MapSection.Cells);
        using BinaryReader reader = new(_stream, MapAssetFormat.Utf8, leaveOpen: true);
        long end = section.Offset + section.Length;
        (int columns, int rows) = ReadCellHeader(reader, end, references.Length);
        if (x < 0 || y < 0 || width <= 0 || height <= 0 ||
            (long)x + width > columns || (long)y + height > rows ||
            (long)width * height > MapAssetFormat.MaxCells)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Requested map cell window is outside the grid.");
        }

        long cellsStart = reader.BaseStream.Position;
        GameMapCell[] result = new GameMapCell[width * height];
        for (int row = 0; row < height; row++)
        {
            reader.BaseStream.Position = cellsStart + ((long)(y + row) * columns + x) * 6;
            for (int col = 0; col < width; col++)
            {
                result[row * width + col] = new GameMapCell(
                    reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt16());
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private string[] ReadReferences() => Parse(MapSection.References, (reader, end) =>
    {
        int count = MapAssetFormat.ReadCount(reader, end,
            MapAssetFormat.MaxReferences, 2, "reference");
        string[] ids = new string[count];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = MapAssetFormat.ReadString(reader, end);
            try
            {
                MapAssetId.Validate(ids[i]);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Map asset contains a noncanonical reference.", exception);
            }

            if (i > 0 && string.CompareOrdinal(ids[i - 1], ids[i]) >= 0)
            {
                throw new InvalidDataException("Map asset reference directory is not strictly sorted.");
            }
        }

        return ids;
    });

    private (string Puzzle, int Width, int Height, GameMapCell[] Cells) ReadCells(string[] references) =>
        Parse(MapSection.Cells, (reader, end) =>
        {
            MapAssetFormat.Require(reader, end, 12);
            int ground = reader.ReadInt32();
            (int width, int height) = ReadCellDimensions(reader, end);
            string puzzle = Resolve(references, ground);
            int count = MapAssetFormat.CellCount(width, height);
            GameMapCell[] cells = new GameMapCell[count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new GameMapCell(reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadInt16());
            }

            return (puzzle, width, height, cells);
        });

    private static (int Width, int Height) ReadCellHeader(BinaryReader reader, long end, int referenceCount)
    {
        MapAssetFormat.Require(reader, end, 12);
        int id = reader.ReadInt32();
        if ((uint)id >= (uint)referenceCount)
        {
            throw new InvalidDataException("Map ground references an unknown asset.");
        }

        return ReadCellDimensions(reader, end);
    }

    private static (int Width, int Height) ReadCellDimensions(BinaryReader reader, long end)
    {
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int count = MapAssetFormat.CellCount(width, height);
        if ((long)count * 6 != end - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Map grid contains an inconsistent number of cells.");
        }

        return (width, height);
    }

    private MapObject[] ReadObjects(string[] references) =>
        Parse(MapSection.Objects, (reader, end) =>
        {
            int count = MapAssetFormat.ReadCount(reader, end, MapAssetFormat.MaxObjects, 1, "object");
            MapObject[] objects = new MapObject[count];
            for (int i = 0; i < objects.Length; i++)
            {
                MapAssetFormat.Require(reader, end, 5);
                byte kind = reader.ReadByte();
                string id = Resolve(references, reader.ReadInt32());
                objects[i] = kind switch
                {
                    1 => ReadTerrain(reader, end, id),
                    2 => ReadAnimated(reader, end, id),
                    _ => throw new InvalidDataException("Map contains an unknown scene object."),
                };
            }

            return objects;
        });

    private MapLayer[] ReadLayers(string[] references) =>
        Parse(MapSection.Layers, (reader, end) =>
        {
            int count = MapAssetFormat.ReadCount(reader, end, MapAssetFormat.MaxLayers, 16, "layer");
            MapLayer[] layers = new MapLayer[count];
            for (int i = 0; i < layers.Length; i++)
            {
                MapAssetFormat.Require(reader, end, 16);
                int insert = reader.ReadInt32();
                int parallaxX = reader.ReadInt32();
                int parallaxY = reader.ReadInt32();
                int children = MapAssetFormat.ReadCount(reader, end, MapAssetFormat.MaxEntries, 1, "layer entry");
                MapLayerEntry[] items = new MapLayerEntry[children];
                for (int j = 0; j < items.Length; j++)
                {
                    MapAssetFormat.Require(reader, end, 5);
                    byte kind = reader.ReadByte();
                    string id = Resolve(references, reader.ReadInt32());
                    items[j] = kind switch
                    {
                        1 => new MapPuzzleLayerEntry(id),
                        2 => ReadLayerAnimation(reader, end, id),
                        _ => throw new InvalidDataException("Map contains an unknown layer entry."),
                    };
                }

                layers[i] = new MapLayer(insert, parallaxX, parallaxY, Array.AsReadOnly(items));
            }

            return layers;
        });

    private static MapTerrainObject ReadTerrain(BinaryReader reader, long end, string id)
    {
        MapAssetFormat.Require(reader, end, 8);
        return new MapTerrainObject(id, reader.ReadInt32(), reader.ReadInt32());
    }

    private static MapAnimatedObject ReadAnimated(BinaryReader reader, long end, string id)
    {
        string section = MapAssetFormat.ReadString(reader, end);
        MapAssetFormat.Require(reader, end, 8);
        return new MapAnimatedObject(id, section, reader.ReadInt32(), reader.ReadInt32());
    }

    private static MapAnimationLayerEntry ReadLayerAnimation(BinaryReader reader, long end, string id)
    {
        string section = MapAssetFormat.ReadString(reader, end);
        MapAssetFormat.Require(reader, end, 24);
        return new MapAnimationLayerEntry(id, section, reader.ReadInt32(), reader.ReadInt32(),
            reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }

    private T Parse<T>(MapSection name, Func<BinaryReader, long, T> parse)
    {
        MapSectionEntry section = Verify(name);
        using BinaryReader reader = new(_stream, MapAssetFormat.Utf8, leaveOpen: true);
        long end = section.Offset + section.Length;
        T result = parse(reader, end);
        if (_stream.Position != end)
        {
            throw new InvalidDataException("Map asset section contains trailing or missing data.");
        }

        return result;
    }

    private MapSectionEntry Verify(MapSection name)
    {
        MapSectionEntry section = _sections[(int)name - 1];
        byte[] actual = MapAssetFormat.HashSection(_stream, section.Offset, section.Length);
        if (!actual.AsSpan().SequenceEqual(section.Digest))
        {
            throw new InvalidDataException("Map asset section digest mismatch.");
        }

        _stream.Position = section.Offset;
        return section;
    }

    private static string Resolve(string[] references, int index)
    {
        if ((uint)index >= (uint)references.Length)
        {
            throw new InvalidDataException("Map asset references an undefined resource.");
        }

        return references[index];
    }
}
