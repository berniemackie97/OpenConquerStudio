using System.Collections.Immutable;
using System.Globalization;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquerStudio.Authoring;

public sealed record SourceAnimationSection(string Name, ImmutableArray<string> FramePaths);

public sealed record SourceAnimationIndexInput(string Path, string Digest, int ByteLength,
    ImmutableArray<SourceAnimationSection> Sections)
{
    public const int MaximumBytes = 16 * 1024 * 1024;

    public void Validate()
    {
        TerrainSource.ValidatePath(Path);
        ProjectDocument.ValidateDigest(Digest);
        if (ByteLength is < 1 or > MaximumBytes || Sections.IsDefault || Sections.Length is < 1 or > 4096)
        {
            throw new InvalidDataException("Captured animation index exceeds its section or byte budget.");
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (SourceAnimationSection section in Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Name) || section.Name.Length > 260 ||
                section.Name.Any(char.IsControl) || !names.Add(section.Name) ||
                section.FramePaths.IsDefault || section.FramePaths.Length is < 1 or > 64)
            {
                throw new InvalidDataException("Captured animation sections require distinct names and 1–64 frames.");
            }

            foreach (string frame in section.FramePaths)
            {
                TerrainSource.ValidatePath(frame.Replace('\\', '/'));
            }
        }
    }
}

public sealed record SourceAnimationIndexCapture(string Path, byte[] Bytes);

public sealed record SourceAnimationRequirement(string Path, ImmutableArray<string> Sections);

public static class SourceAnimationDependencies
{
    public static ImmutableArray<SourceAnimationRequirement> Collect(GameMapSceneSections scene,
        ImmutableArray<SourcePuzzleCapture> puzzles, ImmutableArray<SourceSceneCapture> scenes = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (puzzles.IsDefault)
        {
            throw new InvalidDataException("Animation index collection requires a complete puzzle capture inventory.");
        }

        List<string> paths = [];
        Dictionary<string, List<string>> sections = new(StringComparer.OrdinalIgnoreCase);
        void Add(string path, string section)
        {
            TerrainSource.ValidatePath(path);
            if (string.IsNullOrWhiteSpace(section))
            {
                throw new InvalidDataException($"Animation index '{path}' contains an empty referenced section.");
            }
            if (!sections.TryGetValue(path, out List<string>? names))
            {
                names = [];
                sections.Add(path, names);
                paths.Add(path);
            }
            if (!names.Contains(section, StringComparer.Ordinal))
            {
                names.Add(section);
            }
        }

        foreach (SourcePuzzleCapture capture in puzzles)
        {
            if (capture.Bytes is null)
            {
                continue;
            }
            GameMapPuzzleBitmap bitmap = GameMapPuzzleBitmap.Parse(capture.Bytes);
            foreach (ushort tile in bitmap.Tiles)
            {
                if (tile != ushort.MaxValue)
                {
                    Add(bitmap.TextureIndexPath, "Puzzle" + tile.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (GameMapSceneryRecord record in scene.Scenery)
        {
            if (record.TypeCode == 4 && !GameMapSceneryAniBindings.IsEmptyAnimationRecord(record))
            {
                Add(record.ResourcePath!, record.PartName!);
            }
        }

        if (!scenes.IsDefault)
        {
            ImmutableArray<string> required = SourceSceneDependencies.Collect(scene);
            SourceSceneDependencies.Validate(required, scenes, ProjectDocument.MaximumTotalSceneBytes);
            foreach (SourceSceneCapture capture in scenes)
            {
                GameMapTerrainSceneFile file = GameMapTerrainSceneFile.Parse(capture.Bytes);
                foreach (GameMapTerrainObject sceneObject in file.Objects)
                {
                    Add(sceneObject.AnimationPath, sceneObject.SectionName);
                }
            }
        }

        foreach (GameMapPositionedOverlayRecord group in scene.PositionedOverlays)
        {
            foreach (GameMapAnimatedOverlayEntry entry in group.Entries.OfType<GameMapAnimatedOverlayEntry>())
            {
                Add(entry.ResourcePath, entry.PartName);
            }
        }

        if (paths.Count > 256 || sections.Values.Sum(names => (long)names.Count) > 4096)
        {
            throw new InvalidDataException("Map animation references exceed the supported index or section budget.");
        }

        return [.. paths.Select(path => new SourceAnimationRequirement(path, [.. sections[path]]))];
    }

    public static SourceAnimationIndexInput Read(SourceAnimationRequirement requirement, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(bytes);
        TerrainSource.ValidatePath(requirement.Path);
        if (bytes.Length is < 1 or > SourceAnimationIndexInput.MaximumBytes)
        {
            throw new InvalidDataException($"Animation index '{requirement.Path}' exceeds its byte budget.");
        }

        using MemoryStream stream = new(bytes, writable: false);
        AniIndexFile index = AniIndexFile.Load(stream, requirement.Path);
        ImmutableArray<SourceAnimationSection>.Builder sections =
            ImmutableArray.CreateBuilder<SourceAnimationSection>(requirement.Sections.Length);
        foreach (string name in requirement.Sections)
        {
            if (!index.TryGetSection(name, out AniIndexSection? section) ||
                !string.Equals(section.Name, name, StringComparison.Ordinal) ||
                section.FrameCount is < 1 or > 64)
            {
                throw new InvalidDataException(
                    $"Animation index '{requirement.Path}' cannot resolve exact section [{name}] with 1–64 frames.");
            }

            sections.Add(new SourceAnimationSection(name, [.. section.FramePaths]));
        }

        SourceAnimationIndexInput result = new(requirement.Path, ProjectFiles.Hash(bytes), bytes.Length,
            sections.MoveToImmutable());
        result.Validate();
        return result;
    }

    public static bool Equivalent(ImmutableArray<SourceAnimationIndexInput> left,
        ImmutableArray<SourceAnimationIndexInput> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }
        for (int i = 0; i < left.Length; i++)
        {
            SourceAnimationIndexInput a = left[i];
            SourceAnimationIndexInput b = right[i];
            if (a.Path != b.Path || a.Digest != b.Digest || a.ByteLength != b.ByteLength ||
                a.Sections.Length != b.Sections.Length)
            {
                return false;
            }
            for (int j = 0; j < a.Sections.Length; j++)
            {
                if (a.Sections[j].Name != b.Sections[j].Name ||
                    !a.Sections[j].FramePaths.SequenceEqual(b.Sections[j].FramePaths))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public static void Validate(ImmutableArray<SourceAnimationRequirement> required,
        ImmutableArray<SourceAnimationIndexCapture> captures, int maximumTotalBytes)
    {
        if (captures.IsDefault || required.Length != captures.Length)
        {
            throw new InvalidDataException("Every referenced animation index must be captured in first-reference order.");
        }

        long total = 0;
        for (int i = 0; i < required.Length; i++)
        {
            SourceAnimationIndexCapture captured = captures[i];
            if (!string.Equals(captured.Path, required[i].Path, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Captured animation indexes are out of source-reference order.");
            }
            total += captured.Bytes.Length;
            if (total > maximumTotalBytes)
            {
                throw new InvalidDataException("Captured animation indexes exceed the project byte budget.");
            }

            _ = Read(required[i], captured.Bytes);
        }
    }
}
