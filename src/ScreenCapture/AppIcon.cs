using System.Reflection;

namespace ScreenCapture;

/// <summary>Application icon and images embedded in the assembly (see Assets/).</summary>
internal static class AppIcon
{
    private const string IconResource = "ScreenCapture.app.ico";
    private const string PhotoResource = "ScreenCapture.EmreIO.jpeg";

    /// <summary>Multi-size icon for windows (Windows picks the best size for title bar and Alt+Tab).</summary>
    public static Icon Create() => Load(null);

    /// <summary>Icon for the notification area, loaded at the small-icon size to stay sharp.</summary>
    public static Icon CreateSmall() => Load(SystemInformation.SmallIconSize);

    /// <summary>The developer photo shown in the About dialog.</summary>
    public static Image LoadPhoto()
    {
        using Stream stream = OpenResource(PhotoResource);
        using var image = Image.FromStream(stream);
        return new Bitmap(image); // detach from the stream
    }

    private static Icon Load(Size? size)
    {
        using Stream stream = OpenResource(IconResource);
        return size is { } s ? new Icon(stream, s) : new Icon(stream);
    }

    private static Stream OpenResource(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
}
