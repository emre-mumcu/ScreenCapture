using ScreenCapture.Interop;

namespace ScreenCapture.Recording;

/// <summary>
/// Copies a screen rectangle into a top-down 32-bit DIB section every frame (optionally scaled down),
/// so the pixels can go straight into the encoder without extra conversions.
/// Must be created, used and disposed on the same thread (GDI device contexts are thread-affine).
/// </summary>
internal sealed unsafe class FrameGrabber : IDisposable
{
    private readonly nint _screenDc;
    private readonly nint _memoryDc;
    private readonly nint _bitmap;
    private readonly nint _previousBitmap;
    private readonly bool _scaled;

    public FrameGrabber(int width, int height, bool scaled)
    {
        Width = width;
        Height = height;
        _scaled = scaled;

        _screenDc = Gdi.GetDC(0);
        _memoryDc = Gdi.CreateCompatibleDC(_screenDc);

        var header = new Gdi.BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(Gdi.BITMAPINFOHEADER),
            biWidth = width,
            biHeight = -height, // negative = top-down rows
            biPlanes = 1,
            biBitCount = 32,
        };
        void* bits;
        _bitmap = Gdi.CreateDIBSection(_screenDc, &header, Gdi.DIB_RGB_COLORS, &bits, 0, 0);
        if (_bitmap == 0)
        {
            Dispose();
            throw new InvalidOperationException("CreateDIBSection failed.");
        }

        Pixels = (byte*)bits;
        _previousBitmap = Gdi.SelectObject(_memoryDc, _bitmap);
        if (scaled)
        {
            Gdi.SetStretchBltMode(_memoryDc, Gdi.HALFTONE);
            Gdi.SetBrushOrgEx(_memoryDc, 0, 0, 0);
        }
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Top-down BGRA pixels, stride = Width * 4.</summary>
    public byte* Pixels { get; }

    /// <summary>Copies <paramref name="source"/> (screen coordinates) into the frame buffer.</summary>
    public void Capture(Rectangle source, bool includeCursor)
    {
        if (_scaled)
        {
            Gdi.StretchBlt(_memoryDc, 0, 0, Width, Height, _screenDc, source.X, source.Y, source.Width, source.Height, Gdi.SRCCOPY);
        }
        else
        {
            Gdi.BitBlt(_memoryDc, 0, 0, Width, Height, _screenDc, source.X, source.Y, Gdi.SRCCOPY);
        }

        if (includeCursor)
        {
            float scale = (float)Width / source.Width;
            global::ScreenCapture.Capture.CursorRenderer.Draw(_memoryDc, source.Location, scale);
        }
    }

    public void Dispose()
    {
        if (_previousBitmap != 0)
        {
            Gdi.SelectObject(_memoryDc, _previousBitmap);
        }

        if (_bitmap != 0)
        {
            Gdi.DeleteObject(_bitmap);
        }

        if (_memoryDc != 0)
        {
            Gdi.DeleteDC(_memoryDc);
        }

        if (_screenDc != 0)
        {
            Gdi.ReleaseDC(0, _screenDc);
        }
    }
}
