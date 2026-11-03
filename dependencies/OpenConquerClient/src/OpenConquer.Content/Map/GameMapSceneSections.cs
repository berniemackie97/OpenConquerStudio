using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

public sealed record GameMapExitMarker(int Field0, int Field1, int Field2);

public sealed record GameMapSceneryAnimation(int FootprintWidth, int FootprintHeight,
    int RenderOffsetX, int RenderOffsetY, int FrameIntervalMilliseconds);

public sealed record GameMapSceneryRecord(int TypeCode, string? ResourcePath, string? PartName,
    int? TileX, int? TileY, ReadOnlyMemory<byte> EncodedPayload, GameMapSceneryAnimation? Animation = null);

public abstract record GameMapPositionedOverlayEntry;

public sealed record GameMapPuzzleOverlayEntry(string PuzzlePath) : GameMapPositionedOverlayEntry;

public sealed record GameMapAnimatedOverlayEntry(string ResourcePath, string PartName, int TileX, int TileY,
    int RenderOffsetX, int RenderOffsetY, int FrameIntervalMilliseconds, int AdditionalValue) : GameMapPositionedOverlayEntry;

public sealed record GameMapPositionedOverlayRecord(int InsertIndex, int HeaderInt0, int HeaderInt1,
    IReadOnlyList<GameMapPositionedOverlayEntry> Entries);

/// <summary>Post-grid DMap records in encoded file and component insertion order.</summary>
public sealed class GameMapSceneSections
{
    private const int MaximumDecodedMapBytes = 32 * 1024 * 1024;

    private GameMapSceneSections(GameMapExitMarker[] exits, GameMapSceneryRecord[] scenery,
        GameMapPositionedOverlayRecord[] overlays)
    {
        ExitMarkers = Array.AsReadOnly(exits);
        Scenery = Array.AsReadOnly(scenery);
        PositionedOverlays = Array.AsReadOnly(overlays);
    }

    public IReadOnlyList<GameMapExitMarker> ExitMarkers
    {
        get;
    }

    public IReadOnlyList<GameMapSceneryRecord> Scenery
    {
        get;
    }

    public IReadOnlyList<GameMapPositionedOverlayRecord> PositionedOverlays
    {
        get;
    }

    public static GameMapSceneSections Parse(ReadOnlySpan<byte> decodedMap, int gridEndOffset, uint headerField0)
    {
        if (decodedMap.Length > MaximumDecodedMapBytes || gridEndOffset < 0 || gridEndOffset > decodedMap.Length)
        {
            throw new InvalidDataException("DMap section boundaries exceed the supported decoded map size.");
        }

        Reader reader = new(decodedMap, gridEndOffset);

        int exitCount = reader.ReadCount(12);
        GameMapExitMarker[] exits = new GameMapExitMarker[exitCount];
        for (int i = 0; i < exits.Length; i++)
        {
            exits[i] = new GameMapExitMarker(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        int sceneryCount = reader.ReadCount(4 + 0x48);
        int spritePayloadLength = headerField0 == 1005 ? 0x1A4 : 0x1A0;
        GameMapSceneryRecord[] scenery = new GameMapSceneryRecord[sceneryCount];
        for (int i = 0; i < scenery.Length; i++)
        {
            int typeCode = reader.ReadInt32();
            int length = typeCode switch
            {
                1 => 0x10C,
                4 => spritePayloadLength,
                10 => 0x48,
                15 => 0x114,
                _ => throw new InvalidDataException($"DMap scenery type {typeCode} has no verified payload layout."),
            };

            ReadOnlySpan<byte> payload = reader.ReadBytes(length);
            string? resourcePath = typeCode is 1 or 4 ? ReadPath(payload[..0x104]) : null;
            string? partName = typeCode == 4 ? ReadText(payload.Slice(0x104, 0x80)) : null;
            int? x = typeCode switch
            {
                1 => BinaryPrimitives.ReadInt32LittleEndian(payload[0x104..]),
                4 => BinaryPrimitives.ReadInt32LittleEndian(payload[0x184..]),
                _ => null,
            };
            int? y = typeCode switch
            {
                1 => BinaryPrimitives.ReadInt32LittleEndian(payload[0x108..]),
                4 => BinaryPrimitives.ReadInt32LittleEndian(payload[0x188..]),
                _ => null,
            };
            GameMapSceneryAnimation? animation = typeCode == 4
                ? new GameMapSceneryAnimation(
                    BinaryPrimitives.ReadInt32LittleEndian(payload[0x18C..]),
                    BinaryPrimitives.ReadInt32LittleEndian(payload[0x190..]),
                    BinaryPrimitives.ReadInt32LittleEndian(payload[0x194..]),
                    BinaryPrimitives.ReadInt32LittleEndian(payload[0x198..]),
                    BinaryPrimitives.ReadInt32LittleEndian(payload[0x19C..]))
                : null;
            scenery[i] = new GameMapSceneryRecord(typeCode, resourcePath, partName,
                x, y, payload.ToArray(), animation);
        }

        int overlayCount = reader.ReadCount(8);
        GameMapPositionedOverlayRecord[] overlays = new GameMapPositionedOverlayRecord[overlayCount];
        for (int i = 0; i < overlays.Length; i++)
        {
            int insertIndex = reader.ReadInt32();
            int recordType = reader.ReadInt32();
            if (recordType != 4)
            {
                throw new InvalidDataException($"DMap positioned overlay record {recordType} has no verified payload layout.");
            }

            int headerInt0 = reader.ReadInt32();
            int headerInt1 = reader.ReadInt32();
            int childCount = reader.ReadCount(4 + 0x104);
            GameMapPositionedOverlayEntry[] children = new GameMapPositionedOverlayEntry[childCount];
            for (int j = 0; j < children.Length; j++)
            {
                int childType = reader.ReadInt32();
                children[j] = childType switch
                {
                    3 => ParseAnimatedOverlay(reader.ReadBytes(0x19C)),
                    8 => new GameMapPuzzleOverlayEntry(ReadPath(reader.ReadBytes(0x104))),
                    _ => throw new InvalidDataException($"DMap positioned overlay child {childType} has no verified payload layout."),
                };
            }

            overlays[i] = new GameMapPositionedOverlayRecord(insertIndex, headerInt0, headerInt1, Array.AsReadOnly(children));
        }

        if (!reader.AtEnd)
        {
            throw new InvalidDataException("DMap contains unconsumed bytes after the positioned overlay records.");
        }

        return new GameMapSceneSections(exits, scenery, overlays);
    }

    private static GameMapAnimatedOverlayEntry ParseAnimatedOverlay(ReadOnlySpan<byte> payload) =>
        new(
            ReadPath(payload[..0x104]),
            ReadText(payload.Slice(0x104, 0x80)),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x184..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x188..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x18C..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x190..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x194..]),
            BinaryPrimitives.ReadInt32LittleEndian(payload[0x198..]));

    private static string ReadPath(ReadOnlySpan<byte> field)
    {
        string path = ReadText(field);
        if (path.Length == 0)
        {
            throw new InvalidDataException("DMap references an empty resource path.");
        }

        try
        {
            return ClientContentPath.NormalizeVirtualPath(path, nameof(field), field.Length);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("DMap references an unsafe resource path.", exception);
        }
    }

    private static string ReadText(ReadOnlySpan<byte> field)
    {
        int length = field.IndexOf((byte)0);
        if (length < 0)
        {
            throw new InvalidDataException("DMap contains an unterminated resource string.");
        }

        return Encoding.Latin1.GetString(field[..length]);
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _source;
        private int _offset;

        public Reader(ReadOnlySpan<byte> source, int offset)
        {
            _source = source;
            _offset = offset;
        }

        public bool AtEnd => _offset == _source.Length;

        public int ReadCount(int minimumBytesPerRecord)
        {
            int count = ReadInt32();
            if (count < 0 || count > (_source.Length - _offset) / minimumBytesPerRecord)
            {
                throw new InvalidDataException("DMap record count exceeds the available payload.");
            }

            return count;
        }

        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (length < 0 || length > _source.Length - _offset)
            {
                throw new InvalidDataException("DMap record is truncated.");
            }

            ReadOnlySpan<byte> result = _source.Slice(_offset, length);
            _offset += length;
            return result;
        }
    }
}
