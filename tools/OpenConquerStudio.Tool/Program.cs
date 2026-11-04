using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Runtime;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
try
{
    switch (args)
    {
        case ["create", string root, string name]:
            using (StudioProject created = StudioProject.Create(root, name))
            {
                Console.WriteLine(created.Document.Id);
            }

            break;
        case ["migrate", string source, string destination]:
            using (StudioProject project = StudioProject.Migrate(source, destination, cancellation.Token))
            {
                Console.WriteLine(project.Document.Id);
            }

            break;
        case ["connect-client", string repository]:
            var connected = ClientProjectConnection.OpenRepository(repository, cancellation.Token);
            using (connected.Project)
            {
                Console.WriteLine(connected.Project.Root);
                Console.WriteLine(connected.Connection.ContentRoot);
                Console.WriteLine($"{connected.Connection.Inventory(cancellation.Token).Length} source entries");
            }

            break;
        case ["client-assets", string root, string kind, string filter]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ClientProjectConnection connection = ClientProjectConnection.OpenAttached(root) ?? throw new InvalidOperationException("The project has no client connection.");
                foreach (ClientAssetEntry entry in connection.Inventory(cancellation.Token).Where(a => (kind == "all" || a.Kind == kind) && a.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"{entry.Kind} {entry}");
                }
            }

            break;
        case ["import-client-image", string root, string path]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ClientProjectConnection connection = ClientProjectConnection.OpenAttached(root) ?? throw new InvalidOperationException("The project has no client connection.");
                Guid id = project.ImportSourceImage(connection.ReadImage(path, cancellation.Token));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["import-client-animation", string root, string path, string section, string duration]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ClientProjectConnection connection = ClientProjectConnection.OpenAttached(root) ?? throw new InvalidOperationException("The project has no client connection.");
                Guid id = project.ImportAnimation(section, connection.ReadAnimation(path, section, cancellation.Token), Number(duration));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["import-c3", string root, string path, string name]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.ImportModelFile(path, name, cancellation.Token);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }
            break;
        case ["import-character", string root, string repository, string appearance, string hair]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ClientProjectConnection connection = ClientProjectConnection.OpenAttached(root) ?? ClientProjectConnection.Attach(project,
                    repository, Path.Combine(repository, "content", "retail-5517", "payload"), cancellation.Token);
                var model = CharacterImporter.Read(connection.Source, uint.Parse(appearance, CultureInfo.InvariantCulture),
                    uint.Parse(hair, CultureInfo.InvariantCulture), cancellation.Token);
                Guid id = project.ImportModel("Character " + appearance, model);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }
            break;
        case ["model-clip", string root, string id, string name, string clip, string milliseconds, string repeat]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.EditModel(Guid.Parse(id), name, Number(clip), Number(milliseconds), bool.Parse(repeat));
                project.Save(cancellation.Token);
            }
            break;
        case ["add-model-motion", string root, string id, string name, string path]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.AddModelMotion(Guid.Parse(id), name, path, cancellation.Token);
                project.Save(cancellation.Token);
            }
            break;
        case ["inspect-client-map", string root, string mapId]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ClientProjectConnection connection = ClientProjectConnection.OpenAttached(root) ?? throw new InvalidOperationException("The project has no client connection.");
                Console.WriteLine(ClientProjectConnection.DescribeMap(connection.ReadMap(uint.Parse(mapId, CultureInfo.InvariantCulture))));
            }

            break;
        case ["list", string root]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                foreach (ResourceInfo resource in project.Document.Resources)
                {
                    Console.WriteLine($"{resource.Id} {resource.Kind} {resource.Name}");
                }

                foreach (AtlasAsset atlas in project.Document.Atlases)
                {
                    foreach (SpriteRegion sprite in atlas.Sprites)
                    {
                        Console.WriteLine($"{sprite.Id} sprite {atlas.Id} {sprite.Name}");
                    }
                }

                foreach (AnimationAsset animation in project.Document.Animations)
                {
                    foreach (AnimationFrame frame in animation.Frames)
                    {
                        Console.WriteLine($"{frame.Id} frame {animation.Id} {frame.AtlasId}/{frame.SpriteId} {frame.DurationTicks}");
                    }
                }
            }

            break;
        case ["new-atlas", string root, string texture, string name]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.CreateAtlas(name, Guid.Parse(texture));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["sprite", string root, string atlasId, string spriteId, string name, string x, string y, string width, string height, string pivotX, string pivotY]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                AtlasAsset atlas = project.Document.Atlases.SingleOrDefault(a => a.Id == Guid.Parse(atlasId)) ?? throw new InvalidDataException("Atlas does not exist.");
                Guid id = spriteId == "new" ? Guid.NewGuid() : Guid.Parse(spriteId);
                SpriteRegion sprite = new(id, name, Number(x), Number(y), Number(width), Number(height), Number(pivotX), Number(pivotY));
                if (spriteId != "new" && !atlas.Sprites.Any(s => s.Id == id))
                {
                    throw new InvalidDataException("Sprite does not exist; use 'new' to add one.");
                }

                project.EditAtlas(atlas with
                {
                    Sprites = spriteId == "new" ? [.. atlas.Sprites, sprite] : [.. atlas.Sprites.Select(s => s.Id == id ? sprite : s)]
                });
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["new-animation", string root, string atlas, string name, string timebase, string duration]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.CreateAnimation(name, Guid.Parse(atlas), Number(timebase), Number(duration));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["clip", string root, string id, string name, string timebase, string repeat]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                AnimationAsset animation = project.Document.Animations.SingleOrDefault(a => a.Id == Guid.Parse(id)) ?? throw new InvalidDataException("Animation does not exist.");
                project.EditAnimation(animation with
                {
                    Name = name,
                    TicksPerSecond = Number(timebase),
                    Repeat = bool.Parse(repeat)
                });
                project.Save(cancellation.Token);
            }

            break;
        case ["frame", string root, string animationId, string frameId, string atlasId, string spriteId, string duration, string index]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                AnimationAsset animation = project.Document.Animations.SingleOrDefault(a => a.Id == Guid.Parse(animationId)) ?? throw new InvalidDataException("Animation does not exist.");
                Guid id = frameId == "new" ? Guid.NewGuid() : Guid.Parse(frameId);
                if (frameId != "new" && !animation.Frames.Any(f => f.Id == id))
                {
                    throw new InvalidDataException("Frame does not exist; use 'new' to add one.");
                }

                AnimationFrame frame = new(id, Guid.Parse(atlasId), Guid.Parse(spriteId), Number(duration));
                project.EditAnimation(animation with
                {
                    Frames = animation.Frames.Where(f => f.Id != id).ToImmutableArray().Insert(Number(index), frame)
                });
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["delete", string root, string id]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.Delete(Guid.Parse(id));
                project.Save(cancellation.Token);
            }

            break;
        case ["list-artwork"]:
            foreach (ArtworkSource source in ConquerArtworkImporter.Sources)
            {
                Console.WriteLine($"{source.Key}  {source.Name}");
            }

            break;
        case ["import-artwork", string root, string key]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = ConquerArtworkImporter.Import(project, key, cancellation.Token);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["import", string root, string source, string name]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.ImportTexture(source, name, cancellation.Token);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["new-terrain", string root, string name, string mapDataId, string width, string height, string surface, string passability, string elevation]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                TerrainCell fill = new(ushort.Parse(surface, CultureInfo.InvariantCulture),
                    ushort.Parse(passability, CultureInfo.InvariantCulture), short.Parse(elevation, CultureInfo.InvariantCulture));
                Guid id = project.CreateTerrain(name, uint.Parse(mapDataId, CultureInfo.InvariantCulture),
                    Number(width), Number(height), fill);
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["terrain-cell", string root, string id, string x, string y, string surface, string passability, string elevation]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid terrainId = Guid.Parse(id);
                TerrainAsset terrain = project.Document.Terrains.Single(a => a.Id == terrainId);
                TerrainCell cell = new(ushort.Parse(surface, CultureInfo.InvariantCulture),
                    ushort.Parse(passability, CultureInfo.InvariantCulture), short.Parse(elevation, CultureInfo.InvariantCulture));
                project.EditTerrainCell(terrainId, Number(x), Number(y), cell, terrain.Name);
                project.Save(cancellation.Token);
            }

            break;
        case ["build-terrain", string root, string id, string serverTool, string destination]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                await ServerTerrainBuild.BuildAsync(project, Guid.Parse(id), serverTool, destination, cancellation.Token);
                Console.WriteLine($"Validated server terrain artifacts published to {destination}.");
            }

            break;
        case ["export-base-terrain", string root, string id, string destination]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                TerrainSourceExporter.Export(project, Guid.Parse(id), destination, cancellation.Token);
                Console.WriteLine($"Exported terrain source for {id}.");
            }

            break;
        case ["new-texture", string root, string name, string width, string height, string red, string green, string blue]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                Guid id = project.CreateTexture(name, int.Parse(width, CultureInfo.InvariantCulture), int.Parse(height, CultureInfo.InvariantCulture),
                    byte.Parse(red, CultureInfo.InvariantCulture), byte.Parse(green, CultureInfo.InvariantCulture), byte.Parse(blue, CultureInfo.InvariantCulture));
                project.Save(cancellation.Token);
                Console.WriteLine(id);
            }

            break;
        case ["edit", string root, string id, string name, string flip, string turns]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.EditTexture(Guid.Parse(id), name, bool.Parse(flip), int.Parse(turns, CultureInfo.InvariantCulture));
                project.Save(cancellation.Token);
            }

            break;
        case ["replace", string root, string id, string source]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                project.ReplaceTexture(Guid.Parse(id), source, cancellation.Token);
                project.Save(cancellation.Token);
            }

            break;
        case ["build", string root, string destination]:
            using (StudioProject project = StudioProject.Open(root, cancellation.Token))
            {
                ContentBuild build = StudioCompiler.Compile(project, destination, cancellation.Token);
                Console.WriteLine($"Compiled and validated {build.Textures.Length} textures and {build.Resources.Length} resources.");
            }

            break;
        case ["verify", string directory]:
            ContentBuild verified = StudioCompiler.Validate(directory, cancellation.Token);
            Console.WriteLine($"Validated {verified.Textures.Length} textures and {verified.Resources.Length} resources for project {verified.ProjectId}.");
            break;
        default:
            Console.Error.WriteLine("Usage:\n  create <empty-directory> <name>\n  list-artwork\n  import-artwork <project-directory> <artwork-key>\n  import <project-directory> <source.tga|source.dds> <name>\n  new-texture <project-directory> <name> <width> <height> <red> <green> <blue>\n  edit <project-directory> <asset-id> <name> <flip-horizontal:true|false> <clockwise-quarter-turns:0..3>\n  replace <project-directory> <asset-id> <source.tga|source.dds>\n  build <project-directory> <new-output-directory>\n  verify <output-directory>");
            Console.Error.WriteLine("  new-terrain <project> <name> <map-data-id> <width> <height> <surface> <passability> <elevation>\n  terrain-cell <project> <terrain-id> <x> <y> <surface> <passability> <elevation>\n  export-base-terrain <project> <terrain-id> <new-source.json>\n  build-terrain <project> <terrain-id> <server-tool.dll> <new-output-directory>");
            Console.Error.WriteLine("  migrate <source-project> <empty-destination>\n  list <project>\n  new-atlas <project> <texture-id> <name>\n  sprite <project> <atlas-id> <sprite-id|new> <name> <x> <y> <width> <height> <pivot-x> <pivot-y>\n  new-animation <project> <atlas-id> <name> <ticks-per-second> <frame-duration>\n  clip <project> <animation-id> <name> <ticks-per-second> <repeat:true|false>\n  frame <project> <animation-id> <frame-id|new> <atlas-id> <sprite-id> <duration> <zero-based-position>\n  delete <project> <resource-id>");
            Console.Error.WriteLine("  connect-client <repository>\n  client-assets <project> <all|map|animation|texture|puzzle> <filter>\n  import-client-image <project> <virtual-path>\n  import-client-animation <project> <ani-path> <section> <frame-milliseconds>\n  inspect-client-map <project> <map-data-id>");
            Console.Error.WriteLine("  import-c3 <project> <mesh.c3> <name>\n  import-character <project> <client-repository> <body-appearance> <hair-style>\n  model-clip <project> <id> <name> <zero-based-clip> <milliseconds> <repeat>\n  add-model-motion <project> <id> <name> <motion.c3>");
            return 2;
    }

    return 0;
}

catch (OperationCanceledException)
{
    Console.Error.WriteLine("Operation canceled.");
    return 130;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or OverflowException or InvalidOperationException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);
