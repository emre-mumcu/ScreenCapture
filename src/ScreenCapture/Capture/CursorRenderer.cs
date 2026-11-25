using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Capture;

/// <summary>Draws the current mouse pointer into a captured image.</summary>
internal static unsafe class CursorRenderer
{
    /// <param name="graphics">Graphics of the target bitmap (must be a non-alpha format such as 32bppRgb).</param>
    /// <param name="captureOrigin">Screen position of the bitmap's top-left pixel.</param>
    public static void Draw(Graphics graphics, Point captureOrigin)
    {
        var info = new CURSORINFO { cbSize = sizeof(CURSORINFO) };
        if (!GetCursorInfo(ref info) || (info.flags & CURSOR_SHOWING) == 0 || info.hCursor == 0)
        {
            return;
        }

        int hotspotX = 0, hotspotY = 0;
        if (GetIconInfo(info.hCursor, out ICONINFO iconInfo))
        {
            hotspotX = iconInfo.xHotspot;
            hotspotY = iconInfo.yHotspot;
            if (iconInfo.hbmMask != 0) DeleteObject(iconInfo.hbmMask);
            if (iconInfo.hbmColor != 0) DeleteObject(iconInfo.hbmColor);
        }

        int x = info.ptScreenPos.X - captureOrigin.X - hotspotX;
        int y = info.ptScreenPos.Y - captureOrigin.Y - hotspotY;

        nint hdc = graphics.GetHdc();
        try
        {
            DrawIconEx(hdc, x, y, info.hCursor, 0, 0, 0, 0, DI_NORMAL);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }
}
