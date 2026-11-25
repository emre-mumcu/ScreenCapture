using ScreenCapture.Interop;

namespace ScreenCapture.Hotkeys;

internal static class ModifierState
{
    /// <summary>
    /// Reads the currently pressed modifiers. Use <paramref name="asynchronous"/> = true inside
    /// low-level hooks (physical state) and false while processing window messages (message-synchronized state).
    /// </summary>
    public static HotkeyModifiers GetCurrent(bool asynchronous = true)
    {
        var result = HotkeyModifiers.None;
        if (IsDown(NativeMethods.VK_CONTROL, asynchronous)) result |= HotkeyModifiers.Control;
        if (IsDown(NativeMethods.VK_MENU, asynchronous)) result |= HotkeyModifiers.Alt;
        if (IsDown(NativeMethods.VK_SHIFT, asynchronous)) result |= HotkeyModifiers.Shift;
        if (IsDown(NativeMethods.VK_LWIN, asynchronous) || IsDown(NativeMethods.VK_RWIN, asynchronous)) result |= HotkeyModifiers.Win;
        return result;
    }

    private static bool IsDown(int vk, bool asynchronous) =>
        ((asynchronous ? NativeMethods.GetAsyncKeyState(vk) : NativeMethods.GetKeyState(vk)) & 0x8000) != 0;
}
