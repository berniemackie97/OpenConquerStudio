using System.Collections.Immutable;
using OpenConquer.Content.Mesh;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

public sealed partial class StudioWindow
{
    private async Task UpdatePreview(Guid id, CancellationToken token, int? modelClipIndex = null)
    {
        ProjectDocument document = Project.Document;
        ResourceInfo info = document.Resources.Single(a => a.Id == id);
        _preview.Pause();
        C3ModelPreview? model = null;
        ImmutableArray<ViewportSprite> frames = [];
        Guid spriteId = _atlasEditor.SelectedSprite;
        if (info.Kind == "model")
        {
            model = await Task.Run(() =>
            {
                C3ModelData data = Project.PreviewModel(id);
                return C3ModelPreview.Create(data, modelClipIndex ?? (data.Definition.Clips.IsEmpty ? -1 : 0));
            }, token);
        }
        else
        {
            if (info.Kind == "atlas")
            {
                AtlasAsset atlas = document.Atlases.Single(a => a.Id == id);
                if (!atlas.Sprites.Any(s => s.Id == spriteId))
                {
                    spriteId = atlas.Sprites[0].Id;
                }
            }
            frames = await Task.Run(() => MakePreview(info, spriteId, token), token);
        }
        token.ThrowIfCancellationRequested();
        _loadingInspector = true;
        try
        {
            _emptyPreview.IsVisible = false;
            _preview.IsVisible = true;
            ClientContent.Inspector.IsVisible = false;
            _assetTabs.SelectedIndex = 0;
            _activeWorkspaceTab = 0;
            _inspectedAsset = id;
            _previewInvalidated = false;
            _textureInspector.IsVisible = info.Kind == "texture";
            _atlasEditor.IsVisible = info.Kind == "atlas";
            _animationEditor.IsVisible = info.Kind == "animation";
            _modelEditor.IsVisible = info.Kind == "model";
            _dependencies.Text = "Uses: " + string.Join(", ", document.Dependencies(id).Select(dependency => document.Resources.Single(a => a.Id == dependency).Name)) +
                "\nUsed by: " + string.Join(", ", document.Dependents(id).Select(a => a.Name));
            if (model is not null)
            {
                _modelEditor.Load(info.Name, model);
                if (_preview.IsWorldModelActive)
                {
                    _clientWorkspace.PreviewOnMap(model);
                }
                else
                {
                    _preview.LoadModel(model);
                }
                return;
            }
            if (info.Kind == "atlas")
            {
                _atlasEditor.Load(document.Atlases.Single(a => a.Id == id), document);
            }
            AnimationAsset? animation = document.Animations.FirstOrDefault(a => a.Id == id);
            if (animation is not null)
            {
                _animationEditor.Load(animation, document);
            }
            _preview.Load(frames, animation is null ? null : new SpriteAnimation(animation.Id, animation.TicksPerSecond, animation.Repeat, animation.Frames));
            if (animation is not null)
            {
                _preview.SelectFrame(_animationEditor.SelectedFrame);
            }
            if (info.Kind == "texture")
            {
                TextureAsset asset = document.Assets.Single(a => a.Id == id);
                _assetName.Text = asset.Name;
                _flip.IsChecked = asset.FlipHorizontal;
                _rotation.SelectedIndex = asset.ClockwiseQuarterTurns;
                _metadata.Text = $"{frames[0].Texture.Width} × {frames[0].Texture.Height} RGBA8\nID: {asset.Id}\nSource: {asset.SourceFormat}";
            }
        }
        finally { _loadingInspector = false; }
    }

    private ImmutableArray<ViewportSprite> MakePreview(ResourceInfo info, Guid selectedSprite, CancellationToken token)
    {
        ProjectDocument document = _project!.Document;
        Dictionary<Guid, TextureData> textures = [];
        TextureData GetTexture(Guid id)
        {
            if (!textures.TryGetValue(id, out TextureData? texture))
            {
                texture = _project.Preview(id, token);
                textures.Add(id, texture);
            }

            return texture;
        }

        ViewportSprite GetSprite(Guid atlasId, Guid spriteId)
        {
            AtlasAsset atlas = document.Atlases.Single(a => a.Id == atlasId);
            return new ViewportSprite(atlas.TextureId, GetTexture(atlas.TextureId), atlas.Sprites.Single(s => s.Id == spriteId));
        }

        if (info.Kind == "texture")
        {
            TextureData texture = GetTexture(info.Id);
            return [new ViewportSprite(info.Id, texture, new SpriteRegion(info.Id, info.Name, 0, 0, texture.Width, texture.Height, texture.Width / 2, texture.Height / 2))];
        }

        if (info.Kind == "atlas")
        {
            return [GetSprite(info.Id, selectedSprite)];
        }

        return [.. document.Animations.Single(a => a.Id == info.Id).Frames.Select(f => GetSprite(f.AtlasId, f.SpriteId))];
    }

    private void ClearPreview()
    {
        _previewInvalidated = false;
        _preview.Clear();
        ClientContent.Inspector.IsVisible = false;
        _inspectedAsset = null;
        _textureInspector.IsVisible = false;
        _atlasEditor.IsVisible = false;
        _animationEditor.IsVisible = false;
        _modelEditor.IsVisible = false;
        _dependencies.Text = "Select an asset to inspect its dependencies.";
        _emptyPreview.Text = _project is null ? "Open a Studio project or connect your client checkout." : "Use Assets to import or create editable content.\nConnected client sources are available in Client content.";
        _emptyPreview.IsVisible = true;
        _preview.IsVisible = false;
    }

}
