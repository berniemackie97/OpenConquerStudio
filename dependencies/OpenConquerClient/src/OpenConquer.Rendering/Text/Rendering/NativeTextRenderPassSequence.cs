using OpenConquer.Rendering.Sprites;
using OpenConquer.Rendering.Text.Layout;

namespace OpenConquer.Rendering.Text.Rendering;

/// <summary>
/// Represents the resolved, allocation-free draw-pass sequence for one native text render operation.
/// </summary>
internal readonly struct NativeTextRenderPassSequence
{
    private readonly NativeTextRenderStyle _style;
    private readonly SpriteColor _textColor;
    private readonly SpriteColor _cornerColor;
    private readonly int _cornerOffsetXPixels;
    private readonly int _cornerOffsetYPixels;
    private readonly NativeTextVertexColors _perCornerColors;

    public NativeTextRenderPassSequence(NativeTextRenderOptions options, NativeTextFontRecord font)
    {
        ArgumentNullException.ThrowIfNull(font);

        _style = options.Style;
        _textColor = font.AntialiasEnabled ? options.TextColor : ForceOpaque(options.TextColor);
        _cornerColor = font.AntialiasEnabled ? options.CornerColor : ForceOpaque(options.CornerColor);
        _cornerOffsetXPixels = options.CornerOffsetXPixels;
        _cornerOffsetYPixels = options.CornerOffsetYPixels;
        _perCornerColors = options.PerCornerColors;
        Count = GetPassCount(options);
    }

    public int Count
    {
        get;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    private static int GetPassCount(NativeTextRenderOptions options)
    {
        return options.Style switch
        {
            NativeTextRenderStyle.Normal => 1,
            NativeTextRenderStyle.ShadowOffset => 2,
            NativeTextRenderStyle.MultiOffsetOutline => 9,
            NativeTextRenderStyle.OffsetTrail => GetTrailPassCount(options.CornerOffsetXPixels, options.CornerOffsetYPixels),
            NativeTextRenderStyle.PerCornerColor => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Style, "Unknown native text render style."),
        };
    }

    private static int GetTrailPassCount(int offsetXPixels, int offsetYPixels)
    {
        long horizontalPassCount = Math.Abs((long)offsetXPixels);
        long verticalPassCount = Math.Abs((long)offsetYPixels);
        return checked((int)(horizontalPassCount + verticalPassCount + 1));
    }

    private static SpriteColor ForceOpaque(SpriteColor color)
    {
        return new SpriteColor(color.Red, color.Green, color.Blue, byte.MaxValue);
    }

    internal struct Enumerator
    {
        private readonly NativeTextRenderPassSequence _sequence;
        private readonly int _trailStepX;
        private readonly int _trailStepY;

        private int _index;
        private int _trailRemainingX;
        private int _trailRemainingY;
        private int _trailCurrentX;
        private int _trailCurrentY;
        private bool _trailStepAlongX;

        public Enumerator(NativeTextRenderPassSequence sequence)
        {
            _sequence = sequence;
            _index = -1;
            Current = default;

            if (sequence._style == NativeTextRenderStyle.OffsetTrail)
            {
                _trailRemainingX = Math.Abs(sequence._cornerOffsetXPixels);
                _trailRemainingY = Math.Abs(sequence._cornerOffsetYPixels);
                _trailStepX = sequence._cornerOffsetXPixels < 0 ? -1 : 1;
                _trailStepY = sequence._cornerOffsetYPixels < 0 ? -1 : 1;
                _trailCurrentX = sequence._cornerOffsetXPixels;
                _trailCurrentY = sequence._cornerOffsetYPixels;
                _trailStepAlongX = _trailRemainingX >= _trailRemainingY;
            }
            else
            {
                _trailRemainingX = 0;
                _trailRemainingY = 0;
                _trailStepX = 0;
                _trailStepY = 0;
                _trailCurrentX = 0;
                _trailCurrentY = 0;
                _trailStepAlongX = false;
            }
        }

        public NativeTextRenderPass Current
        {
            get; private set;
        }

        public bool MoveNext()
        {
            int nextIndex = _index + 1;

            if (nextIndex >= _sequence.Count)
            {
                return false;
            }

            _index = nextIndex;
            Current = _sequence._style switch
            {
                NativeTextRenderStyle.Normal => CreateSolidPass(0, 0, _sequence._textColor),
                NativeTextRenderStyle.ShadowOffset => CreateShadowPass(nextIndex),
                NativeTextRenderStyle.MultiOffsetOutline => CreateOutlinePass(nextIndex),
                NativeTextRenderStyle.OffsetTrail => CreateTrailPass(nextIndex),
                NativeTextRenderStyle.PerCornerColor => new NativeTextRenderPass(0, 0, _sequence._perCornerColors),
                _ => throw new InvalidOperationException($"Unsupported native text render style {(int)_sequence._style}."),
            };

            return true;
        }

        private NativeTextRenderPass CreateShadowPass(int index)
        {
            return index == 0
                ? CreateSolidPass(_sequence._cornerOffsetXPixels, _sequence._cornerOffsetYPixels, _sequence._cornerColor)
                : CreateSolidPass(0, 0, _sequence._textColor);
        }

        private NativeTextRenderPass CreateOutlinePass(int index)
        {
            if (index == 8)
            {
                return CreateSolidPass(0, 0, _sequence._textColor);
            }

            (int offsetX, int offsetY) = index switch
            {
                0 => (-1, 0),
                1 => (1, 0),
                2 => (0, -1),
                3 => (0, 1),
                4 => (-1, -1),
                5 => (1, -1),
                6 => (-1, 1),
                7 => (1, 1),
                _ => throw new InvalidOperationException($"Invalid native outline pass index {index}."),
            };

            return CreateSolidPass(offsetX, offsetY, _sequence._cornerColor);
        }

        private NativeTextRenderPass CreateTrailPass(int index)
        {
            if (index == _sequence.Count - 1)
            {
                return CreateSolidPass(0, 0, _sequence._textColor);
            }

            NativeTextRenderPass pass = CreateSolidPass(_trailCurrentX, _trailCurrentY, _sequence._cornerColor);
            AdvanceTrail();
            return pass;
        }

        private void AdvanceTrail()
        {
            if (_trailStepAlongX)
            {
                _trailCurrentX = checked(_trailCurrentX - _trailStepX);
                _trailRemainingX--;
                _trailStepAlongX = _trailRemainingY == 0;
            }
            else
            {
                _trailCurrentY = checked(_trailCurrentY - _trailStepY);
                _trailRemainingY--;
                _trailStepAlongX = _trailRemainingX > 0;
            }
        }

        private static NativeTextRenderPass CreateSolidPass(int offsetXPixels, int offsetYPixels, SpriteColor color)
        {
            return new NativeTextRenderPass(offsetXPixels, offsetYPixels, NativeTextVertexColors.Solid(color));
        }
    }
}
