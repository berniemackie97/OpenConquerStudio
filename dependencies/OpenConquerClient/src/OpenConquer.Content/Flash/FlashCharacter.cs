namespace OpenConquer.Content.Flash;

/// <summary>A display character of a baked movie.</summary>
public abstract record FlashCharacter(int Id);

/// <summary>A shape or static text, drawn as ordered raster pieces.</summary>
public sealed record FlashRasterCharacter(int Id, IReadOnlyList<FlashRasterPiece> Pieces, bool IsStaticText) : FlashCharacter(Id);

/// <summary>One raster of a character; its pixel (0,0) is the character-space point (OriginX, OriginY).</summary>
public sealed record FlashRasterPiece(int Raster, float OriginX, float OriginY, FlashPixelSnap Snap);

/// <summary>
/// Where Flash puts an untransformed raster's first pixel when its origin falls between device pixels
/// (20_notes Architecture/12_create_role_flow.md, "Pixel placement").
/// </summary>
public enum FlashPixelSnap
{
    /// <summary>Texel under the pixel centre: first pixel <c>ceil(x - 0.5)</c>. Scaled bitmap fills and vector art.</summary>
    PixelCenter,

    /// <summary>Identity bitmap fill: first pixel <c>ceil(x)</c>.</summary>
    PixelCorner,
}

/// <summary>A morph shape, baked as one raster per ratio its timelines use.</summary>
public sealed record FlashMorphCharacter(int Id, float OriginX, float OriginY, IReadOnlyDictionary<int, int> RasterByRatio) : FlashCharacter(Id);

/// <summary>
/// An embedded video stream (<c>DefineVideoStream</c>), baked as one opaque raster per <c>VideoFrame</c>. A
/// placement ratio selects the frame; a ratio without a frame of its own shows the last frame before it, as the Flash
/// decoder holds its last decoded picture.
/// </summary>
public sealed record FlashVideoCharacter(int Id, int Width, int Height, IReadOnlyDictionary<int, int> RasterByFrame) : FlashCharacter(Id)
{
    /// <summary>The raster shown for <paramref name="ratio"/>, or null before the stream's first frame.</summary>
    public int? RasterAt(int ratio)
    {
        int best = -1;
        foreach (int frame in RasterByFrame.Keys)
        {
            if (frame <= ratio && frame > best)
            {
                best = frame;
            }
        }

        return best < 0 ? null : RasterByFrame[best];
    }
}

/// <summary>A movie clip with its own timeline.</summary>
public sealed record FlashSpriteCharacter(int Id, FlashTimeline Timeline) : FlashCharacter(Id);

/// <summary>A dynamic or input text field. Text is drawn at run time with a device font.</summary>
public sealed record FlashEditTextCharacter(int Id, FlashBounds Bounds, string Font, float FontHeight,
    byte Red, byte Green, byte Blue, byte Alpha, FlashTextAlignment Alignment, float LeftMargin, float RightMargin,
    float Indent, float Leading, int MaxLength, string Variable, string InitialText, bool ReadOnly, bool Password,
    bool Multiline, bool WordWrap, bool Html, bool NoSelect, bool Border, bool AutoSize) : FlashCharacter(Id);

public enum FlashTextAlignment
{
    Left = 0,
    Right = 1,
    Center = 2,
    Justify = 3,
}

/// <summary>A button: each record shows a character in some of its up, over, down and hit states.</summary>
public sealed record FlashButtonCharacter(int Id, IReadOnlyList<FlashButtonRecord> Records) : FlashCharacter(Id);

[Flags]
public enum FlashButtonStates
{
    None = 0,
    Up = 1,
    Over = 2,
    Down = 4,
    Hit = 8,
}

public sealed record FlashButtonRecord(int CharacterId, int Depth, FlashButtonStates States, FlashMatrix Matrix, FlashColorTransform ColorTransform);
