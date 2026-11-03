using System.Text;

namespace OpenConquer.Content.Map;

public static class MapAssetCodec
{
    public static byte[] Encode(MapAsset map)
    {
        using MemoryStream destination = new();
        Write(map, destination);
        return destination.ToArray();
    }

    public static void Write(MapAsset map, Stream destination) => MapAssetWriter.Write(map, destination);

    public static MapAsset Load(IClientContentSource content, uint mapDataId)
    {
        ArgumentNullException.ThrowIfNull(content);
        string path = $"maps/{mapDataId}.ocmap";
        using Stream input = content.OpenRequiredRead(path, ContentLookupMode.LooseOnly);
        MapAsset asset = Decode(input);
        if (asset.MapDataId != mapDataId)
        {
            throw new InvalidDataException($"Map asset '{path}' declares a different map data ID.");
        }

        return asset;
    }

    public static MapAsset Decode(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using MemoryStream input = new(source, writable: false);
        return Decode(input);
    }

    public static MapAsset Decode(ReadOnlySpan<byte> source) => Decode(source.ToArray());

    public static MapAsset Decode(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using MapAssetReader reader = new(source, leaveOpen: true);
            return reader.Read();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Map asset data is truncated.", exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Map asset string contains invalid UTF-8.", exception);
        }
    }
}
