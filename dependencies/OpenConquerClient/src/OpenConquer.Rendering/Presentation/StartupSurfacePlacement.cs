namespace OpenConquer.Rendering.Presentation;

/// <summary>
/// Where the startup logo is drawn inside a startup window's framebuffer.
/// </summary>
public readonly record struct StartupSurfacePlacement(int Width, int Height)
{
    /// <summary>
    /// Computes the destination size for an image of <paramref name="imageWidth"/> by <paramref name="imageHeight"/> logical units.
    /// </summary>
    public static StartupSurfacePlacement Compute(int framebufferWidth, int framebufferHeight, int logicalWidth, int logicalHeight, int imageWidth, int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebufferHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        double horizontalScale = (double)framebufferWidth / logicalWidth;
        double verticalScale = (double)framebufferHeight / logicalHeight;

        return new StartupSurfacePlacement(Scale(imageWidth, horizontalScale), Scale(imageHeight, verticalScale));
    }

    private static int Scale(int lengthInLogicalUnits, double scale)
    {
        double scaledLength = Math.Round(lengthInLogicalUnits * scale, MidpointRounding.AwayFromZero);
        return scaledLength >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)scaledLength);
    }
}
