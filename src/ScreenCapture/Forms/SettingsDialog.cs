using ScreenCapture.Localization;
using ScreenCapture.Storage;

namespace ScreenCapture.Forms;

/// <summary>Secondary settings kept out of the compact main window: language, appearance and behavior.</summary>
internal sealed class SettingsDialog : ThemedDialog
{
    private const int ContentWidth = 380;

    private readonly AppSettings _settings;
    private readonly ComboBox _language;
    private readonly ComboBox _theme;
    private readonly CheckBox _minimizeToTray;
    private readonly CheckBox _alwaysOnTop;
    private readonly CheckBox _activeWindow;
    private readonly CheckBox _includeCursor;
    private readonly CheckBox _playSound;
    private readonly CheckBox _showNotification;
    private readonly CheckBox _copyToClipboard;
    private readonly CheckBox _swallowMouse;
    private readonly CheckBox _startWithWindows;

    public SettingsDialog(AppSettings settings, string capturesDirectory)
    {
        _settings = settings.Clone();
        BeginBuild(Strings.SettingsTitle);

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };

        // Language names are shown in their own language so anyone can find theirs.
        AddHeader(layout, Strings.Current == AppLanguage.English ? "Language" : Strings.SettingsLanguage + " / Language");
        _language = AddCombo(layout, Enum.GetValues<AppLanguage>().Select(Strings.NativeName),
            (int)(_settings.Language ?? Strings.Current));

        AddHeader(layout, Strings.SettingsTheme);
        _theme = AddCombo(layout, [Strings.ThemeLight, Strings.ThemeDark, Strings.ThemeSystem], (int)_settings.Theme);

        AddHeader(layout, Strings.SettingsGeneral);
        _minimizeToTray = AddCheck(layout, Strings.SettingsMinimizeToTray, _settings.MinimizeToTray);
        _alwaysOnTop = AddCheck(layout, Strings.SettingsAlwaysOnTop, _settings.AlwaysOnTop);
        _startWithWindows = AddCheck(layout, Strings.SettingsStartWithWindows, _settings.StartWithWindows);

        AddHeader(layout, Strings.SettingsCapture);
        _activeWindow = AddCheck(layout, Strings.SettingsActiveWindow, _settings.CaptureActiveWindowDirectly);
        _includeCursor = AddCheck(layout, Strings.SettingsIncludeCursor, _settings.IncludeCursor);
        _copyToClipboard = AddCheck(layout, Strings.SettingsCopyToClipboard, _settings.CopyToClipboard);

        AddHeader(layout, Strings.SettingsFeedback);
        _playSound = AddCheck(layout, Strings.SettingsPlaySound, _settings.PlaySound);
        _showNotification = AddCheck(layout, Strings.SettingsShowNotification, _settings.ShowNotification);
        _swallowMouse = AddCheck(layout, Strings.SettingsSwallowMouse, _settings.SwallowMouseHotkey);

        AddHeader(layout, Strings.SettingsCapturesFolder);
        layout.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(ContentWidth, 0),
            Text = capturesDirectory,
            Tag = ThemeRole.SecondaryText,
        });

        layout.Controls.Add(CreateButtonRow());
        Controls.Add(layout);

        EndBuild();
    }

    /// <summary>The edited copy of the settings (valid when the dialog returns OK).</summary>
    public AppSettings Result => _settings;

    private TableLayoutPanel CreateButtonRow()
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Margin = new Padding(0, 14, 0, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth - 2 * 86));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var aboutButton = new Button { Text = Strings.AboutButton, AutoSize = true, MinimumSize = new Size(80, 28), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        aboutButton.Click += (_, _) =>
        {
            using var about = new AboutDialog();
            about.ShowDialog(this);
        };

        var okButton = new Button { Text = Strings.Ok, AutoSize = true, MinimumSize = new Size(80, 28), DialogResult = DialogResult.OK, Margin = new Padding(6, 0, 0, 0) };
        var cancelButton = new Button { Text = Strings.Cancel, AutoSize = true, MinimumSize = new Size(80, 28), DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 0, 0) };
        okButton.Click += (_, _) => ReadBack();

        row.Controls.Add(aboutButton, 0, 0);
        row.Controls.Add(okButton, 1, 0);
        row.Controls.Add(cancelButton, 2, 0);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return row;
    }

    private void ReadBack()
    {
        _settings.Language = (AppLanguage)_language.SelectedIndex;
        _settings.Theme = (ThemeMode)_theme.SelectedIndex;
        _settings.MinimizeToTray = _minimizeToTray.Checked;
        _settings.AlwaysOnTop = _alwaysOnTop.Checked;
        _settings.StartWithWindows = _startWithWindows.Checked;
        _settings.CaptureActiveWindowDirectly = _activeWindow.Checked;
        _settings.IncludeCursor = _includeCursor.Checked;
        _settings.CopyToClipboard = _copyToClipboard.Checked;
        _settings.PlaySound = _playSound.Checked;
        _settings.ShowNotification = _showNotification.Checked;
        _settings.SwallowMouseHotkey = _swallowMouse.Checked;
    }

    private static ComboBox AddCombo(Control parent, IEnumerable<string> items, int selectedIndex)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(3, 2, 3, 2),
        };
        foreach (string item in items)
        {
            combo.Items.Add(item);
        }

        combo.SelectedIndex = Math.Clamp(selectedIndex, 0, combo.Items.Count - 1);
        parent.Controls.Add(combo);
        return combo;
    }

    private static CheckBox AddCheck(Control parent, string text, bool value)
    {
        var check = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
        parent.Controls.Add(check);
        return check;
    }

    private static void AddHeader(Control parent, string text)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = UiFonts.Bold,
            Margin = new Padding(0, 10, 0, 2),
        });
    }
}
