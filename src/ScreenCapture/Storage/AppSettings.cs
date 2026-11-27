using ScreenCapture.Capture;
using ScreenCapture.Hotkeys;
using ScreenCapture.Localization;

namespace ScreenCapture.Storage;

/// <summary>User settings persisted to settings.json.</summary>
internal sealed class AppSettings
{
    /// <summary>UI language; null on first start, then resolved from the Windows display language.</summary>
    public AppLanguage? Language { get; set; }

    /// <summary>Light (default, the original look), Dark, or follow the Windows app theme.</summary>
    public Forms.ThemeMode Theme { get; set; } = Forms.ThemeMode.Light;

    public CaptureMode Mode { get; set; } = CaptureMode.Region;

    /// <summary>Screenshot (PNG) or video recording (MP4).</summary>
    public CaptureOutput Output { get; set; } = CaptureOutput.Image;

    /// <summary>Video frames per second: 15, 24, 30 or 60.</summary>
    public int VideoFrameRate { get; set; } = 30;

    /// <summary>Record what the computer plays (WASAPI loopback of the default playback device).</summary>
    public bool RecordSystemAudio { get; set; } = true;

    /// <summary>Record the default microphone (mixed with system audio when both are on).</summary>
    public bool RecordMicrophone { get; set; }

    public bool VideoIncludeCursor { get; set; } = true;

    public HotkeyDefinition Hotkey { get; set; } = HotkeyDefinition.Default;

    /// <summary>When minimized, hide from the taskbar and live only in the notification area.</summary>
    public bool MinimizeToTray { get; set; }

    public bool AlwaysOnTop { get; set; }

    /// <summary>Saved full-screen target; null = monitor under the mouse cursor.</summary>
    public MonitorTarget? FullScreenTarget { get; set; }

    /// <summary>Saved window target; null = pick (or take the active window) at every capture.</summary>
    public WindowTarget? WindowTarget { get; set; }

    /// <summary>Saved region; null = draw a region at every capture.</summary>
    public RegionTarget? RegionTarget { get; set; }

    /// <summary>Window mode without a saved window captures the foreground window instead of showing the picker.</summary>
    public bool CaptureActiveWindowDirectly { get; set; }

    public bool IncludeCursor { get; set; }

    public bool PlaySound { get; set; } = true;

    public bool ShowNotification { get; set; }

    public bool CopyToClipboard { get; set; }

    /// <summary>Do not pass the mouse hotkey click to other applications (prevents e.g. browser "Back").</summary>
    public bool SwallowMouseHotkey { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public int? WindowLeft { get; set; }

    public int? WindowTop { get; set; }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
