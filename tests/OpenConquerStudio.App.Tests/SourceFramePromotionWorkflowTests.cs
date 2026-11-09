using System.Buffers.Binary;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenConquer.Content.Map;
using OpenConquerStudio.Authoring;
using OpenConquerStudio.ConquerImport;
using OpenConquerStudio.Tests;

namespace OpenConquerStudio.App.Tests;

public sealed partial class StudioWindowTests
{
    [Fact]
    public Task TerrainInspectorPromotesSelectedFrameAsAnUndoableAuthoredTexture() =>
        s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        Guid terrain;
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            terrain = project.ImportTerrain(CreateCapturedTerrain(), "Ground");
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            ListBox sourceFrames = Find<ListBox>(window, "TerrainSourceFrameSelection");
            Assert.Single(sourceFrames.ItemsSource!);
            Assert.True(sourceFrames.IsEffectivelyVisible);
            Button promote = Find<Button>(window, "PromoteTerrainFrames");
            Assert.False(promote.IsEnabled);
            sourceFrames.SelectedIndex = 0;
            Assert.True(promote.IsEnabled);
            promote.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            TextureAsset texture = Assert.Single(window.Project.Document.Assets);
            Assert.Equal(texture.Id, window.Project.Document.Terrains.Single()
                .Source!.Frames[0].TextureId);
            Assert.Equal(terrain, window.Project.Document.Terrains.Single().Id);
            Assert.True(window.Project.IsModified);

            Find<Button>(window, "Undo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Empty(window.Project.Document.Assets);
            Assert.Null(window.Project.Document.Terrains.Single().Source!.Frames[0].TextureId);
            Find<Button>(window, "Redo").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await window.PendingOperation;
            Assert.Equal(texture.Id, Assert.Single(window.Project.Document.Assets).Id);

            await window.ExecuteAsync(token =>
            {
                window.Project.Save(token);
                return Task.CompletedTask;
            }, commitEdits: false);
            Assert.False(window.Project.IsModified);
        }
        finally
        {
            window.Close();
        }

        using StudioProject reopened = StudioProject.Open(directory.Project,
            TestContext.Current.CancellationToken);
        Assert.Single(reopened.Document.Assets);
        Assert.NotNull(reopened.Document.Terrains.Single().Source!.Frames[0].TextureId);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TerrainWithoutSourceFramesDoesNotShowPromotionControls() =>
        s_session.Value.Dispatch(async () =>
    {
        using ProjectDirectory directory = new();
        using (StudioProject project = StudioProject.Create(directory.Project, "Game"))
        {
            project.CreateTerrain("Ground", 9100, 1, 1, new TerrainCell(1, 0, 0));
            project.Save(TestContext.Current.CancellationToken);
        }

        StudioWindow window = new();
        window.Show();
        try
        {
            await window.ExecuteAsync(token => window.LoadProject(directory.Manifest, token));
            Assert.False(Find<Button>(window, "PromoteTerrainFrames").IsEffectivelyVisible);
            Assert.False(Find<ListBox>(window, "TerrainSourceFrameSelection").IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    private static TerrainImport CreateCapturedTerrain()
    {
        using MemoryStream stream = new();
        byte[] header = new byte[276];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1000);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), 77);
        Encoding.Latin1.GetBytes("puzzles/ground.pul").CopyTo(header, 8);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(268), 1);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(272), 1);
        stream.Write(header);
        using (BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true))
        {
            writer.Write((ushort)1);
            writer.Write((ushort)5);
            writer.Write((short)0);
            writer.Write((uint)18);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
        }
        byte[] map = stream.ToArray();
        GameMapCellGrid grid = GameMapCellGrid.Parse(map);
        GameMapSceneSections sections = GameMapSceneSections.Parse(map, grid.BytesConsumed, grid.HeaderField0);
        byte[] puzzle = new byte[274];
        "PUZZLE\0\0"u8.CopyTo(puzzle);
        Encoding.Latin1.GetBytes("ani/puzzle.ani").CopyTo(puzzle, 8);
        BinaryPrimitives.WriteInt32LittleEndian(puzzle.AsSpan(264), 1);
        BinaryPrimitives.WriteInt32LittleEndian(puzzle.AsSpan(268), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(puzzle.AsSpan(272), 2);
        byte[] tga = new byte[23];
        tga[2] = 10;
        tga[12] = 1;
        tga[14] = 1;
        tga[16] = 32;
        tga[17] = 8;
        tga[18] = 0x80;
        tga[21] = 21;
        tga[22] = 255;
        return ClientTerrainImporter.Read(9100, "maps/ground.dmap", 42, grid, sections,
            TestContext.Current.CancellationToken) with
        {
            DecodedSource = map,
            PuzzleFiles = [new("puzzles/ground.pul", puzzle)],
            SceneFiles = [],
            AnimationFiles = [new("ani/puzzle.ani", Encoding.Latin1.GetBytes(
                "[Puzzle2]\nFrameAmount=1\nFrame0=images/shared.tga\n"))],
            FrameFiles = [new("images/shared.tga", tga)]
        };
    }
}
