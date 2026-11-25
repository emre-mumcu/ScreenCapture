namespace ScreenCapture.Hotkeys;

/// <summary>Listens globally for one shortcut. <see cref="Triggered"/> is raised on the UI thread.</summary>
internal interface IHotkeyListener : IDisposable
{
    event EventHandler? Triggered;

    /// <summary>Starts listening. Returns false with a user-facing message if the shortcut cannot be installed.</summary>
    bool Start(HotkeyDefinition hotkey, out string? error);

    void Stop();
}
