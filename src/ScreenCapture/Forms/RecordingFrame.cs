using ScreenCapture.Interop;

namespace ScreenCapture.Forms;

/// <summary>
/// Red frame around the area being recorded (or, briefly, the area just captured / saved). Click-through, never activated, and excluded from
/// screen capture, so it is visible to the user but not in the video.
/// </summary>
internal sealed class RecordingFrame : Form
{
    private const int Thickness = 3;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly Color FrameColor = Color.FromArgb(232, 17, 35);
    private static readonly Color KeyColor = Color.FromArgb(1, 2, 3);

    public RecordingFrame()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = KeyColor;
        TransparencyKey = KeyColor; // only the border is visible
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW_INT;
            return cp;
        }
    }

    /// <summary>
    /// Shows the frame just outside <paramref name="area"/> (screen coordinates, physical pixels). Where that would
    /// leave the monitors (e.g. a full screen area), the frame is drawn on the inner edge so it stays visible.
    /// </summary>
    public void Track(Rectangle area)
    {
        Rectangle bounds = Rectangle.Inflate(area, Thickness, Thickness);
        Rectangle monitors = Rectangle.Empty;
        foreach (Screen screen in Screen.AllScreens)
        {
            if (screen.Bounds.IntersectsWith(area))
            {
                monitors = monitors.IsEmpty ? screen.Bounds : Rectangle.Union(monitors, screen.Bounds);
            }
        }

        if (!monitors.IsEmpty)
        {
            bounds.Intersect(monitors);
        }

        if (bounds.Width <= 2 * Thickness || bounds.Height <= 2 * Thickness)
        {
            Conceal();
            return;
        }

        if (Bounds != bounds || !NativeMethods.IsWindowVisible(Handle))
        {
            NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            Invalidate();
        }
    }

    /// <summary>Hides the frame without destroying it; <see cref="Track"/> shows it again.</summary>
    public void Conceal()
    {
        if (IsHandleCreated && NativeMethods.IsWindowVisible(Handle))
        {
            NativeMethods.SetWindowPos(Handle, 0, 0, 0, 0, 0,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_HIDEWINDOW | 0x0001 | 0x0002 | 0x0004); // no size/move/z-order
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
#if DEBUG
        if (Environment.GetEnvironmentVariable("SCREENCAPTURE_UI_VISIBLE") == "1")
        {
            return; // diagnostics: let screenshots show the frame
        }
#endif
        NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_DPICHANGED)
        {
            m.Result = 0; // keep exact physical bounds across monitors
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var brush = new SolidBrush(FrameColor);
        Rectangle r = ClientRectangle;
        e.Graphics.FillRectangle(brush, 0, 0, r.Width, Thickness);
        e.Graphics.FillRectangle(brush, 0, r.Height - Thickness, r.Width, Thickness);
        e.Graphics.FillRectangle(brush, 0, 0, Thickness, r.Height);
        e.Graphics.FillRectangle(brush, r.Width - Thickness, 0, Thickness, r.Height);
    }
}
