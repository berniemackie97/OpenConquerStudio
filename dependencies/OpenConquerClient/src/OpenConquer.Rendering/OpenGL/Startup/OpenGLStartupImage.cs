using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Startup;

internal sealed unsafe class OpenGLStartupImage : IDisposable
{
    private const int FloatsPerVertex = 4;

    private static readonly uint[] s_indices = [0, 1, 2, 0, 2, 3];

    private readonly GL _gl;

    private OpenGLProgram? _program;
    private OpenGLTexture2D? _texture;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private int _textureUniform;
    private bool _disposed;

    public OpenGLStartupImage(GL gl, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;

        try
        {
            CreatePipeline(width, height, rgbaPixels);
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original pipeline-creation failure.
            }

            throw;
        }
    }

    /// <summary>
    /// Draws the image into the top left corner of the viewport at the requested device size.
    /// </summary>
    public void DrawTopLeft(int viewportWidth, int viewportHeight, int destinationWidth, int destinationHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(destinationHeight);

        float left = ToNormalizedX(0, viewportWidth);
        float right = ToNormalizedX(destinationWidth, viewportWidth);
        float top = ToNormalizedY(0, viewportHeight);
        float bottom = ToNormalizedY(destinationHeight, viewportHeight);

        Span<float> vertices =
        [
            left, top, 0f, 0f,
            right, top, 1f, 0f,
            right, bottom, 1f, 1f,
            left, bottom, 0f, 1f,
        ];

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The startup image program is unavailable.");
        OpenGLTexture2D texture = _texture ?? throw new InvalidOperationException("The startup image texture is unavailable.");

        _gl.Disable(EnableCap.DepthTest);
        _gl.DepthMask(false);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.Blend);

        program.Use();

        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

        fixed (float* vertexPointer = vertices)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
        }

        _gl.ActiveTexture(TextureUnit.Texture0);
        texture.Bind();
        _gl.Uniform1(_textureUniform, 0);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)s_indices.Length, DrawElementsType.UnsignedInt, null);

        _gl.BindVertexArray(0);
        _gl.DepthMask(true);
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

    private void CreatePipeline(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        _program = new OpenGLProgram(_gl, VertexShaderSource, FragmentShaderSource);
        _textureUniform = _program.GetRequiredUniformLocation("uTexture");
        _texture = new OpenGLTexture2D(_gl, width, height, rgbaPixels);

        _vertexArray = _gl.GenVertexArray();
        _vertexBuffer = _gl.GenBuffer();
        _indexBuffer = _gl.GenBuffer();

        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (4 * FloatsPerVertex * sizeof(float)), null, BufferUsageARB.DynamicDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);

        fixed (uint* indexPointer = s_indices)
        {
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(s_indices.Length * sizeof(uint)), indexPointer, BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)0);

        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)(2 * sizeof(float)));

        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer: 0);
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        OpenGLTexture2D? texture = _texture;
        _texture = null;

        if (texture is not null)
        {
            try
            {
                texture.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        uint indexBuffer = _indexBuffer;
        _indexBuffer = 0;

        if (indexBuffer != 0)
        {
            try
            {
                _gl.DeleteBuffer(indexBuffer);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

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
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
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

        OpenGLProgram? program = _program;
        _program = null;

        if (program is not null)
        {
            try
            {
                program.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }

    private static float ToNormalizedX(int pixelX, int viewportWidth)
    {
        return 2f * pixelX / viewportWidth - 1f;
    }

    private static float ToNormalizedY(int pixelY, int viewportHeight)
    {
        return 1f - 2f * pixelY / viewportHeight;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTextureCoordinate;
        out vec2 textureCoordinate;

        void main()
        {
            textureCoordinate = aTextureCoordinate;
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 textureCoordinate;
        out vec4 outputColor;
        uniform sampler2D uTexture;

        void main()
        {
            outputColor = texture(uTexture, textureCoordinate);
        }
        """;
}
