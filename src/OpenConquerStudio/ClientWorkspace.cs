using System.Collections.Immutable;
using OpenConquer.Content.Map;
using OpenConquer.Content.Mesh;
using OpenConquer.Rendering.World;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio;

internal sealed class ClientWorkspace
{
    private readonly StudioWindow _owner;
    private readonly AssetPreview _preview;
    private readonly ClientContentPanel _clientContent;
    private ClientProjectConnection? _connection;
    private WorldMapScene? _sourceMap;

    public ClientWorkspace(StudioWindow owner, AssetPreview preview)
    {
        _owner = owner;
        _preview = preview;
        _clientContent = new ClientContentPanel(PreviewAsset, ImportAsset, MoveCamera, _owner.ClearSourcePreview);
    }

    private void MoveCamera(int x, int y)
    {
        _ = _owner.ExecuteAsync(_ =>
        {
            _sourceMap?.SetCameraTile(x, y);
            if (_sourceMap is not null)
            {
                GameMapCell cell = _sourceMap.Map.CellGrid.GetCell(x, y);
                _clientContent.Describe(ClientProjectConnection.DescribeMap(_sourceMap.Map) +
                    $"\nTile {x}, {y}: surface {cell.SurfaceId}, passability flag {cell.PassabilityFlag}, elevation {cell.Elevation}");
            }
            _preview.RefreshMap();
            return Task.CompletedTask;
        });
    }

    public ClientContentPanel Panel => _clientContent;

    public void Attach(ClientProjectConnection connection, ImmutableArray<ClientAssetEntry> inventory)
    {
        _sourceMap = null;
        _connection = connection;
        _clientContent.Load(inventory, connection.ContentRoot);
    }

    public void Reset()
    {
        _connection = null;
        _sourceMap = null;
        _clientContent.Reset();
    }

    public async Task ChangeContentFolder(CancellationToken token)
    {
        string? repository = _connection?.Repository ?? await StudioDialogs.PickFolder(_owner, "Select the OpenConquerClient repository");
        if (repository is null)
        {
            return;
        }

        string? source = await StudioDialogs.PickFolder(_owner, "Select client content containing ini/GameMap.dat (payload or original installation)");
        if (source is not null)
        {
            ClientProjectConnection connection = await Task.Run(() => ClientProjectConnection.Attach(_owner.Project, repository, source, token), token);
            ImmutableArray<ClientAssetEntry> inventory = await Task.Run(() => connection.Inventory(token), token);
            bool worldPreview = _preview.IsWorldModelActive;
            Attach(connection, inventory);
            if (worldPreview && _owner.SelectedAsset is { Kind: "model" } selected)
            {
                _preview.Clear();
                await _owner.SelectAsset(selected.Id, token);
            }
            _owner.Diagnostic = $"Connected client content: {connection.ContentRoot}\n{inventory.Length} source entries.";
        }
    }

    public void PreviewOnMap(C3ModelPreview model)
    {
        if (_sourceMap is null || _connection is null)
        {
            throw new InvalidOperationException("Open a map in Client content first, then select the authored character.");
        }
        _preview.LoadMap(_sourceMap, _connection.Source, model);
        _owner.Diagnostic = "Character preview uses the map's native camera, scenery depth order and geometric shadow. Placement is a preview, not a saved map edit.";
    }

    private Task PreviewAsset(ClientAssetEntry entry, Func<ClientAssetOptions> readOptions) => _owner.ExecuteAsync(async token =>
    {
        ClientAssetOptions options = readOptions();
        string? section = options.Section;
        int duration = options.FrameMilliseconds;
        ClientProjectConnection connection = _connection ?? throw new InvalidOperationException("Open a client project first.");
        _sourceMap = null;
        _preview.Clear();
        _clientContent.Describe(entry.Path);
        _owner.ShowSourceInspector();
        if (entry.Kind is "model" or "character")
        {
            C3ModelPreview model = await Task.Run(() =>
            {
                C3ModelData data = entry.Kind == "character" ? CharacterImporter.Read(connection.Source, entry.Appearance!.Value, options.HairStyle, options.BodyModelId, options.ArmetModelId, token)
                    : connection.ReadModel(entry.Path, token);
                return C3ModelPreview.Create(data, data.Definition.Clips.IsEmpty ? -1 : 0);
            }, token);
            _preview.LoadModel(model);
            string details = $"{entry.Path} · {model.Parts.Length} part(s) · {model.FrameCount} frames\n" +
                $"{model.Parts.Sum(p => p.Tracks.Length)} native matrix tracks. Import to edit/save clip settings and inspect bones.";
            _clientContent.Describe(details);
            _owner.Diagnostic = details;
            return;
        }
        if (entry.Kind is not ("map" or "puzzle" or "animation" or "texture"))
        {
            string details = await Task.Run(() => connection.DescribeSource(entry), token);
            _clientContent.Describe(details);
            _owner.Diagnostic = details;
            return;
        }
        if (entry.Kind == "map")
        {
            GameMapContent map = await Task.Run(() => connection.ReadMap(entry.MapDataId!.Value), token);
            int x = Math.Min(65535, map.CellGrid.Width / 2);
            int y = Math.Min(65535, map.CellGrid.Height / 2);
            _sourceMap = await Task.Run(() => WorldMapScene.Load(connection.Source, map, (ushort)x, (ushort)y, unchecked((uint)Environment.TickCount), token), token);
            _clientContent.SetCamera(x, y, map.CellGrid.Width, map.CellGrid.Height);
            string details = ClientProjectConnection.DescribeMap(map);
            int omitted = map.Scene.PositionedOverlays.Sum(group => group.Entries.OfType<GameMapAnimatedOverlayEntry>().Count());
            _clientContent.Describe(details);
            _preview.LoadMap(_sourceMap, connection.Source);
            _owner.Diagnostic = details + $"\nSource map preview uses the client's shared puzzle/terrain renderer. {omitted} positioned animated overlays are not rendered by that implementation. Map editing is not implemented. Character assets can be previewed separately from the character catalog.";
            return;
        }

        if (entry.Kind == "puzzle")
        {
            GameMapPuzzleBitmap puzzle = await Task.Run(() => connection.ReadPuzzle(entry.Path), token);
            _preview.Clear();
            _clientContent.Describe($"{entry.Path}\n{puzzle.Columns} × {puzzle.Rows} puzzle tiles\nANI: {puzzle.TextureIndexPath}\nScroll velocity: {puzzle.ScrollVelocityX}, {puzzle.ScrollVelocityY}\nOpen its map to preview placement and layering.");
            return;
        }

        ImmutableArray<ImportedImageFrame> images;
        if (entry.Kind == "animation")
        {
            if (section is null)
            {
                ImmutableArray<string> sections = await Task.Run(() => connection.AnimationSections(entry.Path), token);
                section = _clientContent.SetSections(sections);
            }

            if (section is null)
            {
                throw new InvalidDataException("The ANI file has no sections.");
            }

            images = await Task.Run(() => connection.ReadAnimation(entry.Path, section, token), token);
        }
        else
        {
            ImportedImageFrame image = await Task.Run(() => connection.ReadImage(entry.Path, token), token);
            images = [image];
        }

        ImmutableArray<ViewportSprite> frames = [.. images.Select(image => new ViewportSprite(Guid.NewGuid(), image.Texture,
            new SpriteRegion(Guid.NewGuid(), image.Name, 0, 0, image.Texture.Width, image.Texture.Height, image.Texture.Width / 2, image.Texture.Height / 2)))];
        SpriteAnimation? animation = entry.Kind == "animation" ? new SpriteAnimation(Guid.NewGuid(), 1000, true,
            [.. frames.Select(frame => new AnimationFrame(Guid.NewGuid(), Guid.NewGuid(), frame.Region.Id, duration))]) : null;
        _preview.Load(frames, animation);
        _clientContent.Describe($"{entry.Path}\n{section}\n{images.Length} image frame(s). ANI ordering is retained; choose authored timing explicitly before import.");
        _owner.Diagnostic = "Previewing actual client source content. Import an editable copy to save and compile changes.";
    });

    private Task ImportAsset(ClientAssetEntry entry, Func<ClientAssetOptions> readOptions, bool replace) => _owner.ExecuteAsync(async token =>
    {
        ClientAssetOptions options = readOptions();
        string? section = options.Section;
        int duration = options.FrameMilliseconds;
        ClientProjectConnection connection = _connection ?? throw new InvalidOperationException("Open a client project first.");
        Guid id;
        if (entry.Kind is "model" or "character")
        {
            C3ModelData model = await Task.Run(() => entry.Kind == "character"
                ? CharacterImporter.Read(connection.Source, entry.Appearance!.Value, options.HairStyle, options.BodyModelId, options.ArmetModelId, token)
                : connection.ReadModel(entry.Path, token), token);
            id = await Task.Run(() => _owner.Project.ImportModel(entry.Path, model), token);
        }
        else if (entry.Kind == "animation")
        {
            if (section is null)
            {
                throw new InvalidOperationException("Select an ANI section first.");
            }

            ImmutableArray<ImportedImageFrame> images = await Task.Run(() => connection.ReadAnimation(entry.Path, section, token), token);
            id = _owner.Project.ImportAnimation(section, images, duration);
        }
        else
        {
            ImportedImageFrame image = await Task.Run(() => connection.ReadImage(entry.Path, token), token);
            if (replace)
            {
                if (_owner.SelectedAsset is not ResourceInfo { Kind: "texture" } selected)
                {
                    throw new InvalidOperationException("Select the target texture in Authored, then return to Client content and replace it.");
                }

                id = selected.Id;
                _owner.Project.ReplaceSourceImage(id, image);
            }
            else
            {
                id = _owner.Project.ImportSourceImage(image);
            }
        }

        await _owner.SelectAsset(id, token);
        _owner.Diagnostic = $"Imported client content into editable resource {id}. Save and build to produce runtime assets. The source client files remain unchanged.";
    });

}
