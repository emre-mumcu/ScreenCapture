using System.Runtime.InteropServices;
using ScreenCapture.Localization;
using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Capture;

/// <summary>A capturable top-level window with its visible frame bounds in physical screen pixels.</summary>
internal sealed record WindowInfo(nint Handle, Rectangle Bounds, string ClassName);

/// <summary>Enumerates and filters top-level windows for window capture.</summary>
internal static unsafe class WindowFinder
{
    private static readonly uint s_currentProcessId = (uint)Environment.ProcessId;

    /// <summary>Returns capturable top-level windows in Z-order, topmost first.</summary>
    public static List<WindowInfo> GetCapturableWindows()
    {
        var windows = new List<WindowInfo>();
        GCHandle handle = GCHandle.Alloc(windows);
        try
        {
            EnumWindows(&EnumWindowsProc, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return windows;
    }

    /// <summary>Topmost window containing the given screen point.</summary>
    public static WindowInfo? FindAt(IReadOnlyList<WindowInfo> windows, Point screenPoint)
    {
        foreach (WindowInfo window in windows)
        {
            if (window.Bounds.Contains(screenPoint))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>True if any window above <paramref name="target"/> overlaps it.</summary>
    public static bool IsOccluded(IReadOnlyList<WindowInfo> windows, WindowInfo target)
    {
        foreach (WindowInfo window in windows)
        {
            if (window.Handle == target.Handle)
            {
                return false;
            }

            if (window.Bounds.IntersectsWith(target.Bounds))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The foreground window, or the topmost capturable window when the foreground
    /// window belongs to this application (e.g. after clicking "Yakala").
    /// </summary>
    public static WindowInfo? GetActiveWindow(IReadOnlyList<WindowInfo> windows)
    {
        nint foreground = GetForegroundWindow();
        if (foreground != 0)
        {
            nint root = GetAncestor(foreground, GA_ROOT);
            if (root != 0)
            {
                foreground = root;
            }

            foreach (WindowInfo window in windows)
            {
                if (window.Handle == foreground)
                {
                    return window;
                }
            }
        }

        return windows.Count > 0 ? windows[0] : null;
    }

    /// <summary>
    /// Visible frame bounds. DWMWA_EXTENDED_FRAME_BOUNDS excludes the invisible resize borders and
    /// shadow that GetWindowRect includes on Windows 10/11.
    /// </summary>
    public static Rectangle GetFrameBounds(nint hwnd)
    {
        RECT rect;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(RECT)) == 0)
        {
            return rect.ToRectangle();
        }

        return GetWindowRect(hwnd, out rect) ? rect.ToRectangle() : Rectangle.Empty;
    }

    [UnmanagedCallersOnly]
    private static int EnumWindowsProc(nint hwnd, nint lParam)
    {
        try
        {
            var windows = (List<WindowInfo>)GCHandle.FromIntPtr(lParam).Target!;
            if (TryCreate(hwnd, out WindowInfo? info))
            {
                windows.Add(info);
            }
        }
        catch
        {
            // Never let exceptions cross the native boundary; skip the window.
        }

        return 1; // continue enumeration
    }

    private static bool TryCreate(nint hwnd, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out WindowInfo? info)
    {
        info = null;

        if (!IsWindowVisible(hwnd) || IsIconic(hwnd))
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == s_currentProcessId)
        {
            return false; // our own forms and overlays
        }

        if (IsCloaked(hwnd))
        {
            return false; // e.g. suspended UWP apps, windows on other virtual desktops
        }

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if ((exStyle & WS_EX_TRANSPARENT) != 0)
        {
            return false; // click-through overlays
        }

        string className = GetClassName(hwnd);
        if (className is "Progman" or "WorkerW")
        {
            return false; // desktop
        }

        if ((exStyle & WS_EX_TOOLWINDOW) != 0 && GetWindowTextLength(hwnd) == 0)
        {
            return false; // untitled helper/tool windows
        }

        Rectangle bounds = GetFrameBounds(hwnd);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return false;
        }

        info = new WindowInfo(hwnd, bounds, className);
        return true;
    }

    /// <summary>Builds a persistable description of a window.</summary>
    public static WindowTarget CreateTarget(WindowInfo window) =>
        new(window.Handle, GetProcessName(window.Handle), window.ClassName, GetWindowTitle(window.Handle));

    /// <summary>
    /// Finds a saved window among the current windows: same handle first, otherwise the same
    /// process + class (preferring an identical title). Throws if it is not available.
    /// </summary>
    public static WindowInfo ResolveTarget(WindowTarget target, IReadOnlyList<WindowInfo> windows)
    {
        nint handle = (nint)target.Handle;
        foreach (WindowInfo window in windows)
        {
            if (window.Handle == handle && SameProcess(window.Handle, target))
            {
                return window;
            }
        }

        WindowInfo? sameClass = null;
        foreach (WindowInfo window in windows)
        {
            if (window.ClassName != target.ClassName || !SameProcess(window.Handle, target))
            {
                continue;
            }

            if (GetWindowTitle(window.Handle) == target.Title)
            {
                return window;
            }

            sameClass ??= window;
        }

        if (sameClass is not null)
        {
            return sameClass;
        }

        if (IsWindow(handle) && IsIconic(handle))
        {
            throw new CaptureTargetException(Strings.WindowMinimized(target.ProcessName));
        }

        throw new CaptureTargetException(Strings.WindowMissing(target.ProcessName));
    }

    public static string GetWindowTitle(nint hwnd)
    {
        const int capacity = 512;
        char* buffer = stackalloc char[capacity];
        int length = GetWindowText(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public static string GetProcessName(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint processId);
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    private static bool SameProcess(nint hwnd, WindowTarget target) =>
        string.Equals(GetProcessName(hwnd), target.ProcessName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the window exists and is currently visible on the desktop (not minimized, hidden or cloaked).</summary>
    public static bool IsShownOnScreen(nint hwnd) =>
        hwnd != 0 && IsWindow(hwnd) && IsWindowVisible(hwnd) && !IsIconic(hwnd) && !IsCloaked(hwnd);

    private static bool IsCloaked(nint hwnd)
    {
        int cloaked = 0;
        return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &cloaked, sizeof(int)) == 0 && cloaked != 0;
    }

    private static string GetClassName(nint hwnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = Interop.NativeMethods.GetClassName(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }
}
