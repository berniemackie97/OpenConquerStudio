using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Meshes;

public sealed unsafe class OpenGLIndexedMesh : IDisposable
{
    public const int VertexStride = 36;

    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;
    private readonly int _vertexCount;
    private readonly uint _indexCount;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private bool _hasVertexData;
    private bool _disposed;

    internal OpenGLIndexedMesh(GL gl, int vertexCount, ReadOnlySpan<ushort> indices)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(vertexCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(vertexCount, ushort.MaxValue + 1);

        if (indices.IsEmpty || indices.Length % 3 != 0)
        {
            throw new ArgumentException("Indexed mesh requires complete triangles.", nameof(indices));
        }

        foreach (ushort index in indices)
        {
            if (index >= vertexCount)
            {
                throw new ArgumentException("Triangle index exceeds the mesh vertex count.", nameof(indices));
            }
        }

        _gl = gl;
        _cleanup = new OpenGLDrawStateCleanup(gl);
        _vertexCount = vertexCount;
        _indexCount = (uint)indices.Length;

        try
        {
            CreateResources(indices);
        }
        catch
        {
            DestroyResources();
            throw;
        }
    }

    public int VertexCount => _vertexCount;

    public int IndexCount => (int)_indexCount;

    public void Upload(ReadOnlySpan<byte> vertexBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (vertexBytes.Length != checked(_vertexCount * VertexStride))
        {
            throw new ArgumentException("Vertex data must match the mesh's 36-byte vertex layout.", nameof(vertexBytes));
        }

        ExceptionDispatchInfo? failure = null;
        try
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            fixed (byte* pointer = vertexBytes)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)vertexBytes.Length, pointer);
            }
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterInitialization(failure);
        _hasVertexData = true;
    }

    internal void ValidateOwner(GL gl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(gl, _gl))
        {
            throw new ArgumentException("The indexed mesh belongs to a different graphics device.");
        }
    }

    internal void Bind()
    {
        if (!_hasVertexData)
        {
            throw new InvalidOperationException("Indexed mesh vertices must be uploaded before drawing.");
        }

        _gl.BindVertexArray(_vertexArray);
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

    private void CreateResources(ReadOnlySpan<ushort> indices)
    {
        ExceptionDispatchInfo? failure = null;
        try
        {
            _vertexArray = _gl.GenVertexArray();
            _vertexBuffer = _gl.GenBuffer();
            _indexBuffer = _gl.GenBuffer();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)checked(_vertexCount * VertexStride), null, BufferUsageARB.DynamicDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);
            fixed (ushort* indicesPointer = indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                    (nuint)(indices.Length * sizeof(ushort)), indicesPointer, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexStride, null);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, VertexStride, (void*)12);
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, VertexStride, (void*)24);
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, VertexStride, (void*)28);
        }
        catch (Exception exception)
        {
            failure = ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterInitialization(failure);
    }

    private void DestroyResources()
    {
        if (_indexBuffer != 0)
        {
            _gl.DeleteBuffer(_indexBuffer);
            _indexBuffer = 0;
        }

        if (_vertexBuffer != 0)
        {
            _gl.DeleteBuffer(_vertexBuffer);
            _vertexBuffer = 0;
        }

        if (_vertexArray != 0)
        {
            _gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }
    }
}
