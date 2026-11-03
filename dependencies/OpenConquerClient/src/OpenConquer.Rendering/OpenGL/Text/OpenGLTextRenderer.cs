using System.Runtime.ExceptionServices;
using OpenConquer.Rendering.Text.Layout;
using OpenConquer.Rendering.Text.Rendering;
using Silk.NET.OpenGL;

namespace OpenConquer.Rendering.OpenGL.Text;

/// <summary>
/// Draws validated text layouts with reusable, fixed-size OpenGL buffers.
/// </summary>
internal sealed class OpenGLTextRenderer : IDisposable
{
    private const int GlyphPassStagingCapacity = 4096;
    private const int MaximumGlyphPassesPerDraw = 65536;

    private readonly GL _gl;
    private readonly NativeTextBatchPlan _batchPlan;
    private readonly OpenGLTextVertexStagingBuffer _stagingBuffer;

    private OpenGLTextPipeline? _pipeline;
    private OpenGLTextVertexBuffer? _vertexBuffer;
    private bool _disposed;

    public OpenGLTextRenderer(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);

        _gl = gl;
        _batchPlan = new NativeTextBatchPlan(MaximumGlyphPassesPerDraw);
        _stagingBuffer = new OpenGLTextVertexStagingBuffer(GlyphPassStagingCapacity);

        try
        {
            _pipeline = new OpenGLTextPipeline(gl);
            _vertexBuffer = new OpenGLTextVertexBuffer(gl, _stagingBuffer.VertexCapacity);
        }
        catch
        {
            try
            {
                DestroyResources();
            }
            catch
            {
                // Preserve the original text-renderer creation failure.
            }

            throw;
        }
    }

    public void Draw(OpenGLTextResource resource, NativeTextLayout layout, NativeTextRenderOptions options, int targetWidth, int targetHeight, int originXPixels, int originYPixels)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        resource.ValidateOwner(_gl, nameof(resource));
        resource.ValidateLayout(layout, nameof(layout));
        _batchPlan.Prepare(layout, options);

        if (_batchPlan.GlyphCount == 0)
        {
            return;
        }

        OpenGLTextPipeline pipeline = _pipeline ?? throw new InvalidOperationException("The OpenGL text pipeline is unavailable.");
        ExceptionDispatchInfo? firstFailure = null;
        bool pipelineActive = false;

        try
        {
            pipeline.Begin(targetWidth, targetHeight);
            pipelineActive = true;

            DrawPrepared(resource, layout, originXPixels, originYPixels);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        _stagingBuffer.Clear();

        if (pipelineActive)
        {
            try
            {
                pipeline.End();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
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
            _stagingBuffer.Clear();
            _disposed = true;
        }
    }

    private void DrawPrepared(OpenGLTextResource resource, NativeTextLayout layout, int originXPixels, int originYPixels)
    {
        ReadOnlySpan<NativeTextLayoutItem> items = layout.Items.Span;
        NativeTextRenderPassSequence passSequence = _batchPlan.PassSequence;

        for (int usedPageIndex = 0; usedPageIndex < _batchPlan.UsedPageCount; usedPageIndex++)
        {
            int pageIndex = _batchPlan.GetUsedPageIndex(usedPageIndex);
            ReadOnlySpan<int> glyphItemIndices = _batchPlan.GetGlyphItemIndicesForPage(pageIndex);

            resource.BindPage(pageIndex);

            foreach (int itemIndex in glyphItemIndices)
            {
                NativeTextLayoutItem item = items[itemIndex];
                NativeTextRenderPassSequence.Enumerator passEnumerator = passSequence.GetEnumerator();

                while (passEnumerator.MoveNext())
                {
                    AppendGlyphPass(item, passEnumerator.Current, originXPixels, originYPixels);
                }
            }

            FlushStagingBuffer();
        }
    }

    private void AppendGlyphPass(NativeTextLayoutItem item, NativeTextRenderPass pass, int originXPixels, int originYPixels)
    {
        if (_stagingBuffer.TryAppendGlyphPass(item, pass, originXPixels, originYPixels))
        {
            return;
        }

        FlushStagingBuffer();

        if (!_stagingBuffer.TryAppendGlyphPass(item, pass, originXPixels, originYPixels))
        {
            throw new InvalidOperationException("The OpenGL text staging buffer cannot hold one complete glyph render pass.");
        }
    }

    private void FlushStagingBuffer()
    {
        if (_stagingBuffer.VertexCount == 0)
        {
            return;
        }

        OpenGLTextVertexBuffer vertexBuffer = _vertexBuffer ?? throw new InvalidOperationException("The OpenGL text vertex buffer is unavailable.");

        vertexBuffer.Draw(_stagingBuffer.Vertices);
        _stagingBuffer.Clear();
    }

    private void DestroyResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        OpenGLTextVertexBuffer? vertexBuffer = _vertexBuffer;
        _vertexBuffer = null;

        if (vertexBuffer is not null)
        {
            try
            {
                vertexBuffer.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        OpenGLTextPipeline? pipeline = _pipeline;
        _pipeline = null;

        if (pipeline is not null)
        {
            try
            {
                pipeline.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
    }
}
