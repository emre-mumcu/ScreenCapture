namespace ScreenCapture.Forms;

/// <summary>Colors and small control factories for a consistent, light look.</summary>
internal static class UiTheme
{
    public static readonly Color Accent = Color.FromArgb(0, 103, 192);
    public static readonly Color AccentHover = Color.FromArgb(0, 89, 168);
    public static readonly Color AccentPressed = Color.FromArgb(0, 75, 145);
    public static readonly Color Window = Color.White;
    public static readonly Color CardBack = Color.FromArgb(246, 248, 251);
    public static readonly Color Border = Color.FromArgb(218, 223, 230);
    public static readonly Color ButtonHover = Color.FromArgb(232, 238, 246);
    public static readonly Color SecondaryText = Color.FromArgb(96, 104, 115);
    public static readonly Color Error = Color.FromArgb(196, 43, 28);
    public static readonly Color Success = Color.FromArgb(16, 124, 16);

    public static Label SectionHeader(string text) => new()
    {
        Text = text.ToUpper(Localization.Strings.Culture),
        AutoSize = true,
        Font = UiFonts.SectionHeader,
        ForeColor = SecondaryText,
        Margin = new Padding(1, 10, 0, 4),
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
            FlatStyle = FlatStyle.Flat,
            BackColor = Window,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = ButtonHover;
        button.FlatAppearance.MouseDownBackColor = Border;
        return button;
    }

    /// <summary>Filled accent button for the primary action.</summary>
    public static Button PrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Font = UiFonts.Medium,
            ForeColor = Color.White,
            BackColor = Accent,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = Padding.Empty,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        button.FlatAppearance.MouseDownBackColor = AccentPressed;
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
            BackColor = Window,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        segment.FlatAppearance.BorderColor = Border;
        segment.FlatAppearance.CheckedBackColor = Accent;
        segment.FlatAppearance.MouseOverBackColor = ButtonHover;
        segment.CheckedChanged += (_, _) =>
        {
            segment.ForeColor = segment.Checked ? Color.White : SystemColors.ControlText;
            segment.Font = segment.Checked ? UiFonts.Bold : UiFonts.Normal;
        };
        return segment;
    }

    /// <summary>Light rounded-looking panel with a 1px border.</summary>
    public static TableLayoutPanel Card()
    {
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            BackColor = CardBack,
            Padding = new Padding(10, 8, 10, 8),
            Margin = Padding.Empty,
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };
        return card;
    }
}
