namespace ScreenCapture.Storage;

/// <summary>
/// Resolves where the application stores its data. The application folder is preferred;
/// when it is not writable (e.g. under Program Files) %LocalAppData%\ScreenCapture is used.
/// </summary>
internal static class AppPaths
{
    public const string CapturesFolderName = "Captures";

    public static string ApplicationDirectory { get; } = AppContext.BaseDirectory;

    public static string FallbackDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenCapture");

    private static readonly Lazy<string> s_dataDirectory = new(ResolveDataDirectory);

    /// <summary>Folder for settings.json and error.log.</summary>
    public static string DataDirectory => s_dataDirectory.Value;

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string ErrorLogFile => Path.Combine(DataDirectory, "error.log");

    /// <summary>
    /// Returns the Captures folder next to the executable, or the fallback folder if that is not writable.
    /// </summary>
    public static (string Directory, bool IsFallback) ResolveCapturesDirectory()
    {
        string primary = Path.Combine(ApplicationDirectory, CapturesFolderName);
        if (IsWritable(primary))
        {
            return (primary, false);
        }

        string fallback = Path.Combine(FallbackDirectory, CapturesFolderName);
        Directory.CreateDirectory(fallback);
        return (fallback, true);
    }

    private static string ResolveDataDirectory()
    {
        if (IsWritable(ApplicationDirectory))
        {
            return ApplicationDirectory;
        }

        Directory.CreateDirectory(FallbackDirectory);
        return FallbackDirectory;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
