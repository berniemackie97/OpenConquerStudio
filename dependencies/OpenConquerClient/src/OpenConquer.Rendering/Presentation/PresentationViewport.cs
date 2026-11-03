namespace OpenConquer.Rendering.Presentation;

/// <summary>
/// Where the fixed logical frame lands inside the desktop game framebuffer, and how to get back again.
/// </summary>
public readonly record struct PresentationViewport
{
    private PresentationViewport(int offsetX, int offsetY, int width, int height, int hostWidth, int hostHeight, int logicalWidth, int logicalHeight, PresentationFilter filter)
    {
        HostWidth = hostWidth;
        HostHeight = hostHeight;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Width = width;
        Height = height;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        Filter = filter;
    }

    /// <summary>Left edge of the destination rectangle, in host framebuffer pixels.</summary>
    public int OffsetX
    {
        get;
    }

    /// <summary>Bottom edge of the destination rectangle, in host framebuffer pixels.</summary>
    public int OffsetY
    {
        get;
    }

    /// <summary>Width of the destination rectangle, in host framebuffer pixels.</summary>
    public int Width
    {
        get;
    }

    /// <summary>Height of the destination rectangle, in host framebuffer pixels.</summary>
    public int Height
    {
        get;
    }

    /// <summary>Width of the host framebuffer this viewport was computed for, in pixels.</summary>
    public int HostWidth
    {
        get;
    }

    /// <summary>Height of the host framebuffer this viewport was computed for, in pixels.</summary>
    public int HostHeight
    {
        get;
    }

    /// <summary>Width of the logical frame being presented.</summary>
    public int LogicalWidth
    {
        get;
    }

    /// <summary>Height of the logical frame being presented.</summary>
    public int LogicalHeight
    {
        get;
    }

    /// <summary>How the copy should be resampled.</summary>
    public PresentationFilter Filter
    {
        get;
    }

    /// <summary>Whether this viewport has no drawable area, as when the host window is minimised.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Horizontal magnification applied to the logical frame.</summary>
    public double ScaleX => IsEmpty ? 0d : (double)Width / LogicalWidth;

    /// <summary>Vertical magnification applied to the logical frame.</summary>
    public double ScaleY => IsEmpty ? 0d : (double)Height / LogicalHeight;

    /// <summary> Whether the destination rectangle fills the whole host framebuffer. </summary>
    public bool CoversHostFramebuffer()
    {
        return OffsetX == 0 && OffsetY == 0 && Width == HostWidth && Height == HostHeight;
    }

    /// <summary>
    /// Places <paramref name="logical"/> inside a host framebuffer according to  <paramref name="policy"/>.
    /// </summary>
    public static PresentationViewport Compute(LogicalRenderSize logical, int hostFramebufferWidth, int hostFramebufferHeight, PresentationPolicy policy = PresentationPolicy.Fit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hostFramebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(hostFramebufferHeight);

        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown presentation policy.");
        }

        int logicalWidth = logical.Width;
        int logicalHeight = logical.Height;

        if (logicalWidth <= 0 || logicalHeight <= 0 || hostFramebufferWidth == 0 || hostFramebufferHeight == 0)
        {
            return new PresentationViewport(0, 0, 0, 0, hostFramebufferWidth, hostFramebufferHeight, logicalWidth, logicalHeight, PresentationFilter.Nearest);
        }

        (int width, int height) = policy switch
        {
            PresentationPolicy.Stretch => (hostFramebufferWidth, hostFramebufferHeight),
            PresentationPolicy.IntegerScale => FitWhole(logicalWidth, logicalHeight, hostFramebufferWidth, hostFramebufferHeight),
            _ => FitUniform(logicalWidth, logicalHeight, hostFramebufferWidth, hostFramebufferHeight),
        };

        int offsetX = (hostFramebufferWidth - width) / 2;
        int offsetY = (hostFramebufferHeight - height) / 2;

        return new PresentationViewport(offsetX, offsetY, width, height, hostFramebufferWidth, hostFramebufferHeight, logicalWidth, logicalHeight,
            SelectFilter(width, height, logicalWidth, logicalHeight));
    }

    /// <summary>
    /// Converts a pointer position to the logical pixel underneath it.
    /// </summary>
    public bool TryMapPointerToLogical(int pointerX, int pointerY, out int logicalX, out int logicalY)
    {
        if (IsEmpty)
        {
            logicalX = 0;
            logicalY = 0;

            return false;
        }

        long flippedPointerY = (long)HostHeight - 1 - pointerY;

        if (flippedPointerY is < 0 or > int.MaxValue)
        {
            logicalX = 0;
            logicalY = 0;

            return false;
        }

        if (!TryMapFramebufferPointToLogical(pointerX, (int)flippedPointerY, out logicalX, out int bottomUpLogicalY))
        {
            logicalY = 0;

            return false;
        }

        logicalY = LogicalHeight - 1 - bottomUpLogicalY;

        return true;
    }

    /// <summary>Clamped horizontal drag mapping, including letterbox margins and offscreen pointer positions.</summary>
    public int MapPointerXClamped(int pointerX)
    {
        if (IsEmpty)
        {
            return 0;
        }

        long relativeX = (long)pointerX - OffsetX;
        return (int)Math.Clamp(relativeX * LogicalWidth / Width, 0L, LogicalWidth - 1L);
    }

    /// <summary>
    /// Converts a host framebuffer pixel to the logical pixel drawn there.
    /// </summary>
    public bool TryMapFramebufferPointToLogical(int hostFramebufferX, int hostFramebufferY, out int logicalX, out int logicalY)
    {
        logicalX = 0;
        logicalY = 0;

        if (IsEmpty)
        {
            return false;
        }

        long relativeX = (long)hostFramebufferX - OffsetX;
        long relativeY = (long)hostFramebufferY - OffsetY;

        if (relativeX < 0 || relativeX >= Width || relativeY < 0 || relativeY >= Height)
        {
            return false;
        }

        logicalX = (int)(relativeX * LogicalWidth / Width);
        logicalY = (int)(relativeY * LogicalHeight / Height);

        return true;
    }

    /// <summary>
    /// Largest rectangle with the logical aspect ratio that fits the host framebuffer.
    /// </summary>
    private static (int Width, int Height) FitUniform(int logicalWidth, int logicalHeight, int hostWidth, int hostHeight)
    {
        if ((long)hostWidth * logicalHeight <= (long)hostHeight * logicalWidth)
        {
            int height = (int)((long)hostWidth * logicalHeight / logicalWidth);

            return (hostWidth, Math.Max(height, 1));
        }

        int width = (int)((long)hostHeight * logicalWidth / logicalHeight);

        return (Math.Max(width, 1), hostHeight);
    }

    /// <summary>
    /// Largest whole-number magnification of the logical frame that fits the host framebuffer.
    /// </summary>
    private static (int Width, int Height) FitWhole(int logicalWidth, int logicalHeight, int hostWidth, int hostHeight)
    {
        int scale = Math.Min(hostWidth / logicalWidth, hostHeight / logicalHeight);

        return scale < 1 ? FitUniform(logicalWidth, logicalHeight, hostWidth, hostHeight) : (logicalWidth * scale, logicalHeight * scale);
    }

    /// <summary>
    /// Chooses point sampling whenever the copy reproduces the logical frame exactly.
    /// </summary>
    private static PresentationFilter SelectFilter(int width, int height, int logicalWidth, int logicalHeight)
    {
        if (width % logicalWidth != 0 || height % logicalHeight != 0)
        {
            return PresentationFilter.Linear;
        }

        int horizontalScale = width / logicalWidth;
        int verticalScale = height / logicalHeight;

        return horizontalScale == verticalScale && horizontalScale >= 1 ? PresentationFilter.Nearest : PresentationFilter.Linear;
    }
}
