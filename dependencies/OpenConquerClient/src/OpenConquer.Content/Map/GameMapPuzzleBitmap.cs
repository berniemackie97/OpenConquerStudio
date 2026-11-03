using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

/// <summary>PUZZLE/PUZZLE2 tile indices used by the retail map's ground and positioned-overlay layers.</summary>
public sealed class GameMapPuzzleBitmap
{
    private const int HeaderLength = 272;
    private const int MaximumFileLength = 16 * 1024 * 1024;
    private readonly ushort[] _tiles;

    private GameMapPuzzleBitmap(string textureIndexPath, int columns, int rows, ushort[] tiles,
        int scrollVelocityX, int scrollVelocityY)
    {
        TextureIndexPath = textureIndexPath;
        Columns = columns;
        Rows = rows;
        _tiles = tiles;
        ScrollVelocityX = scrollVelocityX;
        ScrollVelocityY = scrollVelocityY;
    }

    public string TextureIndexPath
    {
        get;
    }

    public int Columns
    {
        get;
    }

    public int Rows
    {
        get;
    }

    public ReadOnlySpan<ushort> Tiles => _tiles;

    public int ScrollVelocityX
    {
        get;
    }

    public int ScrollVelocityY
    {
        get;
    }

    public static GameMapPuzzleBitmap Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength || data.Length > MaximumFileLength)
        {
            throw new InvalidDataException("Puzzle bitmap size is invalid.");
        }

        bool scrolling = data[..8].SequenceEqual("PUZZLE2\0"u8);
        if (!scrolling && !data[..8].SequenceEqual("PUZZLE\0\0"u8))
        {
            throw new InvalidDataException("Puzzle bitmap signature is invalid.");
        }

        ReadOnlySpan<byte> pathField = data.Slice(8, 0x100);
        int pathLength = pathField.IndexOf((byte)0);
        if (pathLength <= 0)
        {
            throw new InvalidDataException("Puzzle bitmap has no terminated texture index path.");
        }

        string textureIndexPath;
        try
        {
            textureIndexPath = ClientContentPath.NormalizeVirtualPath(
                Encoding.Latin1.GetString(pathField[..pathLength]), nameof(data), 0x100);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Puzzle bitmap has an invalid texture index path.", exception);
        }

        int columns = BinaryPrimitives.ReadInt32LittleEndian(data[264..]);
        int rows = BinaryPrimitives.ReadInt32LittleEndian(data[268..]);
        if (columns <= 0 || rows <= 0)
        {
            throw new InvalidDataException("Puzzle bitmap dimensions must be positive.");
        }

        long count = (long)columns * rows;
        long requiredLength = HeaderLength + count * sizeof(ushort) + (scrolling ? 8 : 0);
        if (requiredLength != data.Length)
        {
            throw new InvalidDataException("Puzzle bitmap tile grid is truncated or contains trailing bytes.");
        }

        ushort[] tiles = new ushort[(int)count];
        ReadOnlySpan<byte> encodedTiles = data.Slice(HeaderLength, tiles.Length * sizeof(ushort));
        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = BinaryPrimitives.ReadUInt16LittleEndian(encodedTiles[(index * 2)..]);
        }

        int velocityX = 0;
        int velocityY = 0;
        if (scrolling)
        {
            int offset = HeaderLength + encodedTiles.Length;
            velocityX = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            velocityY = BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 4)..]);
        }

        return new GameMapPuzzleBitmap(textureIndexPath, columns, rows, tiles, velocityX, velocityY);
    }
}
