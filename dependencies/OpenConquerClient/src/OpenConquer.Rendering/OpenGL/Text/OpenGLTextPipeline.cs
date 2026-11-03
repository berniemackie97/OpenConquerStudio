using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.OpenGL.Resources;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// The text shader program and the fixed OpenGL render state it expects.
/// </summary>
internal sealed class OpenGLTextPipeline : IDisposable
{
    private readonly GL _gl;

    private OpenGLProgram? _program;
    private readonly int _targetSizeUniform;
    private readonly int _textureUniform;
    private bool _active;
    private bool _disposed;

    public OpenGLTextPipeline(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;

        try
        {
            _program = new OpenGLProgram(gl, VertexShaderSource, FragmentShaderSource);
            _targetSizeUniform = _program.GetRequiredUniformLocation("uTargetSize");
            _textureUniform = _program.GetRequiredUniformLocation("uTexture");
        }
        catch
        {
            try
            {
                _program?.Dispose();
            }
            catch
            {
                // Preserve the original text-pipeline creation failure.
            }

            _program = null;
            throw;
        }
    }

    public void Begin(int targetWidth, int targetHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        if (_active)
        {
            throw new InvalidOperationException("The OpenGL text pipeline is already active.");
        }

        OpenGLProgram program = _program ?? throw new InvalidOperationException("The OpenGL text program is unavailable.");
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.Disable(EnableCap.DepthTest);
            _gl.DepthMask(false);
            _gl.Disable(EnableCap.CullFace);
            _gl.Disable(EnableCap.Dither);
            _gl.Disable(EnableCap.FramebufferSrgb);
            _gl.ColorMask(red: true, green: true, blue: true, alpha: true);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.ActiveTexture(TextureUnit.Texture0);

            program.Use();
            _gl.Uniform2(_targetSizeUniform, targetWidth, (float)targetHeight);
            _gl.Uniform1(_textureUniform, 0);

            _active = true;
            return;
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            RestoreBaseline();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure.Throw();
    }

    public void End()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_active)
        {
            throw new InvalidOperationException("The OpenGL text pipeline is not active.");
        }

        try
        {
            RestoreBaseline();
        }
        finally
        {
            _active = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        if (_active)
        {
            try
            {
                RestoreBaseline();
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                _active = false;
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

        _disposed = true;
        firstFailure?.Throw();
    }

    private void RestoreBaseline()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.UseProgram(0);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.Disable(EnableCap.Blend);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _gl.DepthMask(true);
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private const string VertexShaderSource = """
        #version 330 core

        layout(location = 0) in vec2 aPosition;
        layout(location = 1) in vec2 aTextureCoordinate;
        layout(location = 2) in vec4 aColor;

        uniform vec2 uTargetSize;

        out vec2 vTextureCoordinate;
        out vec4 vColor;

        void main()
        {
            vec2 normalizedPosition = vec2(
                (aPosition.x / uTargetSize.x) * 2.0 - 1.0,
                1.0 - (aPosition.y / uTargetSize.y) * 2.0
            );

            gl_Position = vec4(normalizedPosition, 0.0, 1.0);
            vTextureCoordinate = aTextureCoordinate;
            vColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core

        in vec2 vTextureCoordinate;
        in vec4 vColor;

        uniform sampler2D uTexture;

        out vec4 oColor;

        void main()
        {
            float coverage = texture(uTexture, vTextureCoordinate).r;
            oColor = vec4(vColor.rgb, vColor.a * coverage);
        }
        """;
}
