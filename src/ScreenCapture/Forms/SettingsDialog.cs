using ScreenCapture.Localization;
using ScreenCapture.Storage;

namespace ScreenCapture.Forms;

/// <summary>Secondary settings kept out of the compact main window, including the UI language.</summary>
internal sealed class SettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly ComboBox _language;
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

        Text = Strings.SettingsTitle;
        Font = UiFonts.Normal;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

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
        _language = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(3, 2, 3, 2),
        };
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            _language.Items.Add(Strings.NativeName(language));
        }

        _language.SelectedIndex = (int)(_settings.Language ?? Strings.Current);
        layout.Controls.Add(_language);

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
            MaximumSize = new Size(380, 0),
            Text = capturesDirectory,
            ForeColor = SystemColors.GrayText,
        });

        var okButton = new Button { Text = Strings.Ok, AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = Strings.Cancel, AutoSize = true, MinimumSize = new Size(80, 0), DialogResult = DialogResult.Cancel };
        okButton.Click += (_, _) => ReadBack();

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 12, 0, 0),
            Anchor = AnchorStyles.Right,
            MinimumSize = new Size(380, 0),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    /// <summary>The edited copy of the settings (valid when the dialog returns OK).</summary>
    public AppSettings Result => _settings;

    private void ReadBack()
    {
        _settings.Language = (AppLanguage)_language.SelectedIndex;
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
