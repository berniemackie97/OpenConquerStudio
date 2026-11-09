using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed partial class StudioProject
{
    public ImmutableArray<Guid> PromoteSourceFrames(Guid terrainId, ImmutableArray<string> paths,
        CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (paths.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Select at least one captured source frame to promote.");
        }

        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == terrainId);
        TerrainSource source = terrain.Source
            ?? throw new InvalidDataException("Select a source-extracted terrain.");
        Dictionary<string, Guid> selected = new(StringComparer.OrdinalIgnoreCase);
        List<TextureAsset> created = [];
        Dictionary<string, TextureData> pixels = new(StringComparer.Ordinal);
        ImmutableArray<Guid>.Builder result = ImmutableArray.CreateBuilder<Guid>(paths.Length);
        foreach (string path in paths)
        {
            token.ThrowIfCancellationRequested();
            TerrainSource.ValidatePath(path);
            if (selected.ContainsKey(path))
            {
                throw new InvalidDataException($"Source frame '{path}' was selected more than once.");
            }

            SourceFrameInput frame = source.Frames.SingleOrDefault(f =>
                string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Source frame '{path}' is not captured by the selected terrain.");
            Guid id;
            if (frame.TextureId is Guid existing)
            {
                id = existing;
            }
            else
            {
                TextureData texture = DecodeSourceFrame(frame, _sourceInputs[frame.Digest], token);
                id = Guid.NewGuid();
                string name = Path.GetFileNameWithoutExtension(frame.Path);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Frame";
                }
                TextureAsset asset = new(id, name, texture.Width, texture.Height,
                    ProjectFiles.Hash(texture.Pixels.Span), frame.Format, frame.Digest, false, 0);
                created.Add(asset);
                pixels.TryAdd(asset.PixelDigest, texture);
            }
            selected.Add(frame.Path, id);
            result.Add(id);
        }

        if (created.Count == 0)
        {
            return result.MoveToImmutable();
        }

        TerrainSource updatedSource = source with
        {
            Frames = [.. source.Frames.Select(frame => selected.TryGetValue(frame.Path, out Guid id)
                ? frame with { TextureId = id } : frame)]
        };
        ProjectDocument updated = Document with
        {
            Assets = [.. Document.Assets, .. created],
            Terrains = [.. Document.Terrains.Select(a => a.Id == terrainId ? terrain with
            {
                Source = updatedSource
            } : a)]
        };
        updated.Validate();
        foreach ((string digest, TextureData texture) in pixels)
        {
            _pixels.TryAdd(digest, texture);
        }
        Apply(updated);
        return result.MoveToImmutable();
    }

    public void UnlinkSourceFrame(Guid terrainId, string path)
    {
        ThrowIfDisposed();
        TerrainSource.ValidatePath(path);
        TerrainAsset terrain = Document.Terrains.Single(a => a.Id == terrainId);
        TerrainSource source = terrain.Source
            ?? throw new InvalidDataException("Select a source-extracted terrain.");
        SourceFrameInput frame = source.Frames.SingleOrDefault(item =>
            string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"Source frame '{path}' is unavailable.");
        if (frame.TextureId is null)
        {
            return;
        }

        TerrainSource updatedSource = source with
        {
            Frames = [.. source.Frames.Select(item => item.Path == frame.Path
                ? item with { TextureId = null } : item)]
        };
        Apply(Document with
        {
            Terrains = [.. Document.Terrains.Select(a => a.Id == terrainId ? terrain with
            {
                Source = updatedSource
            } : a)]
        });
    }

    private (ImmutableArray<SourceFrameInput> Frames,
        Dictionary<Guid, TextureAsset> Textures, Dictionary<string, TextureData> Pixels)
        ReconcilePromotedFrames(TerrainSource previous, TerrainSource imported, TerrainImport capture)
    {
        Dictionary<string, SourceFrameInput> linked = previous.Frames
            .Where(frame => frame.TextureId is not null)
            .ToDictionary(frame => frame.Path, StringComparer.OrdinalIgnoreCase);
        Dictionary<Guid, TextureAsset> replacements = [];
        Dictionary<string, TextureData> pixels = new(StringComparer.Ordinal);
        ImmutableArray<SourceFrameInput>.Builder frames =
            ImmutableArray.CreateBuilder<SourceFrameInput>(imported.Frames.Length);
        foreach (SourceFrameInput frame in imported.Frames)
        {
            if (!linked.TryGetValue(frame.Path, out SourceFrameInput? original))
            {
                frames.Add(frame);
                continue;
            }

            Guid id = original.TextureId!.Value;
            frames.Add(frame with
            {
                TextureId = id
            });
            if (frame.Digest == original.Digest)
            {
                continue;
            }

            SourceFrameCapture source = capture.FrameFiles.Single(item =>
                string.Equals(item.Path, frame.Path, StringComparison.OrdinalIgnoreCase));
            TextureData texture = DecodeSourceFrame(frame, source.Bytes);
            TextureAsset asset = FindAsset(id);
            TextureAsset updated = asset with
            {
                Width = texture.Width,
                Height = texture.Height,
                PixelDigest = ProjectFiles.Hash(texture.Pixels.Span),
                SourceFormat = frame.Format,
                SourceDigest = frame.Digest
            };
            replacements.Add(id, updated);
            pixels.TryAdd(updated.PixelDigest, texture);
        }

        return (frames.MoveToImmutable(), replacements, pixels);
    }

    private static TextureData DecodeSourceFrame(SourceFrameInput frame, byte[] bytes,
        CancellationToken token = default)
    {
        (TextureData texture, string format, string digest) =
            TextureImporter.Decode(bytes, frame.Format, token);
        if (format != frame.Format || digest != frame.Digest ||
            texture.Width != frame.Width || texture.Height != frame.Height)
        {
            throw new InvalidDataException($"Captured source frame '{frame.Path}' failed its revision check.");
        }
        return texture;
    }
}
