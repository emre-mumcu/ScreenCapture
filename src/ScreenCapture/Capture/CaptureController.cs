using ScreenCapture.Forms;
using ScreenCapture.Interop;
using ScreenCapture.Localization;

namespace ScreenCapture.Capture;

/// <summary>
/// Runs captures and the interactive pickers. Must be called on the UI thread.
/// When a mode has a saved target the capture is immediate; otherwise the user picks interactively.
/// </summary>
internal sealed class CaptureController
{
    /// <summary>Returns the captured bitmap (caller owns it) or null if the user cancelled.</summary>
    /// <exception cref="CaptureTargetException">A saved target is currently not available.</exception>
    public async Task<Bitmap?> CaptureAsync(CaptureMode mode, CaptureOptions options)
    {
        return mode switch
        {
            CaptureMode.FullScreen => CaptureFullScreen(options),
            CaptureMode.Window => await CaptureWindowAsync(options),
            CaptureMode.Region => await CaptureRegionAsync(options),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    /// <summary>Lets the user click a window and returns it as a reusable target.</summary>
    public async Task<WindowTarget?> PickWindowTargetAsync()
    {
        List<WindowInfo> windows = WindowFinder.GetCapturableWindows();
        using Bitmap frozen = ScreenCaptureService.CaptureVirtualScreen(includeCursor: false);
        WindowInfo? selected = await SelectWindowAsync(frozen, windows);
        return selected is null ? null : WindowFinder.CreateTarget(selected);
    }

    /// <summary>Lets the user draw a rectangle and returns it in screen coordinates.</summary>
    public async Task<RegionTarget?> PickRegionTargetAsync()
    {
        Rectangle virtualScreen = ScreenCaptureService.VirtualScreen;
        using Bitmap frozen = ScreenCaptureService.CaptureRectangle(virtualScreen, includeCursor: false);
        Rectangle? selection = await SelectRegionAsync(frozen, virtualScreen);
        if (selection is not { } rect)
        {
            return null;
        }

        rect.Offset(virtualScreen.Location);
        return RegionTarget.FromRectangle(rect);
    }

    /// <summary>
    /// Resolves the screen area to record for a mode, using the saved target or an interactive pick.
    /// Returns null if the user cancelled. Window recordings follow the window when it moves.
    /// </summary>
    /// <exception cref="CaptureTargetException">A saved target is currently not available.</exception>
    public async Task<Recording.RecordingArea?> ResolveRecordingAreaAsync(CaptureMode mode, CaptureOptions options)
    {
        switch (mode)
        {
            case CaptureMode.FullScreen:
                return new Recording.RecordingArea(ResolveMonitorBounds(options.Monitor));

            case CaptureMode.Region:
                if (options.Region is { } saved)
                {
                    Rectangle visible = Rectangle.Intersect(saved.ToRectangle(), ScreenCaptureService.VirtualScreen);
                    if (visible.Width <= 0 || visible.Height <= 0)
                    {
                        throw new CaptureTargetException(Strings.RegionOffScreen);
                    }

                    return new Recording.RecordingArea(visible);
                }

                RegionTarget? picked = await PickRegionTargetAsync();
                return picked is null ? null : new Recording.RecordingArea(picked.ToRectangle());

            case CaptureMode.Window:
                List<WindowInfo> windows = WindowFinder.GetCapturableWindows();
                WindowInfo? window;
                if (options.Window is { } savedWindow)
                {
                    window = WindowFinder.ResolveTarget(savedWindow, windows);
                }
                else if (options.ActiveWindowDirectly)
                {
                    window = WindowFinder.GetActiveWindow(windows);
                }
                else
                {
                    using Bitmap frozen = ScreenCaptureService.CaptureVirtualScreen(includeCursor: false);
                    window = await SelectWindowAsync(frozen, windows);
                }

                return window is null ? null : new Recording.RecordingArea(window.Bounds, window.Handle);

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    // ---- Full screen ----------------------------------------------------------

    private static Bitmap CaptureFullScreen(CaptureOptions options) =>
        ScreenCaptureService.CaptureRectangle(ResolveMonitorBounds(options.Monitor), options.IncludeCursor);

    /// <summary>Monitor under the cursor, all monitors, or the saved monitor.</summary>
    private static Rectangle ResolveMonitorBounds(MonitorTarget? target)
    {
        if (target is null)
        {
            return Screen.FromPoint(Cursor.Position).Bounds;
        }

        if (target.AllMonitors)
        {
            return ScreenCaptureService.VirtualScreen;
        }

        Screen screen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == target.DeviceName)
                        ?? throw new CaptureTargetException(Strings.MonitorMissing(target.DeviceName));
        return screen.Bounds;
    }

    // ---- Region ---------------------------------------------------------------

    private static async Task<Bitmap?> CaptureRegionAsync(CaptureOptions options)
    {
        Rectangle virtualScreen = ScreenCaptureService.VirtualScreen;

        if (options.Region is { } saved)
        {
            Rectangle visible = Rectangle.Intersect(saved.ToRectangle(), virtualScreen);
            if (visible.Width <= 0 || visible.Height <= 0)
            {
                throw new CaptureTargetException(Strings.RegionOffScreen);
            }

            return ScreenCaptureService.CaptureRectangle(visible, options.IncludeCursor);
        }

        // Freeze the screen first, so menus/tooltips/videos stay exactly as they were.
        using Bitmap frozen = ScreenCaptureService.CaptureRectangle(virtualScreen, options.IncludeCursor);
        Rectangle? selection = await SelectRegionAsync(frozen, virtualScreen);
        return selection is { } rect ? ScreenCaptureService.Crop(frozen, rect) : null;
    }

    private static async Task<Rectangle?> SelectRegionAsync(Bitmap frozen, Rectangle virtualScreen)
    {
        nint previousForeground = NativeMethods.GetForegroundWindow();
        Rectangle? selection;
        using (var overlay = new RegionSelectOverlay(frozen, virtualScreen))
        {
            selection = await overlay.SelectAsync();
        }

        RestoreForeground(previousForeground);
        return selection;
    }

    // ---- Window ---------------------------------------------------------------

    private static async Task<Bitmap?> CaptureWindowAsync(CaptureOptions options)
    {
        List<WindowInfo> windows = WindowFinder.GetCapturableWindows();

        if (options.Window is { } saved)
        {
            WindowInfo target = WindowFinder.ResolveTarget(saved, windows);
            return CaptureWindow(target, windows, frozen: null, Point.Empty, options.IncludeCursor);
        }

        if (options.ActiveWindowDirectly)
        {
            WindowInfo? active = WindowFinder.GetActiveWindow(windows);
            return active is null ? null : CaptureWindow(active, windows, frozen: null, Point.Empty, options.IncludeCursor);
        }

        Rectangle virtualScreen = ScreenCaptureService.VirtualScreen;
        using Bitmap frozen = ScreenCaptureService.CaptureRectangle(virtualScreen, options.IncludeCursor);
        WindowInfo? selected = await SelectWindowAsync(frozen, windows);
        return selected is null
            ? null
            : CaptureWindow(selected, windows, frozen, virtualScreen.Location, options.IncludeCursor);
    }

    private static async Task<WindowInfo?> SelectWindowAsync(Bitmap frozen, IReadOnlyList<WindowInfo> windows)
    {
        nint previousForeground = NativeMethods.GetForegroundWindow();
        WindowInfo? selected;
        using (var overlay = new WindowSelectOverlay(frozen, ScreenCaptureService.VirtualScreen, windows))
        {
            selected = await overlay.SelectAsync();
        }

        // Selecting a window is an explicit choice: give it the focus.
        RestoreForeground(selected?.Handle ?? previousForeground);
        return selected;
    }

    /// <summary>
    /// Unobstructed windows are copied from the screen (pixel exact, includes everything the user sees).
    /// Covered or partially off-screen windows are rendered with PrintWindow instead.
    /// </summary>
    private static Bitmap CaptureWindow(WindowInfo target, IReadOnlyList<WindowInfo> windows, Bitmap? frozen, Point frozenOrigin, bool includeCursor)
    {
        Rectangle virtualScreen = ScreenCaptureService.VirtualScreen;
        bool occluded = WindowFinder.IsOccluded(windows, target);
        bool partiallyOffScreen = !virtualScreen.Contains(target.Bounds);

        if (occluded || partiallyOffScreen)
        {
            Bitmap? rendered = ScreenCaptureService.CaptureWindowContent(target.Handle, target.Bounds);
            if (rendered is not null)
            {
                return rendered;
            }
        }

        if (frozen is not null)
        {
            Rectangle rect = target.Bounds;
            rect.Offset(-frozenOrigin.X, -frozenOrigin.Y);
            return ScreenCaptureService.Crop(frozen, rect);
        }

        Rectangle visible = Rectangle.Intersect(target.Bounds, virtualScreen);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            throw new CaptureTargetException(Strings.WindowOffScreen);
        }

        return ScreenCaptureService.CaptureRectangle(visible, includeCursor);
    }

    private static void RestoreForeground(nint hwnd)
    {
        if (hwnd != 0)
        {
            NativeMethods.SetForegroundWindow(hwnd);
        }
    }
}
