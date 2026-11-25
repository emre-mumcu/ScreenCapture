using System.Drawing.Drawing2D;
using ScreenCapture.Interop;

namespace ScreenCapture;

/// <summary>Builds the application icon (a small camera) at runtime.</summary>
internal static class AppIcon
{
    public static Icon Create(int size = 32)
    {
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            float s = size / 32f;
            using var bodyBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
            using var lensBrush = new SolidBrush(Color.White);
            using var innerBrush = new SolidBrush(Color.FromArgb(0, 84, 153));

            // Viewfinder bump
            using (GraphicsPath bump = RoundedRect(new RectangleF(10 * s, 5 * s, 12 * s, 6 * s), 2 * s))
            {
                g.FillPath(bodyBrush, bump);
            }

            // Body
            using (GraphicsPath body = RoundedRect(new RectangleF(2 * s, 9 * s, 28 * s, 19 * s), 4 * s))
            {
                g.FillPath(bodyBrush, body);
            }

            // Lens
            g.FillEllipse(lensBrush, 10 * s, 12 * s, 12 * s, 12 * s);
            g.FillEllipse(innerBrush, 13 * s, 15 * s, 6 * s, 6 * s);
        }

        nint hIcon = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(hIcon);
            return (Icon)temporary.Clone(); // the clone owns its own handle
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
