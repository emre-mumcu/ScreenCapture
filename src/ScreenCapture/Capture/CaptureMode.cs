namespace ScreenCapture.Capture;

public enum CaptureMode
{
    FullScreen = 0,
    Window = 1,
    Region = 2,
}

internal static class CaptureModeExtensions
{
    public static string DisplayName(this CaptureMode mode) => mode switch
    {
        CaptureMode.FullScreen => Localization.Strings.ModeFullScreen,
        CaptureMode.Window => Localization.Strings.ModeWindow,
        CaptureMode.Region => Localization.Strings.ModeRegion,
        _ => mode.ToString(),
    };
}

/// <summary>
/// Capture-related settings snapshot passed to <see cref="CaptureController"/>.
/// A non-null target makes the capture non-interactive for that mode.
/// </summary>
internal readonly record struct CaptureOptions(
    bool IncludeCursor,
    bool ActiveWindowDirectly,
    MonitorTarget? Monitor,
    WindowTarget? Window,
    RegionTarget? Region);
