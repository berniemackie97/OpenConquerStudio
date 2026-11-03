using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenConquer.Content.Mesh;

/// <summary>OCM3 v2 (reads v1): bounded metadata and ordered, exact source payloads, integrity checked together by SHA-256.</summary>
public static class C3ModelCodec
{
    private const int HeaderLength = 48;
    private const int MaximumMetadataBytes = 1024 * 1024;
    public const int MaximumLength = C3ModelData.MaximumPayloadBytes + MaximumMetadataBytes + HeaderLength;
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        MaxDepth = 12,
    };

    public static byte[] Write(C3ModelData model)
    {
        ArgumentNullException.ThrowIfNull(model);
        byte[] metadata = JsonSerializer.SerializeToUtf8Bytes(model.Definition with
        {
            SchemaVersion = 2
        }, s_json);
        if (metadata.Length > MaximumMetadataBytes)
        {
            throw new InvalidDataException("Model metadata exceeds 1 MiB.");
        }
        byte[] bytes = new byte[checked(HeaderLength + metadata.Length + model.PayloadBytes)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x334D434F);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), metadata.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), model.PayloadBytes);
        metadata.CopyTo(bytes.AsSpan(HeaderLength));
        int offset = HeaderLength + metadata.Length;
        foreach (C3ModelSource file in model.Definition.Sources)
        {
            model.Source(file.Path).Span.CopyTo(bytes.AsSpan(offset));
            offset += file.Length;
        }
        SHA256.HashData(bytes.AsSpan(HeaderLength), bytes.AsSpan(16, 32));
        return bytes;
    }

    public static C3ModelData Read(ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderLength || data.Length > MaximumLength)
        {
            throw new InvalidDataException("Model size is outside supported bounds.");
        }
        ReadOnlySpan<byte> bytes = data.Span;
        int metadataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x334D434F ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]) is not (1 or 2) ||
            metadataLength is < 1 or > MaximumMetadataBytes ||
            payloadLength is < 1 or > C3ModelData.MaximumPayloadBytes ||
            HeaderLength + (long)metadataLength + payloadLength != bytes.Length ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes[HeaderLength..]), bytes.Slice(16, 32)))
        {
            throw new InvalidDataException("Model header, version, length or digest is invalid.");

        }
        C3ModelDefinition definition;
        try
        {
            definition = JsonSerializer.Deserialize<C3ModelDefinition>(bytes.Slice(HeaderLength, metadataLength), s_json)
                ?? throw new InvalidDataException("Model metadata is empty.");
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid model metadata.", error); }

        if (definition.SchemaVersion != BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]))
        {
            throw new InvalidDataException("Model header and metadata versions disagree.");
        }
        if (definition.Sources.IsDefaultOrEmpty || definition.Sources.Length > 128)
        {
            throw new InvalidDataException("Invalid model file count.");
        }
        Dictionary<string, ReadOnlyMemory<byte>> files = new(StringComparer.Ordinal);
        int offset = HeaderLength + metadataLength;
        string? previous = null;
        foreach (C3ModelSource file in definition.Sources)
        {
            if (file is null || file.Path != C3ModelData.NormalizePath(file.Path) ||
                previous is not null && string.CompareOrdinal(previous, file.Path) >= 0 || file.Length < 0 ||
                file.Length > bytes.Length - offset)
            {
                throw new InvalidDataException("Model dependencies require unique, sorted paths and bounded lengths.");
            }
            ReadOnlyMemory<byte> payload = data.Slice(offset, file.Length).ToArray();
            if (Hash(payload.Span) != file.Sha256)
            {
                throw new InvalidDataException("Model dependency digest mismatch.");
            }
            files.Add(file.Path, payload);
            offset += file.Length;
            previous = file.Path;
        }

        if (offset != bytes.Length)
        {
            throw new InvalidDataException("Model contains undeclared trailing data.");
        }
        return new C3ModelData(definition, files);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
