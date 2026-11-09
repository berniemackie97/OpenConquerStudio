using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using OpenConquerStudio.Authoring;

namespace OpenConquerStudio;

internal sealed class TerrainGridSurface : Control
{
    private static readonly IPen s_grid = new Pen(Brushes.Black, 0.5);
    private static readonly IPen s_selected = new Pen(Brushes.Orange, 2);
    private static readonly IBrush s_selectionFill = new SolidColorBrush(Color.FromArgb(50, 255, 170, 0));
    private static readonly IBrush[] s_surfaces =
    [
        new SolidColorBrush(Color.FromRgb(44, 66, 77)),
        new SolidColorBrush(Color.FromRgb(58, 75, 69)),
        new SolidColorBrush(Color.FromRgb(79, 70, 65)),
        new SolidColorBrush(Color.FromRgb(70, 67, 90)),
        new SolidColorBrush(Color.FromRgb(61, 82, 93)),
        new SolidColorBrush(Color.FromRgb(88, 83, 67)),
        new SolidColorBrush(Color.FromRgb(61, 75, 81)),
        new SolidColorBrush(Color.FromRgb(86, 67, 77)),
    ];

    private Func<int, int, TerrainCell>? _read;
    private int _width;
    private int _height;
    private int _originX;
    private int _originY;
    private int _cellSize = 28;
    private (int X, int Y)? _anchor;

    public event Action<TerrainRegion>? Selected;

    public TerrainRegion Selection { get; private set; }

    public TerrainGridSurface()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public void Load(TerrainAsset terrain, Func<int, int, TerrainCell> read)
    {
        _width = terrain.Width;
        _height = terrain.Height;
        _read = read;
        _originX = Math.Clamp(_originX, 0, _width - 1);
        _originY = Math.Clamp(_originY, 0, _height - 1);
        InvalidateVisual();
    }

    public void Clear()
    {
        _read = null;
        _width = 0;
        _height = 0;
        _anchor = null;
        InvalidateVisual();
    }

    public void SetView(int x, int y, int cellSize)
    {
        _originX = x;
        _originY = y;
        _cellSize = cellSize;
        InvalidateVisual();
    }

    public void SetSelection(TerrainRegion region)
    {
        Selection = region;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (_read is null)
        {
            return;
        }

        int columns = Math.Min(_width - _originX, (int)Math.Ceiling(Bounds.Width / _cellSize));
        int rows = Math.Min(_height - _originY, (int)Math.Ceiling(Bounds.Height / _cellSize));
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                TerrainCell cell = _read(_originX + x, _originY + y);
                Rect rectangle = new(x * _cellSize, y * _cellSize, _cellSize, _cellSize);
                context.DrawRectangle(s_surfaces[cell.SurfaceId % s_surfaces.Length], s_grid, rectangle);
            }
        }

        int left = Math.Max(Selection.StartX, _originX);
        int top = Math.Max(Selection.StartY, _originY);
        int right = Math.Min(Selection.EndX, _originX + columns - 1);
        int bottom = Math.Min(Selection.EndY, _originY + rows - 1);
        if (left <= right && top <= bottom)
        {
            context.DrawRectangle(s_selectionFill, s_selected, new Rect(
                (left - _originX) * _cellSize, (top - _originY) * _cellSize,
                (right - left + 1) * _cellSize, (bottom - top + 1) * _cellSize));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !TryCell(e.GetPosition(this), out int x, out int y))
        {
            return;
        }

        _anchor = (x, y);
        SetSelection(new TerrainRegion(x, y, x, y));
        Selected?.Invoke(Selection);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_anchor is not { } start || !TryCell(e.GetPosition(this), out int x, out int y))
        {
            return;
        }

        TerrainRegion region = TerrainRegion.Between(start.X, start.Y, x, y);
        if (region != Selection)
        {
            SetSelection(region);
            Selected?.Invoke(region);
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_anchor is null)
        {
            return;
        }

        _anchor = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private bool TryCell(Point point, out int x, out int y)
    {
        x = _originX + (int)Math.Floor(point.X / _cellSize);
        y = _originY + (int)Math.Floor(point.Y / _cellSize);
        return _read is not null && point.X >= 0 && point.Y >= 0 &&
            point.X < Bounds.Width && point.Y < Bounds.Height &&
            x >= 0 && y >= 0 && x < _width && y < _height;
    }
}
