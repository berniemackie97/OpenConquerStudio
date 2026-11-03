using System.Buffers.Binary;
using System.Text;

namespace OpenConquer.Content.Map;

public readonly record struct GameMapTerrainCell(ushort PassabilityFlag, ushort SurfaceId, short ElevationDelta);

public sealed record GameMapTerrainObject(
    string AnimationPath, string SectionName,
    int RenderOffsetX, int RenderOffsetY, int FrameIntervalMilliseconds,
    int GridWidth, int GridHeight, uint AuthoredColor,
    int AnchorOffsetX, int AnchorOffsetY, int DrawParameter,
    IReadOnlyList<GameMapTerrainCell> Cells);

public sealed class GameMapTerrainSceneFile
{
    internal const int MaximumFileBytes = 1024 * 1024;

    private const int AnimationPathBytes = 0x100;
    private const int SectionNameBytes = 0x40;
    private const int ObjectFieldCount = 9;
    private const int ObjectHeaderBytes = AnimationPathBytes + SectionNameBytes + ObjectFieldCount * sizeof(int);
    private const int CellBytes = 3 * sizeof(int);

    private GameMapTerrainSceneFile(GameMapTerrainObject[] objects)
    {
        Objects = Array.AsReadOnly(objects);
    }

    public IReadOnlyList<GameMapTerrainObject> Objects
    {
        get;
    }

    public static GameMapTerrainSceneFile Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length < sizeof(int) || source.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("Terrain scene file length is invalid.");
        }

        SceneReader reader = new(source);
        int objectCount = reader.ReadInt32();
        if (objectCount < 0 || objectCount > reader.Remaining / ObjectHeaderBytes)
        {
            throw new InvalidDataException("Terrain scene object count exceeds the file.");
        }

        GameMapTerrainObject[] objects = new GameMapTerrainObject[objectCount];
        for (int index = 0; index < objects.Length; index++)
        {
            objects[index] = ReadObject(ref reader, index);
        }

        if (reader.Remaining != 0)
        {
            throw new InvalidDataException("Terrain scene file has trailing bytes.");
        }

        return new GameMapTerrainSceneFile(objects);
    }

    private static GameMapTerrainObject ReadObject(ref SceneReader reader, int index)
    {
        string animationPath = reader.ReadContentPath(AnimationPathBytes, index);
        string sectionName = reader.ReadName(SectionNameBytes, index);
        int renderOffsetX = reader.ReadInt32();
        int renderOffsetY = reader.ReadInt32();
        int frameInterval = reader.ReadInt32();
        int gridWidth = reader.ReadInt32();
        int gridHeight = reader.ReadInt32();
        uint authoredColor = unchecked((uint)reader.ReadInt32());
        int anchorOffsetX = reader.ReadInt32();
        int anchorOffsetY = reader.ReadInt32();
        int drawParameter = reader.ReadInt32();

        long cellCount = (long)gridWidth * gridHeight;
        if (gridWidth < 0 || gridHeight < 0 || cellCount > reader.Remaining / CellBytes)
        {
            throw new InvalidDataException($"Terrain scene object {index} has an invalid cell grid.");
        }

        GameMapTerrainCell[] cells = new GameMapTerrainCell[(int)cellCount];
        for (int cell = 0; cell < cells.Length; cell++)
        {
            ushort passability = unchecked((ushort)reader.ReadInt32());
            ushort surface = unchecked((ushort)reader.ReadInt32());
            short elevation = unchecked((short)reader.ReadInt32());
            cells[cell] = new GameMapTerrainCell(passability, surface, elevation);
        }

        return new GameMapTerrainObject(animationPath, sectionName, renderOffsetX, renderOffsetY, frameInterval,
            gridWidth, gridHeight, authoredColor, anchorOffsetX, anchorOffsetY, drawParameter, Array.AsReadOnly(cells));
    }

    private ref struct SceneReader(ReadOnlySpan<byte> source)
    {
        private readonly ReadOnlySpan<byte> _source = source;
        private int _offset;

        public readonly int Remaining => _source.Length - _offset;

        public int ReadInt32()
        {
            if (Remaining < sizeof(int))
            {
                throw new InvalidDataException("Terrain scene record is truncated.");
            }

            int value = BinaryPrimitives.ReadInt32LittleEndian(_source[_offset..]);
            _offset += sizeof(int);
            return value;
        }

        public string ReadName(int fieldBytes, int objectIndex)
        {
            if (Remaining < fieldBytes)
            {
                throw new InvalidDataException("Terrain scene record is truncated.");
            }

            ReadOnlySpan<byte> field = _source.Slice(_offset, fieldBytes);
            _offset += fieldBytes;

            int length = field.IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException($"Terrain scene object {objectIndex} has an empty or unterminated name.");
            }

            return Encoding.Latin1.GetString(field[..length]);
        }

        public string ReadContentPath(int fieldBytes, int objectIndex)
        {
            string path = ReadName(fieldBytes, objectIndex);
            try
            {
                return ClientContentPath.NormalizeVirtualPath(path, nameof(path), fieldBytes);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Terrain scene object {objectIndex} has an invalid animation path.", exception);
            }
        }
    }
}
