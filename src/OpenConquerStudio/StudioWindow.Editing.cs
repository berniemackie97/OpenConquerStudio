using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

public sealed partial class StudioWindow
{
    private Guid? _inspectedAsset;
    private int _activeWorkspaceTab;
    private bool _loadingInspector;
    private bool _previewInvalidated;

    private bool HasPendingEdits
    {
        get
        {
            if (_project is null || _loadingInspector)
            {
                return false;
            }
            if (_projectName.Text != _project.Document.Name)
            {
                return true;
            }
            if (_activeWorkspaceTab != 0 || _inspectedAsset is not Guid id)
            {
                return false;
            }
            ResourceInfo? info = _project.Document.Resources.FirstOrDefault(r => r.Id == id);
            return info?.Kind switch
            {
                "texture" => _project.Document.Assets.First(a => a.Id == id) is { } texture &&
                    (texture.Name != _assetName.Text || texture.FlipHorizontal != (_flip.IsChecked == true) || texture.ClockwiseQuarterTurns != _rotation.SelectedIndex),
                "atlas" => _atlasEditor.HasPending,
                "animation" => _animationEditor.HasPending,
                "model" => _modelEditor.HasPending,
                "terrain" => _terrainEditor.HasPending,
                _ => false,
            };
        }
    }

    private async Task CommitPendingEdits(CancellationToken token)
    {
        if (_project is null || !HasPendingEdits)
        {
            return;
        }
        StudioProject project = _project;
        string name = _projectName.Text ?? "";
        (project.Document with
        {
            Name = name
        }).Validate();
        Action? apply = null;
        ResourceInfo? info = _activeWorkspaceTab == 0 && _inspectedAsset is Guid id
            ? project.Document.Resources.FirstOrDefault(r => r.Id == id) : null;
        switch (info?.Kind)
        {
            case "texture":
                string textureName = _assetName.Text ?? "";
                bool flip = _flip.IsChecked == true;
                int rotation = _rotation.SelectedIndex;
                apply = () => project.EditTexture(info.Id, textureName, flip, rotation);
                break;
            case "atlas":
                AtlasAsset atlas = _atlasEditor.ReadChanges();
                apply = () => project.EditAtlas(atlas);
                break;
            case "animation":
                AnimationAsset animation = _animationEditor.ReadChanges();
                apply = () => project.EditAnimation(animation);
                break;
            case "model":
                ModelEdit model = _modelEditor.ReadChanges();
                apply = () => project.EditModel(info.Id, model.Name, model.Clip, model.Milliseconds, model.Repeat);
                break;
            case "terrain":
                (string terrainName, TerrainCell cell, var exits) = _terrainEditor.ReadChanges();
                int x = _terrainEditor.SelectedX;
                int y = _terrainEditor.SelectedY;
                apply = () => project.EditTerrainCell(info.Id, x, y, cell, terrainName, exits);
                break;
        }
        await Task.Run(() => { token.ThrowIfCancellationRequested(); apply?.Invoke(); project.Rename(name); }, token);
        _previewInvalidated = apply is not null;
        if (info?.Kind == "atlas")
        {
            _atlasEditor.Accept(project.Document.Atlases.Single(a => a.Id == info.Id), project.Document);
        }
        if (info?.Kind == "animation")
        {
            _animationEditor.Accept(project.Document.Animations.Single(a => a.Id == info.Id));
        }
        if (info?.Kind == "model")
        {
            _modelEditor.AcceptPending();
        }
        if (info?.Kind == "terrain")
        {
            _terrainEditor.Accept(project.Document.Terrains.Single(a => a.Id == info.Id));
        }
    }

    private async Task<bool> CommitForNavigation()
    {
        if (!HasPendingEdits)
        {
            return true;
        }
        bool accepted = false;
        await ExecuteAsync(_ => { accepted = true; return Task.CompletedTask; });
        return accepted;
    }

    private void PendingEditsChanged()
    {
        if (!_loadingInspector && !_refreshing)
        {
            RefreshShell();
            RefreshEnabled();
        }
    }

    private void RefreshShell()
    {
        bool dirty = _project?.IsModified == true || HasPendingEdits;
        Title = (dirty ? "* " : "") + "OpenConquerStudio" + (_project is null ? "" : " — " + _project.Document.Name);
        _projectStatus.Text = _project is null ? "No project open" : $"{_project.Document.Name}  •  {_project.Document.Resources.Count()} assets  •  {(dirty ? "Unsaved changes" : "Saved")}";
        Avalonia.Controls.ToolTip.SetTip(_projectStatus, _project?.Root);
    }
}
