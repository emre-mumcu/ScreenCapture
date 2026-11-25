# Regenerates src/ScreenCapture/Assets/app.ico from the developer photo.
# Usage: .	oolsmake-icon.ps1 -Source srcScreenCaptureAssetsEmreIO.jpeg -IcoOut srcScreenCaptureAssetspp.ico -PreviewOut preview.png

param(
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $IcoOut,
    [Parameter(Mandatory)] [string] $PreviewOut,
    [int] $CropX = 150, [int] $CropY = 70, [int] $CropSize = 820
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IconMaker
{
    // Round avatar: square crop -> circular mask (anti-aliased) -> thin ring so it reads on light and dark.
    public static Bitmap Render(Image src, Rectangle crop, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            float inset = Math.Max(0.5f, size / 64f);
            var circle = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(circle);
                using (var tex = new TextureBrush(Scale(src, crop, size)))
                {
                    g.FillPath(tex, path);
                }
                using (var pen = new Pen(Color.FromArgb(255, 0, 103, 192), Math.Max(1f, size / 28f)))
                {
                    g.DrawEllipse(pen, circle);
                }
            }
        }
        return bmp;
    }

    static Bitmap Scale(Image src, Rectangle crop, int size)
    {
        var scaled = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new Rectangle(0, 0, size, size), crop, GraphicsUnit.Pixel);
        }
        return scaled;
    }

    // ICO container with PNG-compressed entries (supported since Windows Vista).
    public static void WriteIco(string path, IList<Bitmap> images)
    {
        var pngs = new List<byte[]>();
        foreach (var img in images)
        {
            using (var ms = new MemoryStream()) { img.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
        }

        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)images.Count);
            int offset = 6 + 16 * images.Count;
            for (int i = 0; i < images.Count; i++)
            {
                int s = images[i].Width;
                w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }
    }
}
'@

$src = [System.Drawing.Image]::FromFile($Source)
try {
    $crop = New-Object System.Drawing.Rectangle($CropX, $CropY, $CropSize, $CropSize)
    $sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
    $images = New-Object 'System.Collections.Generic.List[System.Drawing.Bitmap]'
    foreach ($s in $sizes) { $images.Add([IconMaker]::Render($src, $crop, $s)) }
    [IconMaker]::WriteIco($IcoOut, $images)
    $images[$images.Count - 1].Save($PreviewOut, [System.Drawing.Imaging.ImageFormat]::Png)
    foreach ($i in $images) { $i.Dispose() }
    "Icon written: $IcoOut ($((Get-Item $IcoOut).Length) bytes)"
}
finally { $src.Dispose() }
