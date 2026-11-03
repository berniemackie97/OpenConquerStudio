using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// Reusable, fixed-capacity CPU memory for staging text vertex uploads.
/// </summary>
internal sealed class OpenGLTextVertexStagingBuffer
{
    private readonly OpenGLTextVertex[] _vertices;

    public OpenGLTextVertexStagingBuffer(int glyphPassCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(glyphPassCapacity);

        int vertexCapacity = checked(glyphPassCapacity * NativeTextGeometryBuilder.VerticesPerGlyphPass);

        GlyphPassCapacity = glyphPassCapacity;
        _vertices = new OpenGLTextVertex[vertexCapacity];
    }

    public int GlyphPassCapacity
    {
        get;
    }

    public int VertexCapacity => _vertices.Length;

    public int VertexCount
    {
        get; private set;
    }

    public ReadOnlySpan<OpenGLTextVertex> Vertices => _vertices.AsSpan(0, VertexCount);

    public bool TryAppendGlyphPass(NativeTextLayoutItem item, NativeTextRenderPass pass, int originXPixels, int originYPixels)
    {
        if (VertexCount > _vertices.Length - NativeTextGeometryBuilder.VerticesPerGlyphPass)
        {
            return false;
        }

        Span<NativeTextVertex> logicalVertices = stackalloc NativeTextVertex[NativeTextGeometryBuilder.VerticesPerGlyphPass];
        NativeTextGeometryBuilder.WriteGlyph(logicalVertices, item, pass, originXPixels, originYPixels);

        int destinationOffset = VertexCount;

        for (int index = 0; index < logicalVertices.Length; index++)
        {
            _vertices[destinationOffset + index] = new OpenGLTextVertex(logicalVertices[index]);
        }

        VertexCount += NativeTextGeometryBuilder.VerticesPerGlyphPass;
        return true;
    }

    public void Clear()
    {
        VertexCount = 0;
    }
}
