using System.Runtime.InteropServices;
using ScreenCapture.Localization;
using ScreenCapture.Interop;
using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Hotkeys;

/// <summary>
/// Keyboard shortcut listener. Uses RegisterHotKey where possible; PrintScreen is handled with a
/// WH_KEYBOARD_LL hook because Windows 11 may route it to the Snipping Tool before RegisterHotKey sees it.
/// </summary>
internal sealed unsafe class KeyboardHotkeyListener : IHotkeyListener
{
    private const int HotkeyId = 0x5C01;

    // Only one low-level keyboard hook is active at a time; the static callback routes to it.
    private static KeyboardHotkeyListener? s_hookOwner;

    private readonly SynchronizationContext _uiContext;
    private HotkeyMessageWindow? _window;
    private nint _hook;
    private HotkeyDefinition? _hotkey;
    private bool _keyHeld;

    public KeyboardHotkeyListener(SynchronizationContext uiContext)
    {
        _uiContext = uiContext;
    }

    public event EventHandler? Triggered;

    public bool UsesLowLevelHook => _hook != 0;

    public bool Start(HotkeyDefinition hotkey, out string? error)
    {
        Stop();
        _hotkey = hotkey;

        if (hotkey.Key == Keys.PrintScreen)
        {
            return InstallHook(out error);
        }

        _window = new HotkeyMessageWindow(HotkeyId, RaiseTriggered);
        uint modifiers = (uint)hotkey.Modifiers | MOD_NOREPEAT;
        if (RegisterHotKey(_window.Handle, HotkeyId, modifiers, (uint)hotkey.Key))
        {
            error = null;
            return true;
        }

        int code = Marshal.GetLastPInvokeError();
        _window.DestroyHandle();
        _window = null;
        error = code == ERROR_HOTKEY_ALREADY_REGISTERED
            ? Strings.HotkeyInUse(hotkey.DisplayText)
            : Strings.HotkeyRegisterFailed(hotkey.DisplayText, code);
        return false;
    }

    public void Stop()
    {
        if (_window is not null)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _window.DestroyHandle();
            _window = null;
        }

        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        if (s_hookOwner == this)
        {
            s_hookOwner = null;
        }

        _keyHeld = false;
    }

    public void Dispose() => Stop();

    private bool InstallHook(out string? error)
    {
        s_hookOwner = this;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, &KeyboardProc, GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            int code = Marshal.GetLastPInvokeError();
            s_hookOwner = null;
            error = Strings.KeyboardHookFailed(code);
            return false;
        }

        error = null;
        return true;
    }

    private void RaiseTriggered() =>
        _uiContext.Post(_ => Triggered?.Invoke(this, EventArgs.Empty), null);

    /// <summary>Must return quickly: the real work is posted to the UI thread.</summary>
    [UnmanagedCallersOnly]
    private static nint KeyboardProc(int nCode, nint wParam, nint lParam)
    {
        KeyboardHotkeyListener? owner = s_hookOwner;
        try
        {
            if (nCode >= 0 && owner?._hotkey is { } hotkey)
            {
                var data = (KBDLLHOOKSTRUCT*)lParam;
                if (data->vkCode == (uint)hotkey.Key)
                {
                    int message = (int)wParam;
                    bool isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
                    bool isUp = message is WM_KEYUP or WM_SYSKEYUP;

                    if (isDown)
                    {
                        if (owner._keyHeld)
                        {
                            return 1; // auto-repeat of a handled key
                        }

                        if (ModifierState.GetCurrent() == hotkey.Modifiers)
                        {
                            owner._keyHeld = true;
                            owner.RaiseTriggered();
                            return 1; // swallow so the Snipping Tool / other apps do not react
                        }
                    }
                    else if (isUp && owner._keyHeld)
                    {
                        owner._keyHeld = false;
                        return 1;
                    }
                }
            }
        }
        catch
        {
            // Exceptions must never cross the native boundary.
        }

        return CallNextHookEx(owner?._hook ?? 0, nCode, wParam, lParam);
    }

    /// <summary>Message-only window that receives WM_HOTKEY.</summary>
    private sealed class HotkeyMessageWindow : NativeWindow
    {
        private readonly int _id;
        private readonly Action _callback;

        public HotkeyMessageWindow(int id, Action callback)
        {
            _id = id;
            _callback = callback;
            CreateHandle(new CreateParams { Parent = HWND_MESSAGE });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam == _id)
            {
                _callback();
                return;
            }

            base.WndProc(ref m);
        }
    }
}
