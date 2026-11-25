using System.Drawing.Drawing2D;
using ScreenCapture.Interop;

namespace ScreenCapture.Forms;

/// <summary>
/// Borderless, topmost form covering the whole virtual screen and showing a frozen screenshot.
/// Client coordinates map 1:1 to pixels of the frozen image (Per-Monitor-V2: client units are physical pixels).
/// </summary>
internal abstract class OverlayFormBase : Form
{
    protected static readonly Color AccentColor = Color.FromArgb(0, 120, 215);

    private static readonly SolidBrush s_dimBrush = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidBrush s_labelBackBrush = new(Color.FromArgb(220, 20, 20, 20));

    private readonly Rectangle _virtualBounds;
    private readonly string _hint;
    private readonly Font _font = new("Segoe UI", 10f);
    private Rectangle _hintRect;

    protected OverlayFormBase(Bitmap frozen, Rectangle virtualBounds, string hint)
    {
        Frozen = frozen;
        _virtualBounds = virtualBounds;
        _hint = hint;

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualBounds;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Font = _font;
        Text = "ScreenCapture";

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
    }

    protected Bitmap Frozen { get; }

    /// <summary>Screen position of client (0,0).</summary>
    protected Point ScreenOrigin => _virtualBounds.Location;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW_INT; // keep out of Alt+Tab
            return cp;
        }
    }

    protected Point ToScreen(Point client) => new(client.X + _virtualBounds.X, client.Y + _virtualBounds.Y);

    protected Rectangle ToClient(Rectangle screen) =>
        new(screen.X - _virtualBounds.X, screen.Y - _virtualBounds.Y, screen.Width, screen.Height);

    protected abstract void Cancel();

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // Enforce exact physical bounds across monitors with different DPI.
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST,
            _virtualBounds.X, _virtualBounds.Y, _virtualBounds.Width, _virtualBounds.Height,
            NativeMethods.SWP_SHOWWINDOW);

        // Opened from a global hotkey while another app is in front: force focus so Esc works.
        NativeMethods.ForceForeground(Handle);
        Activate();

        Rectangle monitor = ToClient(Screen.FromPoint(Cursor.Position).Bounds);
        Size textSize = TextRenderer.MeasureText(_hint, Font);
        _hintRect = new Rectangle(
            monitor.X + (monitor.Width - textSize.Width) / 2 - 12,
            monitor.Y + 24,
            textSize.Width + 24,
            textSize.Height + 12);
        Invalidate(_hintRect);
    }

    protected override void WndProc(ref Message m)
    {
        // The overlay spans monitors with different DPI; never let WinForms resize/rescale it.
        if (m.Msg == NativeMethods.WM_DPICHANGED)
        {
            m.Result = 0;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Cancel();
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            Cancel();
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Everything is painted in OnPaint.
    }

    /// <summary>Paints the frozen screenshot with dimming everywhere except <paramref name="highlight"/>.</summary>
    protected void PaintFrozen(Graphics g, Rectangle clip, Rectangle highlight)
    {
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(Frozen, clip, clip, GraphicsUnit.Pixel);

        g.CompositingMode = CompositingMode.SourceOver;
        g.PixelOffsetMode = PixelOffsetMode.Default;
        using var dimRegion = new Region(clip);
        if (highlight.Width > 0 && highlight.Height > 0)
        {
            dimRegion.Exclude(highlight);
        }

        g.FillRegion(s_dimBrush, dimRegion);
    }

    protected void PaintHint(Graphics g, Rectangle clip)
    {
        if (_hintRect.IsEmpty || !clip.IntersectsWith(_hintRect))
        {
            return;
        }

        g.FillRectangle(s_labelBackBrush, _hintRect);
        TextRenderer.DrawText(g, _hint, Font, _hintRect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    /// <summary>Draws a size/info label next to <paramref name="anchor"/> and returns the label rectangle.</summary>
    protected Rectangle PaintLabel(Graphics g, string text, Rectangle anchor)
    {
        Rectangle rect = GetLabelRect(text, anchor);
        g.FillRectangle(s_labelBackBrush, rect);
        TextRenderer.DrawText(g, text, Font, rect, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        return rect;
    }

    /// <summary>Label below the anchor's bottom-left corner; above it or inside if there is no room.</summary>
    protected Rectangle GetLabelRect(string text, Rectangle anchor)
    {
        Size size = TextRenderer.MeasureText(text, Font);
        size.Width += 12;
        size.Height += 6;

        var rect = new Rectangle(new Point(anchor.Left, anchor.Bottom + 4), size);
        if (rect.Bottom > ClientSize.Height)
        {
            rect.Y = anchor.Top - size.Height - 4;
        }

        if (rect.Top < 0)
        {
            rect.Y = anchor.Top + 4;
        }

        rect.X = Math.Clamp(rect.X, 0, Math.Max(0, ClientSize.Width - size.Width));
        return rect;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
