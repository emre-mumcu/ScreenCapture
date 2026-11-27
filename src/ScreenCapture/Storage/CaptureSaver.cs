using System.Drawing.Imaging;
using System.Globalization;

namespace ScreenCapture.Storage;

/// <summary>
/// Encodes captures as lossless PNG and writes them to the Captures folder on a background thread.
/// Saves are serialized so rapid captures keep their order and never collide.
/// </summary>
internal sealed class CaptureSaver
{
    private const string FileNameFormat = "yyyyMMdd_HHmmss_fff";
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CaptureSaver()
    {
        (Directory, IsFallback) = AppPaths.ResolveCapturesDirectory();
    }

    public string Directory { get; }

    /// <summary>True when the application folder was not writable and the LocalAppData folder is used.</summary>
    public bool IsFallback { get; }

    /// <summary>
    /// Saves the bitmap and returns the full file path. Takes ownership of <paramref name="bitmap"/>
    /// and disposes it when done.
    /// </summary>
    public Task<string> SaveAsync(Bitmap bitmap, DateTime timestamp) => Task.Run(async () =>
    {
        try
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return SaveCore(bitmap, timestamp);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            bitmap.Dispose();
        }
    });

    private string SaveCore(Bitmap bitmap, DateTime timestamp)
    {
        System.IO.Directory.CreateDirectory(Directory);

        string baseName = timestamp.ToString(FileNameFormat, CultureInfo.InvariantCulture);
        string tempPath = Path.Combine(Directory, $"{baseName}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // Screen pixels carry no meaningful alpha: 24bpp RGB is lossless and smaller.
                if (bitmap.PixelFormat == PixelFormat.Format24bppRgb)
                {
                    bitmap.Save(stream, ImageFormat.Png);
                }
                else
                {
                    using Bitmap rgb = bitmap.Clone(new Rectangle(Point.Empty, bitmap.Size), PixelFormat.Format24bppRgb);
                    rgb.Save(stream, ImageFormat.Png);
                }
            }

            return Publish(tempPath, baseName, ".png");
        }
        catch
        {
            try { File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>Path the recorder writes to while recording (renamed by <see cref="PublishVideo"/>).</summary>
    public string CreateVideoTempPath(DateTime timestamp)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string baseName = timestamp.ToString(FileNameFormat, CultureInfo.InvariantCulture);
        return Path.Combine(Directory, $"{baseName}.{Guid.NewGuid():N}.recording.mp4");
    }

    /// <summary>Renames a finished recording to yyyyMMdd_HHmmss_fff.mp4 and returns the final path.</summary>
    public string PublishVideo(string tempPath, DateTime timestamp) =>
        Publish(tempPath, timestamp.ToString(FileNameFormat, CultureInfo.InvariantCulture), ".mp4");

    /// <summary>Atomic publish: never overwrites an existing file, appends _1, _2 ... on collision.</summary>
    private string Publish(string tempPath, string baseName, string extension)
    {
        for (int suffix = 0; ; suffix++)
        {
            string fileName = suffix == 0 ? $"{baseName}{extension}" : $"{baseName}_{suffix}{extension}";
            string target = Path.Combine(Directory, fileName);
            if (File.Exists(target))
            {
                continue;
            }

            try
            {
                File.Move(tempPath, target, overwrite: false);
                return target;
            }
            catch (IOException) when (File.Exists(target))
            {
                // Lost a race with another writer; try the next suffix.
            }
        }
    }
}
