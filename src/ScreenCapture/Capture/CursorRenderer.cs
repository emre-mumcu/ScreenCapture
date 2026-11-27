using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Capture;

/// <summary>Draws the current mouse pointer into a captured image or video frame.</summary>
internal static unsafe class CursorRenderer
{
    /// <param name="graphics">Graphics of the target bitmap (must be a non-alpha format such as 32bppRgb).</param>
    /// <param name="captureOrigin">Screen position of the bitmap's top-left pixel.</param>
    public static void Draw(Graphics graphics, Point captureOrigin)
    {
        nint hdc = graphics.GetHdc();
        try
        {
            Draw(hdc, captureOrigin, 1f);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    /// <summary>Draws into a device context whose (0,0) is <paramref name="captureOrigin"/>, scaled by <paramref name="scale"/>.</summary>
    public static void Draw(nint hdc, Point captureOrigin, float scale)
    {
        var info = new CURSORINFO { cbSize = sizeof(CURSORINFO) };
        if (!GetCursorInfo(ref info) || (info.flags & CURSOR_SHOWING) == 0 || info.hCursor == 0)
        {
            return;
        }

        int hotspotX = 0, hotspotY = 0, width = 0, height = 0;
        if (GetIconInfo(info.hCursor, out ICONINFO iconInfo))
        {
            hotspotX = iconInfo.xHotspot;
            hotspotY = iconInfo.yHotspot;
            if (scale != 1f)
            {
                // Size of the cursor image: color bitmap, or the AND/XOR mask (twice as tall) for monochrome cursors.
                nint sizeSource = iconInfo.hbmColor != 0 ? iconInfo.hbmColor : iconInfo.hbmMask;
                if (GetBitmapSize(sizeSource, out int bw, out int bh))
                {
                    width = Math.Max(1, (int)(bw * scale));
                    height = Math.Max(1, (int)((iconInfo.hbmColor != 0 ? bh : bh / 2) * scale));
                }
            }

            if (iconInfo.hbmMask != 0) DeleteObject(iconInfo.hbmMask);
            if (iconInfo.hbmColor != 0) DeleteObject(iconInfo.hbmColor);
        }

        int x = (int)((info.ptScreenPos.X - captureOrigin.X - hotspotX) * scale);
        int y = (int)((info.ptScreenPos.Y - captureOrigin.Y - hotspotY) * scale);
        DrawIconEx(hdc, x, y, info.hCursor, width, height, 0, 0, DI_NORMAL);
    }

    private static bool GetBitmapSize(nint hBitmap, out int width, out int height)
    {
        BITMAP bitmap;
        if (GetObject(hBitmap, sizeof(BITMAP), &bitmap) != 0)
        {
            width = bitmap.bmWidth;
            height = bitmap.bmHeight;
            return true;
        }

        width = height = 0;
        return false;
    }
}
