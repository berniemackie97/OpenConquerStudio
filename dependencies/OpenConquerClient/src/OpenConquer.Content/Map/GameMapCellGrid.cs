using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

public readonly record struct GameMapCell(ushort SurfaceId, ushort PassabilityFlag, short Elevation);

/// <summary>The initial cell-grid section of a decoded retail DMap, before exit markers and scenery.</summary>
public sealed class GameMapCellGrid
{
    private const int HeaderLength = 0x114;
    private const int PuzzlePathFieldLength = 0x104;
    private const int CellLength = 6;
    private const int MaximumCellCount = 4 * 1024 * 1024;

    private readonly GameMapCell[] _cells;

    private GameMapCellGrid(uint headerField0, uint headerField1, string puzzlePath, int width, int height,
        GameMapCell[] cells, int bytesConsumed)
    {
        HeaderField0 = headerField0;
        HeaderField1 = headerField1;
        PuzzlePath = puzzlePath;
        Width = width;
        Height = height;
        _cells = cells;
        BytesConsumed = bytesConsumed;
    }

    public uint HeaderField0
    {
        get;
    }

    public uint HeaderField1
    {
        get;
    }

    public string PuzzlePath
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public int BytesConsumed
    {
        get;
    }

    public GameMapCell GetCell(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _cells[y * Width + x];
    }

    public static GameMapCellGrid Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            throw new InvalidDataException("DMap cell-grid header is truncated.");
        }

        uint headerField0 = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint headerField1 = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);

        ReadOnlySpan<byte> pathField = data.Slice(8, PuzzlePathFieldLength);
        int pathLength = pathField.IndexOf((byte)0);
        if (pathLength <= 0)
        {
            throw new InvalidDataException("DMap puzzle path is missing or unterminated.");
        }

        string puzzlePath;
        try
        {
            puzzlePath = ClientContentPath.NormalizeVirtualPath(
                Encoding.Latin1.GetString(pathField[..pathLength]), nameof(data), PuzzlePathFieldLength);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("DMap puzzle path is invalid.", exception);
        }

        int width = BinaryPrimitives.ReadInt32LittleEndian(data[268..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(data[272..]);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("DMap cell-grid dimensions must be positive.");
        }

        long cellCount = (long)width * height;
        if (cellCount > MaximumCellCount)
        {
            throw new InvalidDataException("DMap cell grid exceeds the supported cell limit.");
        }

        long rowLength = (long)width * CellLength + sizeof(uint);
        long remainingBytes = data.Length - HeaderLength;
        if (rowLength > remainingBytes || height > remainingBytes / rowLength)
        {
            throw new InvalidDataException("DMap cell grid exceeds its decoded payload.");
        }

        GameMapCell[] cells = new GameMapCell[(int)cellCount];
        int offset = HeaderLength;

        for (int y = 0; y < height; y++)
        {
            uint checksum = 0;
            for (int x = 0; x < width; x++)
            {
                ushort passabilityFlag = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
                ushort surfaceId = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
                short elevation = BinaryPrimitives.ReadInt16LittleEndian(data[(offset + 4)..]);
                offset += CellLength;

                cells[y * width + x] = new GameMapCell(surfaceId, passabilityFlag, elevation);
                checksum = unchecked(checksum
                    + ((uint)surfaceId + (uint)y + 1) * passabilityFlag
                    + ((uint)surfaceId + (uint)x + 1) * (uint)(elevation + 2));
            }

            uint expectedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
            offset += sizeof(uint);
            if (checksum != expectedChecksum)
            {
                throw new InvalidDataException($"DMap cell-grid row {y} has an invalid checksum.");
            }
        }

        return new GameMapCellGrid(headerField0, headerField1, puzzlePath, width, height, cells, offset);
    }
}
