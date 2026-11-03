namespace OpenConquer.Rendering.Text.Fonts.FreeType;

/// <summary>
/// Represents an expected failure while opening or configuring a FreeType font face.
/// </summary>
internal sealed class FreeTypeFaceCreationException(string message) : InvalidOperationException(message);
