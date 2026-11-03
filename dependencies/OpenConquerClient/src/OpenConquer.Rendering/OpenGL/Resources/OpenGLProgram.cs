using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Resources;

internal sealed class OpenGLProgram : IDisposable
{
    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    public OpenGLProgram(GL gl, string vertexSource, string fragmentSource)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentException.ThrowIfNullOrWhiteSpace(vertexSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(fragmentSource);

        _gl = gl;
        _program = CreateProgram(vertexSource, fragmentSource);
    }

    public void Use()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _gl.UseProgram(_program);
    }

    public int GetRequiredUniformLocation(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(name);

        int location = _gl.GetUniformLocation(_program, name);

        if (location < 0)
        {
            throw new InvalidOperationException($"The OpenGL program does not expose required uniform '{name}'.");
        }

        return location;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            uint program = _program;
            _program = 0;

            if (program != 0)
            {
                _gl.DeleteProgram(program);
            }
        }
        finally
        {
            _disposed = true;
        }
    }

    private uint CreateProgram(string vertexSource, string fragmentSource)
    {
        uint vertexShader = 0;
        uint fragmentShader = 0;
        uint program = 0;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            program = _gl.CreateProgram();
            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);
            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);

            if (status == 0)
            {
                throw new InvalidOperationException($"Failed to link the OpenGL program: {_gl.GetProgramInfoLog(program)}");
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        if (vertexShader != 0)
        {
            try
            {
                _gl.DeleteShader(vertexShader);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        if (fragmentShader != 0)
        {
            try
            {
                _gl.DeleteShader(fragmentShader);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        if (firstFailure is not null)
        {
            if (program != 0)
            {
                try
                {
                    _gl.DeleteProgram(program);
                }
                catch
                {
                    // Preserve the earlier compile, link, or shader-cleanup failure.
                }
            }

            firstFailure.Throw();
        }

        return program;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);

            if (status == 0)
            {
                throw new InvalidOperationException($"Failed to compile the OpenGL {type} shader: {_gl.GetShaderInfoLog(shader)}");
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        if (firstFailure is not null)
        {
            try
            {
                _gl.DeleteShader(shader);
            }
            catch
            {
                // Preserve the shader-creation or compilation failure.
            }

            firstFailure.Throw();
        }

        return shader;
    }
}
