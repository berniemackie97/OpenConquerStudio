using System.Collections.Immutable;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record SourceSceneObject(string AnimationPath, string SectionName,
    int Width, int Height, int CellCount);

public sealed record SourceSceneInput(string Path, string Digest, int ByteLength,
    ImmutableArray<SourceSceneObject> Objects)
{
    public void Validate()
    {
        TerrainSource.ValidatePath(Path);
        ProjectDocument.ValidateDigest(Digest);
        if (ByteLength is < 4 or > GameMapTerrainSceneFile.MaximumFileBytes || Objects.IsDefault ||
            Objects.Length > 4096)
        {
            throw new InvalidDataException("Captured scenery file exceeds its source-record limits.");
        }
        foreach (SourceSceneObject item in Objects)
        {
            TerrainSource.ValidatePath(item.AnimationPath);
            if (string.IsNullOrWhiteSpace(item.SectionName) || item.SectionName.Length > 260 ||
                item.SectionName.Any(char.IsControl) || item.Width < 0 || item.Height < 0 ||
                (long)item.Width * item.Height != item.CellCount)
            {
                throw new InvalidDataException("Captured scenery object has invalid animation or cell metadata.");
            }
        }
    }
}

public sealed record SourceSceneCapture(string Path, byte[] Bytes);

public static class SourceSceneDependencies
{
    public static ImmutableArray<string> Collect(GameMapSceneSections mapScene)
    {
        ArgumentNullException.ThrowIfNull(mapScene);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        ImmutableArray<string>.Builder paths = ImmutableArray.CreateBuilder<string>();
        foreach (GameMapSceneryRecord record in mapScene.Scenery)
        {
            if (record.TypeCode != 1)
            {
                continue;
            }
            if (record.ResourcePath is not string path)
            {
                throw new InvalidDataException("Type-1 scenery has no source scene path.");
            }
            TerrainSource.ValidatePath(path);
            if (seen.Add(path))
            {
                paths.Add(path);
            }
        }
        if (paths.Count > 256)
        {
            throw new InvalidDataException("Map references too many distinct scenery files.");
        }
        return paths.ToImmutable();
    }

    public static SourceSceneInput Read(string path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        TerrainSource.ValidatePath(path);
        if (bytes.Length is < 4 or > GameMapTerrainSceneFile.MaximumFileBytes)
        {
            throw new InvalidDataException($"Scenery source '{path}' exceeds its file budget.");
        }

        GameMapTerrainSceneFile scene = GameMapTerrainSceneFile.Parse(bytes);
        SourceSceneInput input = new(path, ProjectFiles.Hash(bytes), bytes.Length,
            [.. scene.Objects.Select(o => new SourceSceneObject(
                o.AnimationPath, o.SectionName, o.GridWidth, o.GridHeight, o.Cells.Count))]);
        input.Validate();
        return input;
    }

    public static bool Equivalent(ImmutableArray<SourceSceneInput> left, ImmutableArray<SourceSceneInput> right) =>
        left.Length == right.Length && left.Zip(right).All(pair =>
            pair.First.Path == pair.Second.Path && pair.First.Digest == pair.Second.Digest &&
            pair.First.ByteLength == pair.Second.ByteLength &&
            pair.First.Objects.SequenceEqual(pair.Second.Objects));

    public static void Validate(ImmutableArray<string> required, ImmutableArray<SourceSceneCapture> captures,
        int maximumBytes)
    {
        if (captures.IsDefault || captures.Length != required.Length)
        {
            throw new InvalidDataException("Every referenced scenery file must be captured.");
        }
        long total = 0;
        for (int i = 0; i < required.Length; i++)
        {
            SourceSceneCapture capture = captures[i];
            if (!string.Equals(capture.Path, required[i], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Scenery file capture order differs from source records.");
            }
            total += capture.Bytes.Length;
            if (total > maximumBytes)
            {
                throw new InvalidDataException("Scenery files exceed the aggregate source budget.");
            }
            _ = Read(required[i], capture.Bytes);
        }
    }
}
