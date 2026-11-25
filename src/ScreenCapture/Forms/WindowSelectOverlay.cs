using System.Globalization;
using ScreenCapture.Capture;

namespace ScreenCapture.Forms;

/// <summary>Highlights the window under the mouse on the frozen screenshot; a left click selects it.</summary>
internal sealed class WindowSelectOverlay : OverlayFormBase
{
    private const int BorderWidth = 3;

    private readonly IReadOnlyList<WindowInfo> _windows;
    private readonly TaskCompletionSource<WindowInfo?> _completion = new();
    private readonly Pen _borderPen = new(AccentColor, BorderWidth);
    private WindowInfo? _hover;
    private Rectangle _labelRect;

    public WindowSelectOverlay(Bitmap frozen, Rectangle virtualBounds, IReadOnlyList<WindowInfo> windows)
        : base(frozen, virtualBounds, Localization.Strings.WindowOverlayHint)
    {
        _windows = windows;
        Cursor = Cursors.Hand;
    }

    public Task<WindowInfo?> SelectAsync()
    {
        Show();
        return _completion.Task;
    }

    protected override void Cancel()
    {
        _completion.TrySetResult(null);
        Close();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateHover(Cursor.Position);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdateHover(ToScreen(e.Location));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            UpdateHover(ToScreen(e.Location));
            if (_hover is not null)
            {
                _completion.TrySetResult(_hover);
                Close();
            }

            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Rectangle clip = e.ClipRectangle;
        Rectangle highlight = _hover is null ? Rectangle.Empty : ToClient(_hover.Bounds);

        PaintFrozen(g, clip, highlight);
        PaintHint(g, clip);

        if (_hover is not null)
        {
            // Border drawn inside the window bounds so it is visible on maximized windows too.
            Rectangle border = Rectangle.Inflate(highlight, -BorderWidth / 2 - 1, -BorderWidth / 2 - 1);
            g.DrawRectangle(_borderPen, border);
            _labelRect = PaintLabel(g, SizeText(highlight), highlight);
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

    private void UpdateHover(Point screenPoint)
    {
        WindowInfo? hover = WindowFinder.FindAt(_windows, screenPoint);
        if (hover == _hover)
        {
            return;
        }

        if (_hover is not null)
        {
            Invalidate(Rectangle.Union(ToClient(_hover.Bounds), _labelRect));
        }

        _hover = hover;
        _labelRect = Rectangle.Empty;
        if (hover is not null)
        {
            Rectangle client = ToClient(hover.Bounds);
            Invalidate(Rectangle.Union(client, GetLabelRect(SizeText(client), client)));
        }
    }

    private static string SizeText(Rectangle r) =>
        string.Create(CultureInfo.InvariantCulture, $"{r.Width} x {r.Height}");
}
