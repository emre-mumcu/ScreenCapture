using ScreenCapture.Capture;
using ScreenCapture.Forms;

namespace ScreenCapture;

/// <summary>
/// Red frames outside of a recording: a permanent one around the area the current mode will capture
/// (saved region, saved window, monitor), and a brief one after screenshots of interactively chosen areas.
/// Both are excluded from screen capture, so they never appear in images or videos.
/// </summary>
internal sealed partial class MainForm
{
    private const int AreaFrameIntervalMs = 200;
    private const int WindowResolveIntervalMs = 1000;
    private const int FlashDurationMs = 1500;

    private RecordingFrame? _areaFrame;
    private System.Windows.Forms.Timer? _areaFrameTimer;
    private Rectangle? _areaFrameBounds;      // what the permanent frame currently shows
    private WindowTarget? _areaFrameTarget;   // saved window the handle below was resolved for
    private nint _areaFrameWindow;
    private long _nextWindowResolve;

    private RecordingFrame? _flashFrame;
    private System.Windows.Forms.Timer? _flashTimer;

    // ---- Permanent frame around the saved area -----------------------------------------

    /// <summary>Re-evaluates the permanent frame now (mode, target or settings changed).</summary>
    private void RefreshAreaFrame()
    {
        if (_areaFrameTimer is null)
        {
            _areaFrameTimer = new System.Windows.Forms.Timer(_components) { Interval = AreaFrameIntervalMs };
            _areaFrameTimer.Tick += (_, _) => UpdateAreaFrame();
            _areaFrameTimer.Start();
        }

        _nextWindowResolve = 0;
        UpdateAreaFrame();
    }

    private void UpdateAreaFrame()
    {
        // While recording, the recording frame marks the area instead.
        Rectangle? area = _settings.ShowAreaFrame && !IsRecording && !_recordingTransition ? ResolveAreaFrameBounds() : null;
        _areaFrameBounds = area;

        if (area is not { } rect)
        {
            _areaFrame?.Conceal();
            return;
        }

        _areaFrame ??= new RecordingFrame(); // never Show()n: Track shows the window without activating it
        _areaFrame.Track(rect);
    }

    /// <summary>Screen area of the current mode's target, or null when there is nothing fixed to show.</summary>
    private Rectangle? ResolveAreaFrameBounds()
    {
        switch (_settings.Mode)
        {
            case CaptureMode.FullScreen:
                // Saved monitor, all monitors, or (by default) the monitor under the cursor.
                return CaptureController.DescribeTargetArea(CaptureMode.FullScreen, _settings.FullScreenTarget, null, null);

            case CaptureMode.Region:
                if (_settings.RegionTarget is not { } region)
                {
                    return null;
                }

                Rectangle visible = Rectangle.Intersect(region.ToRectangle(), ScreenCaptureService.VirtualScreen);
                return visible.Width > 0 && visible.Height > 0 ? visible : null;

            case CaptureMode.Window:
                return ResolveAreaFrameWindow();

            default:
                return null;
        }
    }

    /// <summary>
    /// Follows the saved window. The handle is cached; a full lookup (the window may have been reopened)
    /// runs at most once a second while it is not on screen.
    /// </summary>
    private Rectangle? ResolveAreaFrameWindow()
    {
        WindowTarget? target = _settings.WindowTarget;
        if (target is null)
        {
            _areaFrameTarget = null;
            _areaFrameWindow = 0;
            return null;
        }

        bool sameTarget = ReferenceEquals(target, _areaFrameTarget);
        if (!sameTarget || !WindowFinder.IsShownOnScreen(_areaFrameWindow))
        {
            long now = Environment.TickCount64;
            if (sameTarget && now < _nextWindowResolve)
            {
                return null;
            }

            _nextWindowResolve = now + WindowResolveIntervalMs;
            _areaFrameTarget = target;
            try
            {
                _areaFrameWindow = WindowFinder.ResolveTarget(target, WindowFinder.GetCapturableWindows()).Handle;
            }
            catch (CaptureTargetException)
            {
                _areaFrameWindow = 0; // closed or minimized: no frame until it is back
                return null;
            }
        }

        Rectangle bounds = WindowFinder.GetFrameBounds(_areaFrameWindow);
        return bounds.Width > 0 && bounds.Height > 0 ? bounds : null;
    }

    private void DisposeAreaFrames()
    {
        _areaFrameTimer?.Stop();
        _areaFrame?.Close();
        _areaFrame?.Dispose();
        _areaFrame = null;
        HideFlashFrame();
    }

    // ---- Brief frame for screenshots and newly saved areas -----------------------------

    /// <summary>
    /// Shows the red frame around <paramref name="area"/> for a moment as feedback for a screenshot.
    /// Skipped when the permanent frame already marks exactly that area.
    /// </summary>
    private void FlashAreaFrame(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0 || _areaFrameBounds == area)
        {
            return;
        }

        if (_flashFrame is null)
        {
            _flashFrame = new RecordingFrame();
            _flashTimer = new System.Windows.Forms.Timer(_components) { Interval = FlashDurationMs };
            _flashTimer.Tick += (_, _) => HideFlashFrame();
        }

        _flashFrame.Track(area);
        _flashTimer!.Stop();
        _flashTimer.Start(); // restart: a new capture extends the display
    }

    private void HideFlashFrame()
    {
        _flashTimer?.Stop();
        _flashFrame?.Close();
        _flashFrame?.Dispose();
        _flashFrame = null;
    }

    /// <summary>After Set Area… / a monitor choice: the permanent frame shows it, or a brief one if that is turned off.</summary>
    private void FlashSavedTarget()
    {
        if (_settings.ShowAreaFrame)
        {
            return;
        }

        Rectangle? area = CaptureController.DescribeTargetArea(
            _settings.Mode, _settings.FullScreenTarget, _settings.WindowTarget, _settings.RegionTarget);
        if (area is { } rect)
        {
            FlashAreaFrame(rect);
        }
    }
}
