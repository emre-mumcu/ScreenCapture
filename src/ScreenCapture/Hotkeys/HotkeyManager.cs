using Microsoft.Win32;

namespace ScreenCapture.Hotkeys;

/// <summary>
/// Owns the active global shortcut listener. Supports temporarily suspending the shortcut
/// (e.g. while the user is recording a new one) and rolls back to the previous shortcut on failure.
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private readonly SynchronizationContext _uiContext;
    private IHotkeyListener? _listener;
    private int _suspendCount;
    private bool _disposed;

    public HotkeyManager()
    {
        _uiContext = SynchronizationContext.Current
                     ?? throw new InvalidOperationException("HotkeyManager must be created on the UI thread.");
    }

    public event EventHandler? Triggered;

    public HotkeyDefinition? Current { get; private set; }

    public bool SwallowMouseClicks { get; private set; } = true;

    public bool IsSuspended => _suspendCount > 0;

    /// <summary>Activates a new shortcut. On failure the previous shortcut stays active.</summary>
    public bool TryApply(HotkeyDefinition hotkey, bool swallowMouseClicks, out string? error)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        HotkeyValidation validation = hotkey.Validate();
        if (!validation.IsValid)
        {
            error = validation.Error;
            return false;
        }

        HotkeyDefinition? previous = Current;
        bool previousSwallow = SwallowMouseClicks;

        StopListener();
        Current = hotkey;
        SwallowMouseClicks = swallowMouseClicks;

        if (IsSuspended || StartListener(out error))
        {
            error = null;
            return true;
        }

        Current = previous;
        SwallowMouseClicks = previousSwallow;
        if (previous is not null && !IsSuspended)
        {
            StartListener(out _);
        }

        return false;
    }

    public void Suspend()
    {
        if (_suspendCount++ == 0)
        {
            StopListener();
        }
    }

    public void Resume()
    {
        if (_suspendCount == 0)
        {
            return;
        }

        if (--_suspendCount == 0 && Current is not null && !_disposed)
        {
            StartListener(out _);
        }
    }

    /// <summary>
    /// True if Windows 11 is set to open the Snipping Tool with PrintScreen
    /// (HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled).
    /// </summary>
    public static bool IsPrintScreenSnippingEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
            return key?.GetValue("PrintScreenKeyForSnippingEnabled") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopListener();
    }

    private bool StartListener(out string? error)
    {
        HotkeyDefinition hotkey = Current!;
        IHotkeyListener listener = hotkey.Kind == HotkeyKind.Mouse
            ? new MouseHotkeyListener(_uiContext, SwallowMouseClicks)
            : new KeyboardHotkeyListener(_uiContext);

        listener.Triggered += OnListenerTriggered;
        if (!listener.Start(hotkey, out error))
        {
            listener.Triggered -= OnListenerTriggered;
            listener.Dispose();
            return false;
        }

        _listener = listener;
        return true;
    }

    private void StopListener()
    {
        if (_listener is null)
        {
            return;
        }

        _listener.Triggered -= OnListenerTriggered;
        _listener.Dispose();
        _listener = null;
    }

    private void OnListenerTriggered(object? sender, EventArgs e)
    {
        // Ignore events posted by a listener that was replaced in the meantime.
        if (sender == _listener && !IsSuspended)
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }
    }
}
