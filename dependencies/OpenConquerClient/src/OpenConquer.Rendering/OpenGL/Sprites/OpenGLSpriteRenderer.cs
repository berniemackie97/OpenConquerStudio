using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Sprites;

internal sealed unsafe class OpenGLSpriteRenderer : IDisposable
{
    private const int FloatsPerVertex = 4;

    private static readonly uint[] s_indices = [0, 1, 2, 0, 2, 3];
    private static readonly float s_degreesToRadians = BitConverter.Int32BitsToSingle(0x3C8EFA35);

    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;

    private OpenGLProgram? _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private uint _nearestRepeatSampler;
    private int _textureUniform;
    private int _colorUniform;
    private bool _disposed;

    public OpenGLSpriteRenderer(GL gl)
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
                // Preserve the original sprite-pipeline creation failure.
            }

            throw;
        }
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, int x, int y)
    {
        Draw(texture, targetWidth, targetHeight, x, y, SpriteColor.White);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, int x, int y, SpriteColor color)
    {
        Draw(texture, targetWidth, targetHeight, x, y, color, SpriteBlendMode.Alpha);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, int x, int y, SpriteColor color, SpriteBlendMode blendMode)
    {
        ValidateCommonDrawArguments(texture, targetWidth, targetHeight);

        DrawCore(texture, targetWidth, targetHeight, 0, 0, texture.Width, texture.Height,
            x, y, texture.Width, texture.Height, color, blendMode, rotationDegrees: 0, sampler: 0);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height)
    {
        Draw(texture, targetWidth, targetHeight, sourceRectangle, x, y, width, height, SpriteColor.White);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color)
    {
        Draw(texture, targetWidth, targetHeight, sourceRectangle, x, y, width, height, color, rotationDegrees: 0);
    }

    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle, int x, int y, int width, int height, SpriteColor color, int rotationDegrees)
    {
        ValidateCommonDrawArguments(texture, targetWidth, targetHeight);
        ValidateSourceRectangle(texture, sourceRectangle);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        DrawCore(texture, targetWidth, targetHeight, sourceRectangle.X, sourceRectangle.Y, sourceRectangle.Right, sourceRectangle.Bottom,
            x, y, width, height, color, SpriteBlendMode.Alpha, rotationDegrees, sampler: 0);
    }

    public void DrawRepeated(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceBounds sourceBounds, int x, int y, int width, int height)
    {
        ValidateCommonDrawArguments(texture, targetWidth, targetHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        DrawCore(texture, targetWidth, targetHeight, sourceBounds.Left, sourceBounds.Top, sourceBounds.Right, sourceBounds.Bottom,
            x, y, width, height, SpriteColor.White, SpriteBlendMode.Alpha, rotationDegrees: 0, _nearestRepeatSampler);
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

    private void DrawCore(OpenGLTexture2D texture, int targetWidth, int targetHeight, long sourceLeftPixel, long sourceTopPixel, long sourceRightPixel, long sourceBottomPixel,
        int x, int y, int width, int height, SpriteColor color, SpriteBlendMode blendMode, int rotationDegrees, uint sampler)
    {
        (BlendingFactor sourceBlend, BlendingFactor destinationBlend) = GetBlendFactors(blendMode);

        long rightPixel = (long)x + width;
        long bottomPixel = (long)y + height;

        float left = ToNormalizedX(x, targetWidth);
        float right = ToNormalizedX(rightPixel, targetWidth);
        float top = ToNormalizedY(y, targetHeight);
        float bottom = ToNormalizedY(bottomPixel, targetHeight);

        Span<float> positions = [left, top, right, top, right, bottom, left, bottom];

        if (rotationDegrees != 0)
        {
            WriteRotatedNormalizedPositions(positions, targetWidth, targetHeight, x, y, width, height, rotationDegrees);
        }

        float sourceLeft = ToNormalizedTextureCoordinate(sourceLeftPixel, texture.Width);
        float sourceRight = ToNormalizedTextureCoordinate(sourceRightPixel, texture.Width);
        float sourceTop = ToNormalizedTextureCoordinate(sourceTopPixel, texture.Height);
        float sourceBottom = ToNormalizedTextureCoordinate(sourceBottomPixel, texture.Height);

        Span<float> vertices =
        [
            positions[0], positions[1], sourceLeft, sourceTop,
            positions[2], positions[3], sourceRight, sourceTop,
            positions[4], positions[5], sourceRight, sourceBottom,
            positions[6], positions[7], sourceLeft, sourceBottom,
        ];

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The OpenGL sprite program is unavailable.");
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            _gl.BlendFunc(sourceBlend, destinationBlend);

            program.Use();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

            fixed (float* vertexPointer = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
            }

            _gl.ActiveTexture(TextureUnit.Texture0);
            texture.Bind();
            _gl.BindSampler(0, sampler);
            _gl.Uniform1(_textureUniform, 0);
            _gl.Uniform4(_colorUniform, ToNormalizedColorChannel(color.Red), ToNormalizedColorChannel(color.Green), ToNormalizedColorChannel(color.Blue), ToNormalizedColorChannel(color.Alpha));
            _gl.DrawElements(PrimitiveType.Triangles, (uint)s_indices.Length, DrawElementsType.UnsignedInt, null);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterDraw(firstFailure, usesTexture: true);
    }

    private void ValidateCommonDrawArguments(OpenGLTexture2D texture, int targetWidth, int targetHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        texture.ValidateOwner(_gl, nameof(texture));
    }

    private static void ValidateSourceRectangle(OpenGLTexture2D texture, SpriteSourceRectangle sourceRectangle)
    {
        if (sourceRectangle.X < 0 || sourceRectangle.Y < 0 || sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, "The sprite source rectangle must have non-negative coordinates and positive dimensions.");
        }

        if (sourceRectangle.Right > texture.Width || sourceRectangle.Bottom > texture.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle, $"The sprite source rectangle must fit within the {texture.Width}x{texture.Height} texture.");
        }
    }

    private static (BlendingFactor Source, BlendingFactor Destination) GetBlendFactors(SpriteBlendMode blendMode)
    {
        return blendMode switch
        {
            SpriteBlendMode.Alpha => (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha),
            SpriteBlendMode.Additive => (BlendingFactor.One, BlendingFactor.One),
            _ => throw new ArgumentOutOfRangeException(nameof(blendMode), blendMode, "Unknown sprite blend mode."),
        };
    }

    private static void WriteRotatedNormalizedPositions(Span<float> positions, int targetWidth, int targetHeight, int x, int y, int width, int height, int rotationDegrees)
    {
        float left = x;
        float top = y;
        float right = ((long)x + width);
        float bottom = ((long)y + height);

        float pivotX = (right - left) / 2f + left;
        float pivotY = (bottom - top) / 2f + top;

        int reducedDegrees = rotationDegrees % 360;
        float radians = reducedDegrees * s_degreesToRadians;
        (float sine, float cosine) = MathF.SinCos(radians);

        RotatePoint(left, top, pivotX, pivotY, sine, cosine, out positions[0], out positions[1]);
        RotatePoint(right, top, pivotX, pivotY, sine, cosine, out positions[2], out positions[3]);
        RotatePoint(right, bottom, pivotX, pivotY, sine, cosine, out positions[4], out positions[5]);
        RotatePoint(left, bottom, pivotX, pivotY, sine, cosine, out positions[6], out positions[7]);

        for (int offset = 0; offset < positions.Length; offset += 2)
        {
            positions[offset] = ToNormalizedX(positions[offset], targetWidth);
            positions[offset + 1] = ToNormalizedY(positions[offset + 1], targetHeight);
        }
    }

    private static void RotatePoint(float x, float y, float pivotX, float pivotY, float sine, float cosine, out float rotatedX, out float rotatedY)
    {
        float deltaX = x - pivotX;
        float deltaY = y - pivotY;

        rotatedX = deltaX * cosine - deltaY * sine + pivotX;
        rotatedY = deltaX * sine + deltaY * cosine + pivotY;
    }

    private void CreateResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _program = new OpenGLProgram(_gl, VertexShaderSource, FragmentShaderSource);
            _textureUniform = _program.GetRequiredUniformLocation("uTexture");
            _colorUniform = _program.GetRequiredUniformLocation("uColor");

            _vertexArray = _gl.GenVertexArray();
            _vertexBuffer = _gl.GenBuffer();
            _indexBuffer = _gl.GenBuffer();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(4 * FloatsPerVertex * sizeof(float)), null, BufferUsageARB.DynamicDraw);

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer);

            fixed (uint* indexPointer = s_indices)
            {
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(s_indices.Length * sizeof(uint)), indexPointer, BufferUsageARB.StaticDraw);
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)0);

            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, FloatsPerVertex * sizeof(float), (void*)(2 * sizeof(float)));

            _nearestRepeatSampler = _gl.GenSampler();
            _gl.SamplerParameter(_nearestRepeatSampler, SamplerParameterI.MinFilter, (int)GLEnum.Nearest);
            _gl.SamplerParameter(_nearestRepeatSampler, SamplerParameterI.MagFilter, (int)GLEnum.Nearest);
            _gl.SamplerParameter(_nearestRepeatSampler, SamplerParameterI.WrapS, (int)GLEnum.Repeat);
            _gl.SamplerParameter(_nearestRepeatSampler, SamplerParameterI.WrapT, (int)GLEnum.Repeat);
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

        uint nearestRepeatSampler = _nearestRepeatSampler;
        _nearestRepeatSampler = 0;

        if (nearestRepeatSampler != 0)
        {
            try
            {
                _gl.DeleteSampler(nearestRepeatSampler);
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

    private static float ToNormalizedX(long pixelX, int targetWidth)
    {
        return (float)(2.0 * pixelX / targetWidth - 1.0);
    }

    private static float ToNormalizedX(float pixelX, int targetWidth)
    {
        return (float)(2.0 * pixelX / targetWidth - 1.0);
    }

    private static float ToNormalizedY(long pixelY, int targetHeight)
    {
        return (float)(1.0 - 2.0 * pixelY / targetHeight);
    }

    private static float ToNormalizedY(float pixelY, int targetHeight)
    {
        return (float)(1.0 - 2.0 * pixelY / targetHeight);
    }

    private static float ToNormalizedTextureCoordinate(long pixel, int textureExtent)
    {
        return (float)((double)pixel / textureExtent);
    }

    private static float ToNormalizedColorChannel(byte channel)
    {
        return channel / (float)byte.MaxValue;
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
        uniform vec4 uColor;

        void main()
        {
            outputColor = texture(uTexture, textureCoordinate) * uColor;
        }
        """;
}
