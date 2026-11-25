using System.Drawing.Drawing2D;

namespace ScreenCapture.Forms;

/// <summary>Shows a square part of an image as a circle with an accent ring (like the app icon).</summary>
internal sealed class RoundPhoto : Control
{
    private readonly Image _image;
    private readonly Rectangle _source;

    public RoundPhoto(Image image, Rectangle source)
    {
        _image = image;
        _source = source;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int size = Math.Min(Width, Height);
        float ring = Math.Max(2f, size / 40f);
        var circle = new RectangleF(ring / 2, ring / 2, size - ring - 1, size - ring - 1);

        using (var path = new GraphicsPath())
        {
            path.AddEllipse(circle);
            GraphicsState state = g.Save();
            g.SetClip(path);
            g.DrawImage(_image, Rectangle.Round(circle), _source, GraphicsUnit.Pixel);
            g.Restore(state);
        }

        using var pen = new Pen(UiTheme.Accent, ring);
        g.DrawEllipse(pen, circle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image.Dispose();
        }

        base.Dispose(disposing);
    }
}
