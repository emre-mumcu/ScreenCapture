using ScreenCapture.Localization;

namespace ScreenCapture.Capture;

/// <summary>Saved full-screen target: one specific monitor or all monitors.</summary>
public sealed record MonitorTarget(bool AllMonitors, string? DeviceName);

/// <summary>
/// Saved window target. The handle is tried first; if the window was closed and reopened it is found
/// again by process name + window class (title is used to prefer the right one among several).
/// </summary>
public sealed record WindowTarget(long Handle, string ProcessName, string ClassName, string Title);

/// <summary>Saved fixed rectangle in physical virtual-screen coordinates.</summary>
public sealed record RegionTarget(int X, int Y, int Width, int Height)
{
    public Rectangle ToRectangle() => new(X, Y, Width, Height);

    public static RegionTarget FromRectangle(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}

/// <summary>A saved target cannot be used right now (monitor unplugged, window closed, ...).</summary>
internal sealed class CaptureTargetException(string message) : Exception(message);

/// <summary>Human readable descriptions of monitors and saved targets.</summary>
internal static class CaptureTargetText
{
    /// <summary>Screens ordered left-to-right, top-to-bottom, so monitor 1 is the leftmost.</summary>
    public static Screen[] OrderedScreens() =>
        Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();

    public static string DescribeScreen(Screen screen, int index) =>
        Strings.MonitorName(index + 1, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);

    public static string Describe(MonitorTarget? target)
    {
        if (target is null)
        {
            return Strings.MonitorUnderCursor;
        }

        if (target.AllMonitors)
        {
            Rectangle v = SystemInformation.VirtualScreen;
            return Strings.AllMonitors(v.Width, v.Height);
        }

        Screen[] screens = OrderedScreens();
        int index = Array.FindIndex(screens, s => s.DeviceName == target.DeviceName);
        return index >= 0 ? DescribeScreen(screens[index], index) : Strings.NotConnected(target.DeviceName);
    }

    public static string Describe(WindowTarget? target, bool activeWindowDirectly)
    {
        if (target is null)
        {
            return activeWindowDirectly ? Strings.ActiveWindowEachTime : Strings.PickWindowEachTime;
        }

        string title = string.IsNullOrWhiteSpace(target.Title) ? target.ClassName : target.Title;
        return $"{target.ProcessName} – {title}";
    }

    public static string Describe(RegionTarget? target) => target is null
        ? Strings.PickRegionEachTime
        : Strings.RegionDescription(target.Width, target.Height, target.X, target.Y);
}
