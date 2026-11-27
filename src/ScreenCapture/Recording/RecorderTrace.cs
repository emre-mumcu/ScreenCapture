using System.Globalization;

namespace ScreenCapture.Recording;

/// <summary>Optional diagnostics for the recorder (enabled by setting <see cref="FilePath"/>; off by default).</summary>
internal static class RecorderTrace
{
    private static readonly Lock s_lock = new();

    public static string? FilePath { get; set; }

    public static void Write(string message)
    {
        string? path = FilePath;
        if (path is null)
        {
            return;
        }

        string line = string.Create(CultureInfo.InvariantCulture,
            $"{DateTime.Now:HH:mm:ss.fff} [{Environment.CurrentManagedThreadId,2}] {message}{Environment.NewLine}");
        lock (s_lock)
        {
            File.AppendAllText(path, line);
        }
    }
}
