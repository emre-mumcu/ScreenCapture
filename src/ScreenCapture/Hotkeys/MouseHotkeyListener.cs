using System.Runtime.InteropServices;
using ScreenCapture.Localization;
using static ScreenCapture.Interop.NativeMethods;

namespace ScreenCapture.Hotkeys;

/// <summary>Mouse shortcut listener based on a WH_MOUSE_LL hook.</summary>
internal sealed unsafe class MouseHotkeyListener : IHotkeyListener
{
    private static MouseHotkeyListener? s_hookOwner;

    private readonly SynchronizationContext _uiContext;
    private readonly bool _swallowClicks;
    private nint _hook;
    private HotkeyDefinition? _hotkey;
    private bool _buttonHeld;

    public MouseHotkeyListener(SynchronizationContext uiContext, bool swallowClicks)
    {
        _uiContext = uiContext;
        _swallowClicks = swallowClicks;
    }

    public event EventHandler? Triggered;

    public bool Start(HotkeyDefinition hotkey, out string? error)
    {
        Stop();
        _hotkey = hotkey;
        s_hookOwner = this;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, &MouseProc, GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            int code = Marshal.GetLastPInvokeError();
            s_hookOwner = null;
            error = Strings.MouseHookFailed(code);
            return false;
        }

        error = null;
        return true;
    }

    public void Stop()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        if (s_hookOwner == this)
        {
            s_hookOwner = null;
        }

        _buttonHeld = false;
    }

    public void Dispose() => Stop();

    private void RaiseTriggered() =>
        _uiContext.Post(_ => Triggered?.Invoke(this, EventArgs.Empty), null);

    private static (MouseHotkeyButton Button, bool IsDown) Decode(int message, uint mouseData) => message switch
    {
        WM_LBUTTONDOWN => (MouseHotkeyButton.Left, true),
        WM_LBUTTONUP => (MouseHotkeyButton.Left, false),
        WM_RBUTTONDOWN => (MouseHotkeyButton.Right, true),
        WM_RBUTTONUP => (MouseHotkeyButton.Right, false),
        WM_MBUTTONDOWN => (MouseHotkeyButton.Middle, true),
        WM_MBUTTONUP => (MouseHotkeyButton.Middle, false),
        WM_XBUTTONDOWN or WM_XBUTTONUP => (
            (mouseData >> 16) switch
            {
                XBUTTON1 => MouseHotkeyButton.XButton1,
                XBUTTON2 => MouseHotkeyButton.XButton2,
                _ => MouseHotkeyButton.None,
            },
            message == WM_XBUTTONDOWN),
        _ => (MouseHotkeyButton.None, false),
    };

    /// <summary>
    /// Must return well within LowLevelHooksTimeout, otherwise Windows silently removes the hook.
    /// Only the matching is done here; the capture runs later on the UI thread.
    /// </summary>
    [UnmanagedCallersOnly]
    private static nint MouseProc(int nCode, nint wParam, nint lParam)
    {
        MouseHotkeyListener? owner = s_hookOwner;
        try
        {
            if (nCode >= 0 && owner?._hotkey is { } hotkey)
            {
                var data = (MSLLHOOKSTRUCT*)lParam;
                var (button, isDown) = Decode((int)wParam, data->mouseData);
                if (button != MouseHotkeyButton.None && button == hotkey.MouseButton)
                {
                    if (isDown && ModifierState.GetCurrent() == hotkey.Modifiers)
                    {
                        owner._buttonHeld = true;
                        owner.RaiseTriggered();
                        if (owner._swallowClicks)
                        {
                            return 1;
                        }
                    }
                    else if (!isDown && owner._buttonHeld)
                    {
                        // Swallow the matching "up" too, so no application sees a half click.
                        owner._buttonHeld = false;
                        if (owner._swallowClicks)
                        {
                            return 1;
                        }
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
}
