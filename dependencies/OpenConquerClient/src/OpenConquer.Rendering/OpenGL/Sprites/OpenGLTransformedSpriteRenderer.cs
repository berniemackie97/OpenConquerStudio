using System.Numerics;
using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Sprites;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Sprites;

/// <summary>
/// Draws a premultiplied-alpha texture region through an arbitrary 2D affine transform with bilinear sampling and a
/// <see cref="SpriteColorTransform"/>, optionally limited by a mask coverage texture, or writes mask coverage.
/// </summary>
internal sealed unsafe class OpenGLTransformedSpriteRenderer : IDisposable
{
    private const int FloatsPerVertex = 4;
    private const int MaskTextureUnit = 1;

    private static readonly uint[] s_indices = [0, 1, 2, 0, 2, 3];

    private readonly GL _gl;
    private readonly OpenGLDrawStateCleanup _cleanup;

    private OpenGLProgram? _program;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _indexBuffer;
    private uint _linearClampSampler;
    private int _textureUniform;
    private int _maskUniform;
    private int _useMaskUniform;
    private int _coverageUniform;
    private int _multiplierUniform;
    private int _offsetUniform;
    private bool _disposed;

    public OpenGLTransformedSpriteRenderer(GL gl)
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
                // Preserve the original transformed-sprite pipeline creation failure.
            }

            throw;
        }
    }

    /// <summary>
    /// Draws <paramref name="sourceRectangle"/> of <paramref name="texture"/>. <paramref name="transform"/> maps
    /// source-local pixel coordinates (0,0 at the rectangle's top-left) to top-left target pixels. A nonzero
    /// <paramref name="mask"/> coverage texture limits the draw. With <paramref name="writeCoverage"/> the draw writes
    /// coverage instead of color: 1 where the texture alpha is at least one half and the mask (if any) covers the pixel.
    /// </summary>
    public void Draw(OpenGLTexture2D texture, int targetWidth, int targetHeight, SpriteSourceRectangle sourceRectangle,
        Matrix3x2 transform, SpriteColorTransform colorTransform, uint mask, bool writeCoverage)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        texture.ValidateOwner(_gl, nameof(texture));

        if (sourceRectangle.Right > texture.Width || sourceRectangle.Bottom > texture.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRectangle), sourceRectangle,
                $"The sprite source rectangle must fit within the {texture.Width}x{texture.Height} texture.");
        }

        if (!float.IsFinite(transform.M11) || !float.IsFinite(transform.M12) || !float.IsFinite(transform.M21) ||
            !float.IsFinite(transform.M22) || !float.IsFinite(transform.M31) || !float.IsFinite(transform.M32))
        {
            throw new ArgumentOutOfRangeException(nameof(transform), "The sprite transform must be finite.");
        }

        Span<float> vertices = stackalloc float[4 * FloatsPerVertex];
        WriteVertex(vertices[..4], transform, 0, 0, targetWidth, targetHeight, sourceRectangle.X, sourceRectangle.Y, texture);
        WriteVertex(vertices[4..8], transform, sourceRectangle.Width, 0, targetWidth, targetHeight, sourceRectangle.Right, sourceRectangle.Y, texture);
        WriteVertex(vertices[8..12], transform, sourceRectangle.Width, sourceRectangle.Height, targetWidth, targetHeight, sourceRectangle.Right, sourceRectangle.Bottom, texture);
        WriteVertex(vertices[12..], transform, 0, sourceRectangle.Height, targetWidth, targetHeight, sourceRectangle.X, sourceRectangle.Bottom, texture);

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The OpenGL transformed-sprite program is unavailable.");
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
            _gl.Enable(EnableCap.Blend);

            if (writeCoverage)
            {
                _gl.BlendEquation(BlendEquationModeEXT.Max);
                _gl.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            }
            else
            {
                _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            }

            program.Use();

            _gl.BindVertexArray(_vertexArray);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);

            fixed (float* vertexPointer = vertices)
            {
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, offset: 0, (nuint)(vertices.Length * sizeof(float)), vertexPointer);
            }

            _gl.ActiveTexture(TextureUnit.Texture0 + MaskTextureUnit);
            _gl.BindTexture(TextureTarget.Texture2D, mask);
            _gl.ActiveTexture(TextureUnit.Texture0);
            texture.Bind();
            _gl.BindSampler(0, _linearClampSampler);
            _gl.Uniform1(_textureUniform, 0);
            _gl.Uniform1(_maskUniform, MaskTextureUnit);
            _gl.Uniform1(_useMaskUniform, mask != 0 ? 1 : 0);
            _gl.Uniform1(_coverageUniform, writeCoverage ? 1 : 0);
            _gl.Uniform4(_multiplierUniform, colorTransform.RedMultiplier, colorTransform.GreenMultiplier, colorTransform.BlueMultiplier, colorTransform.AlphaMultiplier);
            _gl.Uniform4(_offsetUniform, colorTransform.RedOffset, colorTransform.GreenOffset, colorTransform.BlueOffset, colorTransform.AlphaOffset);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)s_indices.Length, DrawElementsType.UnsignedInt, null);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            _gl.ActiveTexture(TextureUnit.Texture0 + MaskTextureUnit);
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        _cleanup.RestoreAfterDraw(firstFailure, usesTexture: true);
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

    private static void WriteVertex(Span<float> vertex, Matrix3x2 transform, float localX, float localY, int targetWidth, int targetHeight,
        long textureX, long textureY, OpenGLTexture2D texture)
    {
        Vector2 target = Vector2.Transform(new Vector2(localX, localY), transform);
        vertex[0] = (float)(2.0 * target.X / targetWidth - 1.0);
        vertex[1] = (float)(1.0 - 2.0 * target.Y / targetHeight);
        vertex[2] = (float)((double)textureX / texture.Width);
        vertex[3] = (float)((double)textureY / texture.Height);
    }

    private void CreateResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _program = new OpenGLProgram(_gl, VertexShaderSource, FragmentShaderSource);
            _textureUniform = _program.GetRequiredUniformLocation("uTexture");
            _maskUniform = _program.GetRequiredUniformLocation("uMask");
            _useMaskUniform = _program.GetRequiredUniformLocation("uUseMask");
            _coverageUniform = _program.GetRequiredUniformLocation("uCoverage");
            _multiplierUniform = _program.GetRequiredUniformLocation("uMultiplier");
            _offsetUniform = _program.GetRequiredUniformLocation("uOffset");

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

            _linearClampSampler = _gl.GenSampler();
            _gl.SamplerParameter(_linearClampSampler, SamplerParameterI.MinFilter, (int)GLEnum.Linear);
            _gl.SamplerParameter(_linearClampSampler, SamplerParameterI.MagFilter, (int)GLEnum.Linear);
            _gl.SamplerParameter(_linearClampSampler, SamplerParameterI.WrapS, (int)GLEnum.ClampToEdge);
            _gl.SamplerParameter(_linearClampSampler, SamplerParameterI.WrapT, (int)GLEnum.ClampToEdge);
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

        uint sampler = _linearClampSampler;
        _linearClampSampler = 0;

        if (sampler != 0)
        {
            try
            {
                _gl.DeleteSampler(sampler);
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

    // Textures are premultiplied. The color transform applies to straight color, as Flash defines it.
    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 textureCoordinate;
        out vec4 outputColor;
        uniform sampler2D uTexture;
        uniform sampler2D uMask;
        uniform int uUseMask;
        uniform int uCoverage;
        uniform vec4 uMultiplier;
        uniform vec4 uOffset;

        void main()
        {
            vec4 texel = texture(uTexture, textureCoordinate);
            float mask = uUseMask != 0 ? texelFetch(uMask, ivec2(gl_FragCoord.xy), 0).r : 1.0;

            if (uCoverage != 0)
            {
                outputColor = vec4(texel.a >= 0.5 && mask >= 0.5 ? 1.0 : 0.0);
                return;
            }

            if (texel.a <= 0.0 || mask < 0.5)
            {
                discard;
            }

            vec4 color = clamp(vec4(texel.rgb / texel.a, texel.a) * uMultiplier + uOffset, 0.0, 1.0);
            outputColor = vec4(color.rgb * color.a, color.a);
        }
        """;
}
