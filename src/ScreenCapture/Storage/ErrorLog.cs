using System.Globalization;

namespace ScreenCapture.Storage;

/// <summary>Appends unexpected errors to error.log. Never throws.</summary>
internal static class ErrorLog
{
    private static readonly Lock s_sync = new();

    public static void Write(Exception exception, string? context = null)
    {
        try
        {
            string entry = string.Create(CultureInfo.InvariantCulture,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {context ?? "Error"}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");

            lock (s_sync)
            {
                File.AppendAllText(AppPaths.ErrorLogFile, entry);
            }
        }
        catch
        {
            // Logging must never take the application down.
        }
    }
}
