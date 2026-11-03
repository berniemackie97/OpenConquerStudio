using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Primitives;

internal sealed unsafe class OpenGLLineRectangleRenderer : IDisposable
{
    private const int VertexCount = 8;
    private const int FloatsPerVertex = 2;

    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;

    private OpenGLProgram? _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private int _colorUniform;
    private bool _disposed;

    public OpenGLLineRectangleRenderer(GL gl)
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
                // Preserve the resource-initialization failure.
            }

            throw;
        }
    }

    public void Draw(int targetWidth, int targetHeight, int left, int top, int right, int bottom, SpriteColor color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        if (right <= left || bottom <= top)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "A native outline requires positive rectangle extents.");
        }

        float l = ToNormalizedX(left, targetWidth);
        float r = ToNormalizedX(right, targetWidth);
        float t = ToNormalizedY(top, targetHeight);
        float b = ToNormalizedY(bottom, targetHeight);

        Span<float> vertices =
        [
            l, t, l, b,
            l, b, r, b,
            r, b, r, t,
            r, t, l, t,
        ];

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The native line program is unavailable.");
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
            _gl.LineWidth(1f);

            program.Use();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

            fixed (float* pointer = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), pointer);
            }

            _gl.Uniform4(_colorUniform,
                color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);

            _gl.DrawArrays(PrimitiveType.Lines, first: 0, count: VertexCount);
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
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(VertexCount * FloatsPerVertex * sizeof(float)), null, BufferUsageARB.DynamicDraw);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)0);
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

        uint buffer = _vertexBuffer;
        _vertexBuffer = 0;

        if (buffer != 0)
        {
            try
            {
                _gl.DeleteBuffer(buffer);
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

    private static float ToNormalizedX(int x, int width) => (float)(2.0 * x / width - 1.0);
    private static float ToNormalizedY(int y, int height) => (float)(1.0 - 2.0 * y / height);

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
