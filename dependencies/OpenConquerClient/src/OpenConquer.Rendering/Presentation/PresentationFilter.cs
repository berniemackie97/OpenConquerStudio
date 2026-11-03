namespace OpenConquer.Rendering.Presentation;

/// <summary>
/// How the logical frame is resampled when it is copied to the host framebuffer.
/// </summary>
public enum PresentationFilter
{
    /// <summary>
    /// Point sampling. Used whenever the copy is 1:1 or a whole number magnification, where it reproduces the logical frame exactly.
    /// </summary>
    Nearest = 0,

    /// <summary>
    /// Bilinear sampling, for fractional scales
    /// </summary>
    Linear = 1,
}
