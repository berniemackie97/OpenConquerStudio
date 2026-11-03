using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Rendering;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// Fixed-capacity OpenGL vertex buffers for uploading and drawing glyph batches.
/// </summary>
internal sealed unsafe class OpenGLTextVertexBuffer : IDisposable
{
    private readonly GL _gl;
    private readonly int _bufferSizeBytes;

    private uint _vertexArray;
    private uint _vertexBuffer;
    private bool _disposed;

    public OpenGLTextVertexBuffer(GL gl, int vertexCapacity)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vertexCapacity);

        _gl = gl;
        VertexCapacity = vertexCapacity;
        _bufferSizeBytes = checked(vertexCapacity * sizeof(OpenGLTextVertex));

        try
        {
            CreateResources();
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original vertex-resource creation failure.
            }

            throw;
        }
    }

    public int VertexCapacity
    {
        get;
    }

    public void Draw(ReadOnlySpan<OpenGLTextVertex> vertices)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (vertices.IsEmpty)
        {
            throw new ArgumentException("A native text vertex batch cannot be empty.", nameof(vertices));
        }

        if (vertices.Length % NativeTextGeometryBuilder.VerticesPerGlyphPass != 0)
        {
            throw new ArgumentException($"A native text vertex batch must contain complete {NativeTextGeometryBuilder.VerticesPerGlyphPass}-vertex glyph passes.", nameof(vertices));
        }

        if (vertices.Length > VertexCapacity)
        {
            throw new ArgumentException($"The native text vertex batch contains {vertices.Length} vertices, exceeding the {VertexCapacity}-vertex buffer capacity.", nameof(vertices));
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

            fixed (OpenGLTextVertex* vertexPointer = vertices)
            {
                nuint byteCount = checked((nuint)vertices.Length * (nuint)sizeof(OpenGLTextVertex));
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, byteCount, vertexPointer);
            }

            _gl.DrawArrays(PrimitiveType.Triangles, first: 0, (uint)vertices.Length);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindVertexArray(0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            DestroyResources();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void CreateResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _vertexArray = _gl.GenVertexArray();
            _vertexBuffer = _gl.GenBuffer();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)_bufferSizeBytes, null, BufferUsageARB.StreamDraw);

            int stride = sizeof(OpenGLTextVertex);

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, normalized: false, (uint)stride, (void*)0);

            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, normalized: false, (uint)stride, (void*)(2 * sizeof(float)));

            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, normalized: true, (uint)stride, (void*)(4 * sizeof(float)));
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindVertexArray(0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        uint vertexBuffer = _vertexBuffer;
        _vertexBuffer = 0;

        if (vertexBuffer != 0)
        {
            try
            {
                _gl.DeleteBuffer(vertexBuffer);
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint vertexArray = _vertexArray;
        _vertexArray = 0;

        if (vertexArray != 0)
        {
            try
            {
                _gl.DeleteVertexArray(vertexArray);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }
}
