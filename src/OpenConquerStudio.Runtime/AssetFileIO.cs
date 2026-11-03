using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenConquerStudio.Runtime;

internal static class AssetFileIO
{
    internal const int MaximumManifestBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        AllowDuplicateProperties = false,
        WriteIndented = true,
        MaxDepth = 12,
    };

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, s_json);

    public static T Deserialize<T>(byte[] bytes) =>
        JsonSerializer.Deserialize<T>(bytes, s_json) ?? throw new InvalidDataException("The document is empty.");

    // All project paths are generated from fixed segments, IDs, or validated digests.
    // Checking every ancestor also rejects a link introduced above a selected project.
    public static string RequireSafePath(string path)
    {
        string full = Path.GetFullPath(path);
        string? current = full;
        while (current is not null)
        {
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (entry.LinkTarget is not null || entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Symbolic links and reparse points are not supported: '{current}'.");
            }

            current = Path.GetDirectoryName(current);
        }

        return full;
    }

    public static byte[] Read(string path, int maximumLength)
    {
        RequireSafePath(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumLength)
        {
            throw new InvalidDataException($"'{Path.GetFileName(path)}' exceeds its {maximumLength}-byte limit.");
        }

        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new IOException("The file changed while it was being read.");
        }

        return bytes;
    }

    public static void WriteAtomic(string path, ReadOnlySpan<byte> bytes, bool overwrite)
    {
        RequireSafePath(path);
        string parent = Path.GetDirectoryName(path) ?? throw new IOException("A destination directory is required.");
        string temporary = Path.Combine(parent, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes);
                output.Flush(flushToDisk: true);
            }

            RequireSafePath(path);
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
    internal static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name != name.Trim() || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Names must contain 1–128 characters, without surrounding whitespace or control characters.");
        }
    }

    internal static void ValidateDigest(string digest)
    {
        if (digest is null || digest.Length != 64 || digest.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new InvalidDataException("Expected a lowercase SHA-256 digest.");
        }
    }
}
