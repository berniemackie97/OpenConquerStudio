using System.Runtime.InteropServices;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// Defines the tightly packed OpenGL vertex representation for native text rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct OpenGLTextVertex(NativeTextVertex vertex)
{
    public readonly float ScreenX = vertex.ScreenX;
    public readonly float ScreenY = vertex.ScreenY;
    public readonly float TextureU = vertex.TextureU;
    public readonly float TextureV = vertex.TextureV;
    public readonly byte Red = vertex.Color.Red;
    public readonly byte Green = vertex.Color.Green;
    public readonly byte Blue = vertex.Color.Blue;
    public readonly byte Alpha = vertex.Color.Alpha;
}
