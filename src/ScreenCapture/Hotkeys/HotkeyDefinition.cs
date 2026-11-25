using System.Text;
using System.Text.Json.Serialization;
using ScreenCapture.Localization;

namespace ScreenCapture.Hotkeys;

public enum HotkeyKind
{
    Keyboard,
    Mouse,
}

public enum MouseHotkeyButton
{
    None,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2,
}

/// <summary>Modifier flags; values match the Win32 MOD_* constants used by RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

public readonly record struct HotkeyValidation(bool IsValid, string? Error, string? Warning)
{
    public static HotkeyValidation Ok(string? warning = null) => new(true, null, warning);

    public static HotkeyValidation Fail(string error) => new(false, error, null);
}

/// <summary>An immutable keyboard or mouse shortcut.</summary>
public sealed record HotkeyDefinition
{
    public HotkeyKind Kind { get; init; }

    public HotkeyModifiers Modifiers { get; init; }

    /// <summary>Virtual key (keyboard shortcuts only).</summary>
    public Keys Key { get; init; }

    /// <summary>Mouse button (mouse shortcuts only).</summary>
    public MouseHotkeyButton MouseButton { get; init; }

    public static HotkeyDefinition Default { get; } = ForKey(Keys.S, HotkeyModifiers.Control | HotkeyModifiers.Shift);

    public static HotkeyDefinition ForKey(Keys key, HotkeyModifiers modifiers) =>
        new() { Kind = HotkeyKind.Keyboard, Key = key & Keys.KeyCode, Modifiers = modifiers };

    public static HotkeyDefinition ForMouse(MouseHotkeyButton button, HotkeyModifiers modifiers) =>
        new() { Kind = HotkeyKind.Mouse, MouseButton = button, Modifiers = modifiers };

    /// <summary>Keys that may be used without a modifier.</summary>
    [JsonIgnore]
    public bool IsStandaloneKey => Key is Keys.PrintScreen or Keys.Pause or Keys.Scroll or (>= Keys.F1 and <= Keys.F24);

    public static bool IsModifierKey(Keys key) => (key & Keys.KeyCode) is
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.LWin or Keys.RWin;

    public HotkeyValidation Validate()
    {
        if (Kind == HotkeyKind.Keyboard)
        {
            if (Key == Keys.None || IsModifierKey(Key))
            {
                return HotkeyValidation.Fail(Strings.HotkeyErrorNoKey);
            }

            if (Modifiers == HotkeyModifiers.None && !IsStandaloneKey)
            {
                return HotkeyValidation.Fail(Strings.HotkeyErrorNeedsModifier);
            }

            if (Modifiers == HotkeyModifiers.Shift && !IsStandaloneKey && IsTypingKey(Key))
            {
                return HotkeyValidation.Fail(Strings.HotkeyErrorShiftTyping);
            }

            return HotkeyValidation.Ok();
        }

        return MouseButton switch
        {
            MouseHotkeyButton.None => HotkeyValidation.Fail(Strings.HotkeyErrorNoMouseButton),
            MouseHotkeyButton.Left or MouseHotkeyButton.Right when Modifiers == HotkeyModifiers.None =>
                HotkeyValidation.Fail(Strings.HotkeyErrorPlainClick),
            MouseHotkeyButton.Left => HotkeyValidation.Ok(Strings.HotkeyWarningLeftClick),
            MouseHotkeyButton.Right => HotkeyValidation.Ok(Strings.HotkeyWarningRightClick),
            _ => HotkeyValidation.Ok(),
        };
    }

    /// <summary>Shortcut text in the current UI language (e.g. "Ctrl+Shift+S", "Strg+Umschalt+S").</summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var sb = new StringBuilder();
            if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append(Strings.ModCtrl).Append('+');
            if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append(Strings.ModShift).Append('+');
            if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
            sb.Append(Kind == HotkeyKind.Keyboard ? GetKeyName(Key) : GetMouseButtonName(MouseButton));
            return sb.ToString();
        }
    }

    public override string ToString() => DisplayText;

    public static string GetModifiersText(HotkeyModifiers modifiers) =>
        ForKey(Keys.None, modifiers).DisplayText;

    private static bool IsTypingKey(Keys key) =>
        key is (>= Keys.A and <= Keys.Z) or (>= Keys.D0 and <= Keys.D9) or (>= Keys.NumPad0 and <= Keys.Divide)
            or Keys.Space or (>= Keys.OemSemicolon and <= Keys.Oemtilde) or (>= Keys.OemOpenBrackets and <= Keys.OemBackslash);

    public static string GetKeyName(Keys key) => key switch
    {
        Keys.None => string.Empty,
        Keys.PrintScreen => Strings.KeyPrintScreen,
        Keys.Scroll => "ScrollLock",
        Keys.Pause => "Pause",
        Keys.Return => "Enter",
        Keys.Back => "Backspace",
        Keys.Escape => "Esc",
        Keys.Space => Strings.KeySpace,
        Keys.Prior => "PageUp",
        Keys.Next => "PageDown",
        Keys.Insert => Strings.KeyInsert,
        Keys.Delete => Strings.KeyDelete,
        Keys.Capital => "CapsLock",
        Keys.Apps => Strings.KeyMenu,
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (key - Keys.NumPad0),
        Keys.Multiply => "Num *",
        Keys.Add => "Num +",
        Keys.Subtract => "Num -",
        Keys.Decimal => "Num .",
        Keys.Divide => "Num /",
        _ => key.ToString(),
    };

    public static string GetMouseButtonName(MouseHotkeyButton button) => button switch
    {
        MouseHotkeyButton.Left => Strings.MouseLeft,
        MouseHotkeyButton.Right => Strings.MouseRight,
        MouseHotkeyButton.Middle => Strings.MouseMiddle,
        MouseHotkeyButton.XButton1 => Strings.MouseX1,
        MouseHotkeyButton.XButton2 => Strings.MouseX2,
        _ => string.Empty,
    };
}
