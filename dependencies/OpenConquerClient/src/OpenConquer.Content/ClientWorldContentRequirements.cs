using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content;

internal static class ClientWorldContentRequirements
{
    public static void Add(List<ClientContentRequirement> requirements, IClientContentSource source,
        GameMapCatalog catalog, uint mapDataId)
    {
        GameMapContent map = GameMapContent.Load(source, catalog, mapDataId);
        GameMapPuzzleResources puzzles = GameMapPuzzleResources.Load(source, map);
        GameMapTerrainSceneResources terrain = GameMapTerrainSceneResources.Load(source, map);

        Add(map.ArchivePath, ContentLookupMode.LooseOnly);

        HashSet<string> puzzlePaths = new(StringComparer.OrdinalIgnoreCase);
        if (puzzles.Ground is not null)
        {
            AddPuzzle(map.CellGrid.PuzzlePath);
        }

        AniSectionResolver animatedSections = new(source, ContentLookupMode.LooseOnly);
        foreach (GameMapPositionedOverlayRecord record in map.Scene.PositionedOverlays)
        {
            foreach (GameMapPositionedOverlayEntry entry in record.Entries)
            {
                if (entry is GameMapPuzzleOverlayEntry puzzle)
                {
                    AddPuzzle(puzzle.PuzzlePath);
                }
                else if (entry is GameMapAnimatedOverlayEntry animated)
                {
                    AddAnimation(animated.ResourcePath, animated.PartName);
                }
            }
        }

        foreach (GameMapSceneryAniBinding binding in GameMapSceneryAniBindings.Load(source, map.Scene))
        {
            Add(binding.Scenery.ResourcePath!, ContentLookupMode.LooseOnly);
            AddFrames(binding.Animation);
        }

        HashSet<string> scenePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (GameMapTerrainScenePlacement placement in terrain.Placements)
        {
            string scenePath = placement.Scenery.ResourcePath!;
            if (!scenePaths.Add(scenePath))
            {
                continue;
            }

            Add(scenePath, ContentLookupMode.LooseOnly);
            foreach (GameMapTerrainObjectBinding binding in placement.Objects)
            {
                Add(binding.Terrain.AnimationPath, ContentLookupMode.LooseOnly);
                AddFrames(binding.Animation);
            }
        }

        void AddPuzzle(string path)
        {
            if (!puzzlePaths.Add(path))
            {
                return;
            }

            Add(path, ContentLookupMode.LooseOnly);
            GameMapPuzzleBitmap bitmap = puzzles.GetRequired(path);
            Add(bitmap.TextureIndexPath, ContentLookupMode.LooseOnly);

            HashSet<ushort> tileIndices = [];
            foreach (ushort tileIndex in bitmap.Tiles)
            {
                if (tileIndex == ushort.MaxValue || !tileIndices.Add(tileIndex))
                {
                    continue;
                }

                AddFrames(puzzles.GetRequiredTileAnimation(path, tileIndex));
            }
        }

        void AddAnimation(string path, string sectionName)
        {
            AniIndexSection animation = animatedSections.Resolve(path, sectionName);
            Add(path, ContentLookupMode.LooseOnly);
            AddFrames(animation);
        }

        void AddFrames(AniIndexSection animation)
        {
            foreach (string framePath in animation.FramePaths)
            {
                Add(framePath, ContentLookupMode.LooseThenPackage);
            }
        }

        void Add(string path, ContentLookupMode lookupMode) =>
            requirements.Add(new ClientContentRequirement(path, lookupMode));
    }
}
