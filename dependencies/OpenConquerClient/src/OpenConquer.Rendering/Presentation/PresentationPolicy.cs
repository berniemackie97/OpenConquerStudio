namespace OpenConquer.Rendering.Presentation;

/// <summary>
/// Selects how the fixed logical frame is placed inside the desktop game framebuffer.
/// </summary>
public enum PresentationPolicy
{
    /// <summary>
    /// Scales by the largest uniform factor that fits and centres the result, leaving bars. Aspect ratio is preserved.
    /// </summary>
    Fit = 0,

    /// <summary>
    /// Scales by the largest whole number factor that fits and centres the result. Aspect ratio is preserved
    /// </summary>
    IntegerScale = 1,

    /// <summary>
    /// Stretches the logical frame across the whole host framebuffer, distorting it whenever the two aspect ratios differ.
    /// </summary>
    Stretch = 2,
}
