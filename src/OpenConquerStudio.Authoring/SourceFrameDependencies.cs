using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed record SourceFrameInput(string Path, string Digest, int ByteLength, string Format,
    int Width, int Height)
{
    public Guid? TextureId
    {
        get; init;
    }
    public const int MaximumBytes = 16 * 1024 * 1024;

    public void Validate()
    {
        TerrainSource.ValidatePath(Path);
        ProjectDocument.ValidateDigest(Digest);
        if (ByteLength is < 1 or > MaximumBytes || Format is not ("dds" or "tga") ||
            Width <= 0 || Height <= 0)
        {
            throw new InvalidDataException("Captured image frame metadata or bounds are invalid.");
        }
        _ = TextureData.GetByteLength(Width, Height);
        if (TextureId == Guid.Empty)
        {
            throw new InvalidDataException("Linked source frame texture identity cannot be empty.");
        }
    }
}

public sealed record SourceFrameCapture(string Path, byte[] Bytes);

public static class SourceFrameDependencies
{
    public static ImmutableArray<string> Collect(ImmutableArray<SourceAnimationIndexInput> indexes)
    {
        if (indexes.IsDefault)
        {
            throw new InvalidDataException("Frame collection requires verified animation section references.");
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        ImmutableArray<string>.Builder paths = ImmutableArray.CreateBuilder<string>();
        foreach (SourceAnimationIndexInput index in indexes)
        {
            foreach (SourceAnimationSection section in index.Sections)
            {
                foreach (string frame in section.FramePaths)
                {
                    string path = frame.Replace('\\', '/');
                    TerrainSource.ValidatePath(path);
                    if (seen.Add(path))
                    {
                        paths.Add(path);
                    }
                }
            }
        }
        if (paths.Count > 8192)
        {
            throw new InvalidDataException("Map references too many distinct image frames.");
        }
        return paths.ToImmutable();
    }

    public static SourceFrameInput Read(string path, byte[] bytes, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        TerrainSource.ValidatePath(path);
        string format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".dds" => "dds",
            ".tga" => "tga",
            _ => throw new InvalidDataException($"Animation frame '{path}' must use supported DDS or TGA image data.")
        };
        (TextureData image, _, string digest) = TextureImporter.Decode(bytes, format, token);
        SourceFrameInput input = new(path, digest, bytes.Length, format, image.Width, image.Height);
        input.Validate();
        return input;
    }

    public static void Validate(ImmutableArray<string> required, ImmutableArray<SourceFrameCapture> frames,
        int maximumBytes, CancellationToken token = default)
    {
        if (frames.IsDefault || frames.Length != required.Length)
        {
            throw new InvalidDataException("Every referenced animation frame must have a captured image.");
        }
        long total = 0;
        for (int i = 0; i < required.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            SourceFrameCapture frame = frames[i];
            if (!string.Equals(frame.Path, required[i], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Captured image frame order differs from referenced ANI sections.");
            }
            total += frame.Bytes.Length;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("Captured image frames exceed the project byte budget.");
            }
            _ = Read(required[i], frame.Bytes, token);
        }
    }

    public static bool Equivalent(ImmutableArray<SourceFrameInput> left, ImmutableArray<SourceFrameInput> right) =>
        left.SequenceEqual(right);
}
