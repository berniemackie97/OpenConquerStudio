using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Primitives;

internal sealed unsafe class OpenGLSolidRectangleRenderer : IDisposable
{
    private const int FloatsPerVertex = 2;

    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;

    private OpenGLProgram? _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private int _colorUniform;
    private bool _disposed;

    public OpenGLSolidRectangleRenderer(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _cleanup = new OpenGLDrawStateCleanup(gl);

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
                // Preserve the original solid-rectangle pipeline creation failure.
            }

            throw;
        }
    }

    public void Draw(int targetWidth, int targetHeight, int x, int y, int width, int height, SpriteColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        long rightPixel = (long)x + width;
        long bottomPixel = (long)y + height;

        Span<float> vertices =
        [
            ToNormalizedX(x, targetWidth), ToNormalizedY(y, targetHeight),
            ToNormalizedX(x, targetWidth), ToNormalizedY(bottomPixel, targetHeight),
            ToNormalizedX(rightPixel, targetWidth), ToNormalizedY(y, targetHeight),
            ToNormalizedX(rightPixel, targetWidth), ToNormalizedY(bottomPixel, targetHeight),
        ];

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The solid-rectangle program is unavailable.");
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            program.Use();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

            fixed (float* vertexPointer = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
            }

            _gl.Uniform4(_colorUniform, ToNormalizedColorChannel(color.Red), ToNormalizedColorChannel(color.Green), ToNormalizedColorChannel(color.Blue), ToNormalizedColorChannel(color.Alpha));
            _gl.DrawArrays(PrimitiveType.TriangleStrip, first: 0, count: 4);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterDraw(firstFailure, usesTexture: false);
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
            _program = new OpenGLProgram(_gl, VertexShaderSource, FragmentShaderSource);
            _colorUniform = _program.GetRequiredUniformLocation("uColor");

            _vertexArray = _gl.GenVertexArray();
            _vertexBuffer = _gl.GenBuffer();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, size: (4 * FloatsPerVertex * sizeof(float)), data: null, BufferUsageARB.DynamicDraw);

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, normalized: false, FloatsPerVertex * sizeof(float), (void*)0);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterInitialization(firstFailure);
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

    private static float ToNormalizedX(long pixelX, int targetWidth)
    {
        return (float)(2.0 * pixelX / targetWidth - 1.0);
    }

    private static float ToNormalizedY(long pixelY, int targetHeight)
    {
        return (float)(1.0 - 2.0 * pixelY / targetHeight);
    }

    private static float ToNormalizedColorChannel(byte channel)
    {
        return channel / (float)byte.MaxValue;
    }

    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;

        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        out vec4 outputColor;
        uniform vec4 uColor;

        void main()
        {
            outputColor = uColor;
        }
        """;
}
