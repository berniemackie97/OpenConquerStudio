using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

public sealed partial class StudioWindow
{
    private async Task<bool> CanReplaceProject(CancellationToken token)
    {
        if (_project?.IsModified != true && !HasPendingEdits)
        {
            return true;
        }

        string choice = await StudioDialogs.ConfirmChanges(this);
        if (choice == "Save")
        {
            await CommitPendingEdits(token);
            await Task.Run(() => _project!.Save(token), token);
        }

        return choice != "Cancel";
    }

    private async Task NewProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? name = await StudioDialogs.ProjectName(this);
        if (name is null)
        {
            return;
        }
        string? parent = await StudioDialogs.PickFolder(this, "Choose where to create the new project folder");
        string? root = parent is null ? null : Path.Combine(parent, name);
        if (root is not null)
        {
            StudioProject candidate = await Task.Run(() => StudioProject.Create(root, name), token);
            ReplaceProject(candidate);
        }
    }

    private async Task OpenProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? root = await StudioDialogs.PickProject(this);
        if (root is not null && root != _project?.Root)
        {
            await LoadProject(root, token);
        }
    }

    internal async Task LoadProject(string root, CancellationToken token)
    {
        ProjectLocation location = StudioProjectOpening.Resolve(root);
        if (_project is not null && string.Equals(Path.GetFullPath(location.Workspace).TrimEnd(Path.DirectorySeparatorChar),
            _project.Root.TrimEnd(Path.DirectorySeparatorChar), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            _output.Text = "Already open: " + _project.Document.Name;
            return;
        }
        OpenedProject opened = await Task.Run(() => StudioProjectOpening.Open(location, token), token);
        ReplaceProject(opened.Project);
        if (opened.Connection is not null)
        {
            _clientWorkspace.Attach(opened.Connection, opened.Inventory);
        }
        if (opened.Project.Document.Resources.FirstOrDefault() is { } first)
        {
            RefreshAssets(first.Id);
            await UpdatePreview(first.Id, token);
        }
        else if (opened.Connection is not null)
        {
            _assetTabs.SelectedIndex = 1;
            ShowSourceInspector();
        }
        _output.Text = opened.Warning ?? "Opened " + opened.Project.Document.Name +
            (opened.Connection is null ? "." : $". Client sources connected ({opened.Inventory.Length:N0} entries).");
    }

    private void ReplaceProject(StudioProject candidate)
    {
        _project?.Dispose();
        _project = candidate;
        _projectName.Text = candidate.Document.Name;
        _inspectedAsset = null;
        _activeWorkspaceTab = 0;
        _clientWorkspace.Reset();
        _assetTabs.SelectedIndex = 0;
        ClearPreview();
        _metadata.Text = "Select a texture to inspect its properties.";
        _output.Text = "Project opened: " + candidate.Root;
    }

    private async Task OpenClientProject(CancellationToken token)
    {
        if (!await CanReplaceProject(token))
        {
            return;
        }

        string? repository = await StudioDialogs.PickFolder(this, "Open the OpenConquerClient repository");
        if (repository is not null)
        {
            await LoadProject(repository, token);
        }
    }

}
