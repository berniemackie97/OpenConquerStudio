using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OpenConquer.Content.Map;

/// <summary>Retail terrain index, keyed by packet 1110 MapDataId rather than world MapId.</summary>
public sealed class GameMapCatalog
{
    public const string RelativePath = "ini/GameMap.dat";

    private const int MaximumLength = 64 * 1024;
    private const int MaximumPathBytes = 63;
    private readonly Dictionary<uint, Terrain> _terrains;

    private GameMapCatalog(Dictionary<uint, Terrain> terrains) => _terrains = terrains;

    public int Count => _terrains.Count;
    public IReadOnlyCollection<uint> MapDataIds => _terrains.Keys;

    public bool TryGetTerrain(uint mapDataId, [NotNullWhen(true)] out Terrain? terrain) =>
        _terrains.TryGetValue(mapDataId, out terrain);

    public static GameMapCatalog Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        return Parse(ContentReader.ReadRequiredBytes(contentSource, RelativePath, ContentLookupMode.LooseOnly, MaximumLength));
    }

    public static GameMapCatalog Parse(ReadOnlySpan<byte> file)
    {
        if (file.Length < sizeof(uint))
        {
            throw new InvalidDataException("GameMap.dat has no record count.");
        }

        uint recordCount = BinaryPrimitives.ReadUInt32LittleEndian(file);
        if (recordCount > (file.Length - sizeof(uint)) / 13)
        {
            throw new InvalidDataException("GameMap.dat declares more records than the file can contain.");
        }

        Dictionary<uint, Terrain> terrains = new(checked((int)recordCount));
        int offset = sizeof(uint);

        for (uint i = 0; i < recordCount; i++)
        {
            if (file.Length - offset < 12)
            {
                throw new InvalidDataException("GameMap.dat contains a truncated terrain record.");
            }

            uint dataId = BinaryPrimitives.ReadUInt32LittleEndian(file[offset..]);
            uint pathLength = BinaryPrimitives.ReadUInt32LittleEndian(file[(offset + 4)..]);
            offset += 8;

            if (pathLength is 0 or > MaximumPathBytes || file.Length - offset < pathLength + sizeof(uint))
            {
                throw new InvalidDataException("GameMap.dat contains an invalid or truncated terrain path.");
            }

            string path = Encoding.Latin1.GetString(file.Slice(offset, (int)pathLength));
            offset += (int)pathLength;
            uint companion = BinaryPrimitives.ReadUInt32LittleEndian(file[offset..]);
            offset += sizeof(uint);

            try
            {
                _ = ClientContentPath.ParseSegments(path, nameof(path));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("GameMap.dat contains an unsafe terrain path.", exception);
            }

            // Native find-or-insert copies over an existing key's value; the last record wins.
            terrains[dataId] = new Terrain(dataId, path, companion);
        }

        if (offset != file.Length)
        {
            throw new InvalidDataException("GameMap.dat contains trailing data.");
        }

        return new GameMapCatalog(terrains);
    }

    public sealed record Terrain(uint MapDataId, string Path, uint CompanionValue);
}
