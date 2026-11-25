using Microsoft.Win32;
using ScreenCapture.Interop;

namespace ScreenCapture.Forms;

public enum ThemeMode
{
    Light,
    Dark,
    System,
}

/// <summary>Role of a control in the theme; stored in <see cref="Control.Tag"/> by the factories below.</summary>
internal enum ThemeRole
{
    SecondaryText,
    Card,
    SecondaryButton,
    PrimaryButton,
    Segment,
    Input,
}

/// <summary>All colors of one theme.</summary>
internal sealed record Palette(
    bool IsDark,
    Color Window,
    Color Text,
    Color SecondaryText,
    Color CardBack,
    Color Border,
    Color ButtonBack,
    Color ButtonHover,
    Color ButtonPressed,
    Color InputBack,
    Color Accent,
    Color AccentHover,
    Color AccentPressed,
    Color AccentText,
    Color Success,
    Color Warning,
    Color Error);

/// <summary>
/// Light and dark themes plus control factories. Controls carry a <see cref="ThemeRole"/> in their Tag,
/// so <see cref="Apply"/> can recolor an existing window when the theme changes at runtime.
/// </summary>
internal static class UiTheme
{
    /// <summary>The original light look of the application.</summary>
    public static readonly Palette LightPalette = new(
        IsDark: false,
        Window: Color.White,
        Text: SystemColors.ControlText,
        SecondaryText: Color.FromArgb(96, 104, 115),
        CardBack: Color.FromArgb(246, 248, 251),
        Border: Color.FromArgb(218, 223, 230),
        ButtonBack: Color.White,
        ButtonHover: Color.FromArgb(232, 238, 246),
        ButtonPressed: Color.FromArgb(218, 223, 230),
        InputBack: SystemColors.Window,
        Accent: Color.FromArgb(0, 103, 192),
        AccentHover: Color.FromArgb(0, 89, 168),
        AccentPressed: Color.FromArgb(0, 75, 145),
        AccentText: Color.FromArgb(0, 103, 192),
        Success: Color.FromArgb(16, 124, 16),
        Warning: Color.FromArgb(157, 93, 0),
        Error: Color.FromArgb(196, 43, 28));

    /// <summary>Dark counterpart with the same structure and accent.</summary>
    public static readonly Palette DarkPalette = new(
        IsDark: true,
        Window: Color.FromArgb(32, 32, 32),
        Text: Color.FromArgb(241, 241, 241),
        SecondaryText: Color.FromArgb(166, 170, 176),
        CardBack: Color.FromArgb(43, 43, 43),
        Border: Color.FromArgb(68, 68, 68),
        ButtonBack: Color.FromArgb(55, 55, 55),
        ButtonHover: Color.FromArgb(66, 66, 66),
        ButtonPressed: Color.FromArgb(78, 78, 78),
        InputBack: Color.FromArgb(45, 45, 45),
        Accent: Color.FromArgb(0, 103, 192),
        AccentHover: Color.FromArgb(0, 120, 215),
        AccentPressed: Color.FromArgb(0, 84, 158),
        AccentText: Color.FromArgb(96, 175, 255),
        Success: Color.FromArgb(108, 203, 95),
        Warning: Color.FromArgb(252, 185, 0),
        Error: Color.FromArgb(255, 128, 120));

    private static ThemeMode s_mode = ThemeMode.Light;
    private static bool s_systemWatch;

    public static event EventHandler? ThemeChanged;

    public static Palette Current { get; private set; } = LightPalette;

    /// <summary>Selected mode; <see cref="ThemeMode.System"/> follows the Windows app theme live.</summary>
    public static ThemeMode Mode
    {
        get => s_mode;
        set
        {
            s_mode = value;
            if (value == ThemeMode.System && !s_systemWatch)
            {
                s_systemWatch = true;
                SystemEvents.UserPreferenceChanged += (_, e) =>
                {
                    if (e.Category == UserPreferenceCategory.General && s_mode == ThemeMode.System)
                    {
                        Refresh();
                    }
                };
            }

            Refresh();
        }
    }

    // Shortcuts used throughout the UI code.
    public static Color Accent => Current.Accent;
    public static Color AccentText => Current.AccentText;
    public static Color Window => Current.Window;
    public static Color Text => Current.Text;
    public static Color SecondaryText => Current.SecondaryText;
    public static Color Border => Current.Border;
    public static Color Success => Current.Success;
    public static Color Warning => Current.Warning;
    public static Color Error => Current.Error;

    /// <summary>Windows "app mode": HKCU\...\Themes\Personalize\AppsUseLightTheme = 0 means dark.</summary>
    public static bool IsSystemDark()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Refresh()
    {
        bool dark = s_mode == ThemeMode.Dark || (s_mode == ThemeMode.System && IsSystemDark());
        Palette palette = dark ? DarkPalette : LightPalette;
        if (palette == Current)
        {
            return;
        }

        Current = palette;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    // ---- Applying a theme -------------------------------------------------------

    /// <summary>Recolors a form (or any control tree) for the current palette.</summary>
    public static void Apply(Control root)
    {
        ApplyTo(root);
        foreach (Control child in root.Controls)
        {
            Apply(child);
        }

        if (root is Form form)
        {
            ApplyTitleBar(form);
            form.Invalidate(invalidateChildren: true);
        }
    }

    /// <summary>Dark or light title bar (Windows 10 20H1+ / Windows 11).</summary>
    public static void ApplyTitleBar(Form form)
    {
        if (form.IsHandleCreated)
        {
            NativeMethods.SetDarkTitleBar(form.Handle, Current.IsDark);
        }
    }

    private static void ApplyTo(Control control)
    {
        Palette p = Current;
        switch (control.Tag)
        {
            case ThemeRole.SecondaryText:
                control.ForeColor = p.SecondaryText;
                return;
            case ThemeRole.Card:
                control.BackColor = p.CardBack;
                return;
            case ThemeRole.SecondaryButton when control is Button button:
                StyleSecondary(button);
                return;
            case ThemeRole.PrimaryButton when control is Button button:
                StylePrimary(button);
                return;
            case ThemeRole.Segment when control is RadioButton segment:
                StyleSegment(segment);
                return;
            case ThemeRole.Input:
                StyleInput(control);
                return;
        }

        switch (control)
        {
            case Form form:
                form.BackColor = p.Window;
                form.ForeColor = p.Text;
                break;
            case Button button:
                // Plain dialog buttons: system look in light mode, flat in dark mode.
                if (p.IsDark)
                {
                    StyleSecondary(button);
                }
                else
                {
                    button.FlatStyle = FlatStyle.Standard;
                    button.UseVisualStyleBackColor = true;
                    button.ForeColor = SystemColors.ControlText;
                }

                break;
            case ComboBox or TextBox:
                StyleInput(control);
                break;
            case LinkLabel link:
                link.LinkColor = p.AccentText;
                link.ActiveLinkColor = p.AccentHover;
                link.VisitedLinkColor = p.AccentText;
                break;
        }
    }

    private static void StyleSecondary(Button button)
    {
        Palette p = Current;
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = p.ButtonBack;
        button.ForeColor = p.Text;
        button.FlatAppearance.BorderColor = p.Border;
        button.FlatAppearance.MouseOverBackColor = p.ButtonHover;
        button.FlatAppearance.MouseDownBackColor = p.ButtonPressed;
    }

    private static void StylePrimary(Button button)
    {
        Palette p = Current;
        button.ForeColor = Color.White;
        button.BackColor = p.Accent;
        button.FlatAppearance.MouseOverBackColor = p.AccentHover;
        button.FlatAppearance.MouseDownBackColor = p.AccentPressed;
    }

    private static void StyleSegment(RadioButton segment)
    {
        Palette p = Current;
        segment.BackColor = p.ButtonBack;
        segment.ForeColor = segment.Checked ? Color.White : p.Text;
        segment.FlatAppearance.BorderColor = p.Border;
        segment.FlatAppearance.CheckedBackColor = p.Accent;
        segment.FlatAppearance.MouseOverBackColor = p.ButtonHover;
    }

    private static void StyleInput(Control control)
    {
        Palette p = Current;
        control.BackColor = p.InputBack;
        control.ForeColor = p.Text;
        if (control is ComboBox combo)
        {
            combo.FlatStyle = p.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
        }
    }

    /// <summary>Colors a context menu; dark menus use a custom renderer.</summary>
    public static void ApplyMenu(ToolStrip menu)
    {
        Palette p = Current;
        if (p.IsDark)
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false };
            menu.BackColor = p.CardBack;
            menu.ForeColor = p.Text;
        }
        else
        {
            menu.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            menu.ResetBackColor();
            menu.ResetForeColor();
        }

        foreach (ToolStripItem item in menu.Items)
        {
            if (p.IsDark)
            {
                item.ForeColor = p.Text;
            }
            else
            {
                item.ResetForeColor();
            }
        }
    }

    // ---- Factories ------------------------------------------------------------------

    public static Label SectionHeader(string text) => new()
    {
        Text = text.ToUpper(Localization.Strings.Culture),
        AutoSize = true,
        Font = UiFonts.SectionHeader,
        ForeColor = SecondaryText,
        Margin = new Padding(1, 10, 0, 4),
        Tag = ThemeRole.SecondaryText,
    };

    /// <summary>Flat secondary button.</summary>
    public static Button SecondaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(34, 30),
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(3, 0, 0, 0),
            Cursor = Cursors.Hand,
            Tag = ThemeRole.SecondaryButton,
        };
        StyleSecondary(button);
        return button;
    }

    /// <summary>Filled accent button for the primary action.</summary>
    public static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Font = UiFonts.Medium,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = Padding.Empty,
            UseVisualStyleBackColor = false,
            Tag = ThemeRole.PrimaryButton,
        };
        button.FlatAppearance.BorderSize = 0;
        StylePrimary(button);
        return button;
    }

    /// <summary>Segment of a segmented control (radio button rendered as a toggle button).</summary>
    public static RadioButton Segment(string text)
    {
        var segment = new RadioButton
        {
            Text = text,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Tag = ThemeRole.Segment,
        };
        StyleSegment(segment);
        segment.CheckedChanged += (_, _) =>
        {
            segment.ForeColor = segment.Checked ? Color.White : Current.Text;
            segment.Font = segment.Checked ? UiFonts.Bold : UiFonts.Normal;
        };
        return segment;
    }

    /// <summary>Panel with a slightly different background and a 1px border.</summary>
    public static TableLayoutPanel Card()
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            BackColor = Current.CardBack,
            Padding = new Padding(10, 8, 10, 8),
            Margin = Padding.Empty,
            Tag = ThemeRole.Card,
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Current.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        return card;
    }

    /// <summary>Menu colors for the dark theme.</summary>
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        private static Palette P => DarkPalette;

        public override Color ToolStripDropDownBackground => P.CardBack;
        public override Color ImageMarginGradientBegin => P.CardBack;
        public override Color ImageMarginGradientMiddle => P.CardBack;
        public override Color ImageMarginGradientEnd => P.CardBack;
        public override Color MenuBorder => P.Border;
        public override Color MenuItemBorder => P.ButtonHover;
        public override Color MenuItemSelected => P.ButtonHover;
        public override Color MenuItemSelectedGradientBegin => P.ButtonHover;
        public override Color MenuItemSelectedGradientEnd => P.ButtonHover;
        public override Color MenuItemPressedGradientBegin => P.ButtonPressed;
        public override Color MenuItemPressedGradientEnd => P.ButtonPressed;
        public override Color SeparatorDark => P.Border;
        public override Color SeparatorLight => P.Border;
        public override Color CheckBackground => P.Accent;
        public override Color CheckSelectedBackground => P.AccentHover;
        public override Color CheckPressedBackground => P.AccentPressed;
    }
}
