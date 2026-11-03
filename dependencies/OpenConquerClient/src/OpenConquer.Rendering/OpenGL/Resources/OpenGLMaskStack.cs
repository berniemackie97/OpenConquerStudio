using System.Runtime.ExceptionServices;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Resources;

/// <summary>
/// Nested masks for one renderer. Each level's coverage is the intersection of its own shapes with the level
/// beneath, so content drawn at a level is limited by every enclosing mask. Levels are created on first use.
/// </summary>
internal sealed class OpenGLMaskStack : IDisposable
{
    internal const int MaximumDepth = 8;

    private readonly GL _gl;
    private readonly int _width;
    private readonly int _height;
    private readonly List<OpenGLMaskTarget> _levels = [];

    private int _depth;
    private bool _writing;
    private bool _disposed;

    public OpenGLMaskStack(GL gl, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gl);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        _gl = gl;
        _width = width;
        _height = height;
    }

    public int Depth => _depth;

    /// <summary>True between <see cref="Begin"/> and <see cref="BeginContent"/>: draws write coverage.</summary>
    public bool IsWritingCoverage => _writing;

    /// <summary>The coverage texture the next draw is limited by, or 0 for none.</summary>
    public uint LimitingTexture
    {
        get
        {
            int level = _writing ? _depth - 1 : _depth;
            return level == 0 ? 0 : _levels[level - 1].Texture;
        }
    }

    public void Begin()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_writing)
        {
            throw new InvalidOperationException("A mask's shapes are still being drawn.");
        }

        if (_depth == MaximumDepth)
        {
            throw new InvalidOperationException($"Masks cannot nest deeper than {MaximumDepth} levels.");
        }

        if (_levels.Count == _depth)
        {
            _levels.Add(new OpenGLMaskTarget(_gl, _width, _height));
        }

        _levels[_depth].BeginCoverage();
        _depth++;
        _writing = true;
    }

    public void BeginContent(OpenGLRenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_writing)
        {
            throw new InvalidOperationException("No mask is collecting shapes.");
        }

        target.BindForDrawing();
        _writing = false;
    }

    public void End(OpenGLRenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_depth == 0)
        {
            throw new InvalidOperationException("No mask is active.");
        }

        if (_writing)
        {
            target.BindForDrawing();
            _writing = false;
        }

        _depth--;
    }

    /// <summary>Drops every level, for a frame that ends with masks still open.</summary>
    public void Reset(OpenGLRenderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_writing)
        {
            target.BindForDrawing();
        }

        _writing = false;
        _depth = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        foreach (OpenGLMaskTarget level in _levels)
        {
            try
            {
                level.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        _levels.Clear();
        _disposed = true;
        firstFailure?.Throw();
    }
}
