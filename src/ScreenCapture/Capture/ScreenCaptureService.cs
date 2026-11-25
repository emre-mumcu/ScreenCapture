using System.Drawing.Imaging;
using ScreenCapture.Localization;
using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Capture;

/// <summary>
/// Produces bitmaps from the screen. All rectangles are physical pixels in virtual-screen
/// coordinates (the process is Per-Monitor-V2 DPI aware).
/// </summary>
internal static class ScreenCaptureService
{
    /// <summary>
    /// 32bpp without alpha: fast to blit/draw and safe for GDI drawing (cursor), saved later as 24bpp PNG.
    /// </summary>
    public const PixelFormat WorkingFormat = PixelFormat.Format32bppRgb;

    public static Rectangle VirtualScreen => SystemInformation.VirtualScreen;

    public static Bitmap CaptureRectangle(Rectangle screenRect, bool includeCursor)
    {
        if (screenRect.Width <= 0 || screenRect.Height <= 0)
        {
            throw new ArgumentException(Strings.EmptyCaptureArea, nameof(screenRect));
        }

        var bitmap = new Bitmap(screenRect.Width, screenRect.Height, WorkingFormat);
        try
        {
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(screenRect.Location, Point.Empty, screenRect.Size, CopyPixelOperation.SourceCopy);
            if (includeCursor)
            {
                CursorRenderer.Draw(graphics, screenRect.Location);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static Bitmap CaptureVirtualScreen(bool includeCursor) => CaptureRectangle(VirtualScreen, includeCursor);

    public static Bitmap CaptureMonitorAtCursor(bool includeCursor) =>
        CaptureRectangle(Screen.FromPoint(Cursor.Position).Bounds, includeCursor);

    /// <summary>Copies a part of <paramref name="source"/>; the rectangle is clipped to the image.</summary>
    public static Bitmap Crop(Bitmap source, Rectangle rect)
    {
        rect.Intersect(new Rectangle(Point.Empty, source.Size));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentException(Strings.SelectionOffScreen, nameof(rect));
        }

        return source.Clone(rect, WorkingFormat);
    }

    /// <summary>
    /// Renders a window with PrintWindow(PW_RENDERFULLCONTENT), which also works when it is covered by
    /// other windows. Returns null if the window could not be rendered (or rendered fully black).
    /// </summary>
    public static Bitmap? CaptureWindowContent(nint hwnd, Rectangle frameBounds)
    {
        if (!GetWindowRect(hwnd, out RECT windowRect))
        {
            return null;
        }

        Rectangle full = windowRect.ToRectangle();
        if (full.Width <= 0 || full.Height <= 0)
        {
            return null;
        }

        using var bitmap = new Bitmap(full.Width, full.Height, WorkingFormat);
        bool rendered;
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            nint hdc = graphics.GetHdc();
            try
            {
                rendered = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }

        if (!rendered)
        {
            return null;
        }

        // GetWindowRect includes invisible borders; cut down to the visible frame.
        var crop = new Rectangle(frameBounds.X - full.X, frameBounds.Y - full.Y, frameBounds.Width, frameBounds.Height);
        crop.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        if (crop.Width <= 0 || crop.Height <= 0 || IsBlack(bitmap, crop))
        {
            return null;
        }

        return bitmap.Clone(crop, WorkingFormat);
    }

    /// <summary>Samples a grid of pixels; some apps (hardware-accelerated) render black with PrintWindow.</summary>
    private static bool IsBlack(Bitmap bitmap, Rectangle area)
    {
        const int steps = 12;
        for (int iy = 0; iy < steps; iy++)
        {
            for (int ix = 0; ix < steps; ix++)
            {
                int x = area.X + (int)((long)area.Width * (2 * ix + 1) / (2 * steps));
                int y = area.Y + (int)((long)area.Height * (2 * iy + 1) / (2 * steps));
                if ((bitmap.GetPixel(x, y).ToArgb() & 0x00FFFFFF) != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
