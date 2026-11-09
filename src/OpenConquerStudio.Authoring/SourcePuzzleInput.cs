using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record SourcePuzzleInput(string Path, string? Digest, int ByteLength,
    string? AnimationIndexPath, int Columns, int Rows, int ScrollVelocityX, int ScrollVelocityY)
{
    public const int MaximumBytes = 128 * 1024;

    public void Validate()
    {
        TerrainSource.ValidatePath(Path);
        if (Digest is null)
        {
            if (ByteLength != 0 || AnimationIndexPath is not null ||
                Columns != 0 || Rows != 0 || ScrollVelocityX != 0 || ScrollVelocityY != 0)
            {
                throw new InvalidDataException("Missing puzzle inputs cannot contain decoded resource metadata.");
            }
            return;
        }

        ProjectDocument.ValidateDigest(Digest);
        if (ByteLength is < 272 or > MaximumBytes || Columns <= 0 || Rows <= 0 ||
            AnimationIndexPath is null)
        {
            throw new InvalidDataException("Captured puzzle metadata is invalid.");
        }
        TerrainSource.ValidatePath(AnimationIndexPath);
    }

    public static SourcePuzzleInput FromBytes(string path, byte[] bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Puzzle input exceeds its supported length.");
        }
        GameMapPuzzleBitmap bitmap = GameMapPuzzleBitmap.Parse(bytes);
        return new SourcePuzzleInput(path, ProjectFiles.Hash(bytes), bytes.Length,
            bitmap.TextureIndexPath, bitmap.Columns, bitmap.Rows,
            bitmap.ScrollVelocityX, bitmap.ScrollVelocityY);
    }
}

public sealed record SourcePuzzleCapture(string Path, byte[]? Bytes);
