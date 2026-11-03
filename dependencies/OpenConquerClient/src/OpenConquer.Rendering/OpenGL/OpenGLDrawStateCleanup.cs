using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

internal interface IOpenGLDrawStateOperations
{
    void ActivateTextureZero();
    void UnbindSamplerZero();
    void UnbindTexture2D();
    void UnbindVertexArray();
    void UnbindArrayBuffer();
    void UnbindProgram();
    void DisableBlending();
    void EnableDepthWrites();
}

internal sealed class OpenGLDrawStateCleanup
{
    private readonly IOpenGLDrawStateOperations _operations;

    public OpenGLDrawStateCleanup(GL gl) : this(new OpenGLDrawStateOperations(gl))
    {
    }

    internal OpenGLDrawStateCleanup(IOpenGLDrawStateOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        _operations = operations;
    }

    public void RestoreAfterInitialization(ExceptionDispatchInfo? firstFailure)
    {
        try
        {
            _operations.UnbindVertexArray();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _operations.UnbindArrayBuffer();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    public void RestoreAfterDraw(ExceptionDispatchInfo? firstFailure, bool usesTexture)
    {
        if (usesTexture)
        {
            bool textureZeroActive = false;

            try
            {
                _operations.ActivateTextureZero();
                textureZeroActive = true;
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                _operations.UnbindSamplerZero();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }

            if (textureZeroActive)
            {
                try
                {
                    _operations.UnbindTexture2D();
                }
                catch (Exception exception)
                {
                    firstFailure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }

        try
        {
            _operations.UnbindVertexArray();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _operations.UnbindArrayBuffer();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _operations.UnbindProgram();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _operations.DisableBlending();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _operations.EnableDepthWrites();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private sealed class OpenGLDrawStateOperations(GL gl) : IOpenGLDrawStateOperations
    {
        public void ActivateTextureZero() => gl.ActiveTexture(TextureUnit.Texture0);
        public void UnbindSamplerZero() => gl.BindSampler(0, 0);
        public void UnbindTexture2D() => gl.BindTexture(TextureTarget.Texture2D, texture: 0);
        public void UnbindVertexArray() => gl.BindVertexArray(0);
        public void UnbindArrayBuffer() => gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer: 0);
        public void UnbindProgram() => gl.UseProgram(0);
        public void DisableBlending() => gl.Disable(EnableCap.Blend);
        public void EnableDepthWrites() => gl.DepthMask(true);
    }
}
