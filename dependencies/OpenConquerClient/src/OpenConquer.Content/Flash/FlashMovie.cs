using OpenConquer.Content.Images;
using OpenConquer.Content.Media;

namespace OpenConquer.Content.Flash;

/// <summary>A straight-alpha RGBA raster of a baked movie and the SHA-256 of its decoded pixels.</summary>
public sealed record FlashRaster(string Path, int Width, int Height, string Sha256);

/// <summary>
/// A retail Flash movie converted offline into display characters, rasters and static timelines. Nothing here runs
/// ActionScript: the screen that plays a movie supplies its scripted behavior.
/// </summary>
public sealed class FlashMovie
{
    private readonly string _assetDirectory;
    private readonly IReadOnlyDictionary<int, FlashCharacter> _characters;

    internal FlashMovie(string name, string assetDirectory, float frameRate, int width, int height,
        IReadOnlyList<FlashRaster> rasters, IReadOnlyDictionary<int, FlashCharacter> characters, FlashTimeline root)
    {
        Name = name;
        _assetDirectory = assetDirectory;
        FrameRate = frameRate;
        Width = width;
        Height = height;
        Rasters = rasters;
        _characters = characters;
        Root = root;
    }

    public string Name
    {
        get;
    }

    public float FrameRate
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public IReadOnlyList<FlashRaster> Rasters
    {
        get;
    }

    public IEnumerable<FlashCharacter> Characters => _characters.Values;

    public FlashTimeline Root
    {
        get;
    }

    public FlashCharacter Character(int id) =>
        _characters.TryGetValue(id, out FlashCharacter? character)
            ? character
            : throw new KeyNotFoundException($"Movie '{Name}' has no character {id}.");

    /// <summary>Decodes raster <paramref name="index"/> and verifies its length and SHA-256.</summary>
    public RgbaImage LoadRaster(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Rasters.Count);

        FlashRaster raster = Rasters[index];
        return EmbeddedMedia.Load(new EmbeddedImage($"{_assetDirectory}/{raster.Path}", raster.Width, raster.Height, raster.Sha256));
    }
}
