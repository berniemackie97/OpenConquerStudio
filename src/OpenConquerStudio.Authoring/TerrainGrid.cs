using System.Buffers.Binary;

namespace OpenConquerStudio.Authoring;

public readonly record struct TerrainCell(ushort SurfaceId, ushort PassabilityFlag, short Elevation);

internal static class TerrainGrid
{
    internal const int MaximumCells = 262_144;
    internal const int MaximumBytes = MaximumCells * 6;

    internal static int CellCount(int width, int height)
    {
        long count = (long)width * height;
        if (width <= 0 || height <= 0 || width > 1024 || height > 1024 || count > MaximumCells)
        {
            throw new InvalidDataException("Terrain dimensions must be positive and fit the authored cell budget.");
        }

        return (int)count;
    }

    internal static int ByteLength(int width, int height) => CellCount(width, height) * 6;

    internal static byte[] Create(int width, int height, TerrainCell fill)
    {
        byte[] cells = new byte[ByteLength(width, height)];
        for (int offset = 0; offset < cells.Length; offset += 6)
        {
            Write(cells.AsSpan(offset, 6), fill);
        }

        return cells;
    }

    internal static TerrainCell GetCell(ReadOnlySpan<byte> cells, int width, int height, int x, int y)
    {
        int offset = CellOffset(cells.Length, width, height, x, y);
        return new TerrainCell(BinaryPrimitives.ReadUInt16LittleEndian(cells[offset..]),
            BinaryPrimitives.ReadUInt16LittleEndian(cells[(offset + 2)..]),
            BinaryPrimitives.ReadInt16LittleEndian(cells[(offset + 4)..]));
    }

    internal static void SetCell(Span<byte> cells, int width, int height, int x, int y, TerrainCell cell)
    {
        int offset = CellOffset(cells.Length, width, height, x, y);
        Write(cells.Slice(offset, 6), cell);
    }

    private static int CellOffset(int length, int width, int height, int x, int y)
    {
        if (length != ByteLength(width, height) || x < 0 || y < 0 || x >= width || y >= height)
        {
            throw new InvalidDataException("Terrain cell coordinates or payload length are invalid.");
        }

        return ((y * width) + x) * 6;
    }

    private static void Write(Span<byte> destination, TerrainCell cell)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, cell.SurfaceId);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], cell.PassabilityFlag);
        BinaryPrimitives.WriteInt16LittleEndian(destination[4..], cell.Elevation);
    }
}
