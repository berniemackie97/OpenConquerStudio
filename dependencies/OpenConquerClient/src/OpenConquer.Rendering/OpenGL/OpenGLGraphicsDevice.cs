using OpenConquer.Rendering.OpenGL.Meshes;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.OpenGL.Startup;
using OpenConquer.Rendering.OpenGL.Text;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Text.Layout;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL;

public sealed class OpenGLGraphicsDevice : IDisposable
{
    private const int MinimumMajorVersion = 3;
    private const int MinimumMinorVersion = 3;

    private readonly GL _gl;
    private bool _disposed;

    public OpenGLGraphicsDevice(OpenGLProcAddressResolver resolveProcAddress)
    {
        ArgumentNullException.ThrowIfNull(resolveProcAddress);

        GL gl = GL.GetApi(functionName => resolveProcAddress(functionName));

        try
        {
            ValidateContext(gl);

            Version = GetRequiredString(gl, StringName.Version);
            ShadingLanguageVersion = GetRequiredString(gl, StringName.ShadingLanguageVersion);
            Vendor = GetRequiredString(gl, StringName.Vendor);
            Renderer = GetRequiredString(gl, StringName.Renderer);

            gl.GetInteger(GetPName.MaxRenderbufferSize, out int maxRenderbuffer);
            gl.GetInteger(GetPName.MaxTextureSize, out int maxTexture);
            if (maxRenderbuffer <= 0 || maxTexture <= 0)
            {
                throw new NotSupportedException("OpenGL did not report valid render-target limits.");
            }

            MaximumRenderTargetDimension = Math.Min(maxRenderbuffer, maxTexture);
            MaximumTextureDimension = maxTexture;
            _gl = gl;
        }
        catch
        {
            try
            {
                gl.Dispose();
            }
            catch
            {
                // Preserve the context-validation or capability-query failure.
            }

            throw;
        }
    }

    public string Version
    {
        get;
    }

    public string ShadingLanguageVersion
    {
        get;
    }

    public string Vendor
    {
        get;
    }

    public string Renderer
    {
        get;
    }

    public int MaximumRenderTargetDimension
    {
        get;
    }

    public int MaximumTextureDimension
    {
        get;
    }

    public OpenGLRenderer CreateRenderer(LogicalRenderSize logicalRenderSize, int framebufferWidth, int framebufferHeight, PresentationPolicy presentationPolicy = PresentationPolicy.Fit,
        RenderColorFormat colorFormat = RenderColorFormat.Rgb565)
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        if (logicalRenderSize.Width <= 0 || logicalRenderSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "Logical render size must have positive width and height.");
        }

        if (logicalRenderSize.Width > MaximumRenderTargetDimension ||
            logicalRenderSize.Height > MaximumRenderTargetDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize,
                "Logical render target exceeds OpenGL framebuffer or texture limits.");
        }

        if (!Enum.IsDefined(presentationPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(presentationPolicy), presentationPolicy, "Unknown presentation policy.");
        }

        return new OpenGLRenderer(_gl, logicalRenderSize, framebufferWidth, framebufferHeight, presentationPolicy, colorFormat);
    }

    public OpenGLIndexedMesh CreateIndexedMesh(int vertexCount, ReadOnlySpan<ushort> indices)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new OpenGLIndexedMesh(_gl, vertexCount, indices);
    }

    public OpenGLTexture2D CreateTexture2D(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (width > MaximumTextureDimension || height > MaximumTextureDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions exceed this graphics device's capacity.");
        }

        return new OpenGLTexture2D(_gl, width, height, rgbaPixels);
    }

    internal OpenGLTextResource CreateTextResource(NativeTextLayoutSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);

        return new OpenGLTextResource(_gl, source);
    }

    public OpenGLStartupSurfaceRenderer CreateStartupSurfaceRenderer(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        return new OpenGLStartupSurfaceRenderer(_gl, width, height, rgbaPixels);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _gl.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private static void ValidateContext(GL gl)
    {
        gl.GetInteger(pname: GetPName.MajorVersion, out int majorVersion);
        gl.GetInteger(pname: GetPName.MinorVersion, out int minorVersion);

        if (majorVersion < MinimumMajorVersion || majorVersion == MinimumMajorVersion && minorVersion < MinimumMinorVersion)
        {
            throw new NotSupportedException($"OpenGL {MinimumMajorVersion}.{MinimumMinorVersion} or later is required. The current context provides OpenGL {majorVersion}.{minorVersion}.");
        }

        gl.GetInteger(pname: GetPName.ContextProfileMask, out int profileMask);

        if ((profileMask & (int)GLEnum.ContextCoreProfileBit) == 0)
        {
            throw new NotSupportedException($"An OpenGL {MinimumMajorVersion}.{MinimumMinorVersion} Core profile is required.");
        }
    }

    private static string GetRequiredString(GL gl, StringName name)
    {
        return gl.GetStringS(name) ?? throw new InvalidOperationException($"OpenGL did not provide a value for {name}.");
    }
}
