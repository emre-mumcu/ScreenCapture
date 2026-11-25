using System.Globalization;

namespace ScreenCapture.Forms;

/// <summary>Lets the user drag a rectangle on the frozen screenshot. Result is in client (= frozen image) coordinates.</summary>
internal sealed class RegionSelectOverlay : OverlayFormBase
{
    private const int MinimumSelectionSize = 3;

    private readonly TaskCompletionSource<Rectangle?> _completion = new();
    private readonly Pen _borderPen = new(AccentColor, 1f);
    private Point _start;
    private bool _dragging;
    private Rectangle _selection;
    private Rectangle _labelRect;

    public RegionSelectOverlay(Bitmap frozen, Rectangle virtualBounds)
        : base(frozen, virtualBounds, Localization.Strings.RegionOverlayHint)
    {
    }

    public Task<Rectangle?> SelectAsync()
    {
        Show();
        return _completion.Task;
    }

    protected override void Cancel()
    {
        _completion.TrySetResult(null);
        Close();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _start = e.Location;
        UpdateSelection(new Rectangle(e.Location, new Size(1, 1)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            // Inclusive of both the start pixel and the pixel under the cursor.
            UpdateSelection(Rectangle.FromLTRB(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Max(_start.X, e.X) + 1, Math.Max(_start.Y, e.Y) + 1));
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _dragging)
        {
            _dragging = false;
            if (_selection.Width >= MinimumSelectionSize && _selection.Height >= MinimumSelectionSize)
            {
                _completion.TrySetResult(_selection);
                Close();
            }
            else
            {
                UpdateSelection(Rectangle.Empty);
            }

            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle clip = e.ClipRectangle;
        PaintFrozen(g, clip, _selection);
        PaintHint(g, clip);

        if (_selection.Width > 0 && _selection.Height > 0)
        {
            g.DrawRectangle(_borderPen, Rectangle.Inflate(_selection, 1, 1) with
            {
                Width = _selection.Width + 1,
                Height = _selection.Height + 1,
            });
            _labelRect = PaintLabel(g, SizeText(_selection), _selection);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _completion.TrySetResult(null);
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _borderPen.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateSelection(Rectangle selection)
    {
        Rectangle oldArea = Rectangle.Union(Rectangle.Inflate(_selection, 2, 2), _labelRect);
        _selection = selection;
        Rectangle newArea = selection.IsEmpty
            ? Rectangle.Empty
            : Rectangle.Union(Rectangle.Inflate(selection, 2, 2), GetLabelRect(SizeText(selection), selection));

        Invalidate(oldArea);
        Invalidate(newArea);
        if (selection.IsEmpty)
        {
            _labelRect = Rectangle.Empty;
        }
    }

    private static string SizeText(Rectangle r) =>
        string.Create(CultureInfo.InvariantCulture, $"{r.Width} x {r.Height}");
}
