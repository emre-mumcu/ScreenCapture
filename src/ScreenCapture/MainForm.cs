using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using ScreenCapture.Capture;
using ScreenCapture.Forms;
using ScreenCapture.Hotkeys;
using ScreenCapture.Interop;
using ScreenCapture.Localization;
using ScreenCapture.Storage;

namespace ScreenCapture;

/// <summary>
/// Main window plus notification-area icon. Coordinates hotkeys, capture targets, captures and saving.
/// The content is built in code and rebuilt when the UI language changes.
/// </summary>
internal sealed partial class MainForm : Form
{
    /// <summary>Delay for captures started from a menu, so the closing menu is not in the image.</summary>
    private const int MenuCaptureDelayMs = 250;

    /// <summary>Logical (96 DPI) width of the content column.</summary>
    private const int ContentWidth = 360;

    private readonly IContainer _components = new Container();
    private readonly CaptureController _captureController = new();
    private readonly CaptureSaver _saver = new();
    private readonly HotkeyManager _hotkeys;
    private readonly Icon _appIcon = AppIcon.Create();
    private readonly Icon _trayIcon = AppIcon.CreateSmall();
    private readonly uint _showInstanceMessage = NativeMethods.RegisterWindowMessage(Program.ShowInstanceMessageName);
    private readonly Dictionary<CaptureMode, RadioButton> _modeButtons = [];
    private readonly Dictionary<CaptureOutput, RadioButton> _outputButtons = [];
    private readonly ToolTip _toolTip;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _monitorMenu;

    private AppSettings _settings;
    private Label _outputHeader = null!;
    private Label _modeHeader = null!;
    private Label _areaHeader = null!;
    private Label _hotkeyHeader = null!;
    private Button _changeHotkeyButton = null!;
    private Button _captureButton = null!;
    private Button _folderButton = null!;
    private Button _settingsButton = null!;
    private Label _targetLabel = null!;
    private Label _targetHintLabel = null!;
    private Button _defineTargetButton = null!;
    private Button _clearTargetButton = null!;
    private Label _hotkeyLabel = null!;
    private Label _hotkeyKindLabel = null!;
    private Label _statusLabel = null!;

    private bool _startHidden;
    private bool _hotkeyInitialized;
    private bool _excludedFromCapture;
    private bool _isBusy;
    private bool _hotkeyDialogOpen;
    private string? _lastSavedPath;
    private StatusKind _statusKind;

    public MainForm(AppSettings settings, bool startMinimized)
    {
        _settings = settings;
        _settings.StartWithWindows = StartupRegistration.IsEnabled();

        _toolTip = new ToolTip(_components);
        _monitorMenu = new ContextMenuStrip(_components);
        _tray = new NotifyIcon(_components) { Icon = _trayIcon, Text = "ScreenCapture", Visible = true };
        _tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                RestoreWindow();
            }
        };

        // Everything inside one layout suspension, so DPI auto-scaling sees all controls.
        SuspendLayout();
        InitializeWindow();
        BuildContent();
        UiTheme.Apply(this);
        ResumeLayout(performLayout: true);
        BuildTrayMenu();

        _hotkeys = new HotkeyManager();
        _hotkeys.Triggered += (_, _) => RunPrimaryAction(delayMs: 0);

        ApplyWindowPosition();
        TopMost = _settings.AlwaysOnTop;
        Strings.LanguageChanged += OnLanguageChanged;
        UiTheme.ThemeChanged += OnThemeChanged;

        if (startMinimized)
        {
            if (_settings.MinimizeToTray)
            {
                _startHidden = true;
            }
            else
            {
                WindowState = FormWindowState.Minimized;
            }
        }
    }

    // ---- Construction -------------------------------------------------------

    private void InitializeWindow()
    {
        Text = "ScreenCapture";
        Icon = _appIcon;
        Font = UiFonts.Normal;
        BackColor = UiTheme.Window;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12, 4, 12, 10);
        StartPosition = FormStartPosition.CenterScreen;
    }

    /// <summary>Creates the controls once; texts come from <see cref="ApplyTexts"/>.</summary>
    private void BuildContent()
    {
        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth));

        _outputHeader = UiTheme.SectionHeader(string.Empty);
        _modeHeader = UiTheme.SectionHeader(string.Empty);
        _areaHeader = UiTheme.SectionHeader(string.Empty);
        _hotkeyHeader = UiTheme.SectionHeader(string.Empty);

        root.Controls.Add(_outputHeader);
        root.Controls.Add(CreateOutputSelector());
        root.Controls.Add(_modeHeader);
        root.Controls.Add(CreateModeSelector());
        root.Controls.Add(_areaHeader);
        root.Controls.Add(CreateTargetCard());
        root.Controls.Add(_hotkeyHeader);
        root.Controls.Add(CreateHotkeyCard());
        root.Controls.Add(CreateActionRow());

        _statusLabel = new Label
        {
            AutoSize = false,
            Size = new Size(10, 20),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = UiFonts.Small,
            ForeColor = UiTheme.SecondaryText,
            Margin = new Padding(1, 6, 0, 0),
        };
        _statusLabel.Click += (_, _) => ShowLastCaptureInExplorer();
        root.Controls.Add(_statusLabel);

        Controls.Add(root);

        ApplyTexts();
        SetStatus(Strings.Ready);
        _modeButtons[_settings.Mode].Checked = true;
        _outputButtons[Enum.IsDefined(_settings.Output) ? _settings.Output : CaptureOutput.Image].Checked = true;
    }

    /// <summary>Sets every static text and tooltip in the current language.</summary>
    private void ApplyTexts()
    {
        _outputHeader.Text = Strings.SectionOutput.ToUpper(Strings.Culture);
        _modeHeader.Text = Strings.SectionMode.ToUpper(Strings.Culture);

        foreach ((CaptureOutput output, RadioButton segment) in _outputButtons)
        {
            segment.Text = output == CaptureOutput.Video ? "🎬  " + Strings.OutputVideo : "📷  " + Strings.OutputImage;
            _toolTip.SetToolTip(segment, output == CaptureOutput.Video ? Strings.OutputVideoTip : Strings.OutputImageTip);
        }
        _areaHeader.Text = Strings.SectionArea.ToUpper(Strings.Culture);
        _hotkeyHeader.Text = Strings.SectionHotkey.ToUpper(Strings.Culture);

        foreach ((CaptureMode mode, RadioButton segment) in _modeButtons)
        {
            segment.Text = mode.DisplayName();
            _toolTip.SetToolTip(segment, mode switch
            {
                CaptureMode.FullScreen => Strings.ModeFullScreenTip,
                CaptureMode.Window => Strings.ModeWindowTip,
                _ => Strings.ModeRegionTip,
            });
        }

        _defineTargetButton.Text = Strings.DefineArea;
        _toolTip.SetToolTip(_defineTargetButton, Strings.DefineAreaTip);
        _clearTargetButton.Text = Strings.ClearArea;
        _toolTip.SetToolTip(_clearTargetButton, Strings.ClearAreaTip);
        _changeHotkeyButton.Text = Strings.ChangeHotkey;
        _toolTip.SetToolTip(_changeHotkeyButton, Strings.ChangeHotkeyTip);
        UpdateCaptureButton();
        _folderButton.Text = Strings.FolderButton;
        _toolTip.SetToolTip(_folderButton, Strings.FolderButtonTip);
        _toolTip.SetToolTip(_settingsButton, Strings.SettingsTip);

        UpdateTargetDisplay();
        if (_hotkeyInitialized)
        {
            UpdateHotkeyDisplay();
        }
    }

    private TableLayoutPanel CreateModeSelector()
    {
        var segments = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Height = 34,
            Margin = Padding.Empty,
        };

        foreach (CaptureMode mode in Enum.GetValues<CaptureMode>())
        {
            segments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            RadioButton segment = UiTheme.Segment(mode.DisplayName());
            segment.CheckedChanged += (_, _) =>
            {
                if (segment.Checked)
                {
                    OnModeSelected(mode);
                }
            };
            _toolTip.SetToolTip(segment, mode switch
            {
                CaptureMode.FullScreen => Strings.ModeFullScreenTip,
                CaptureMode.Window => Strings.ModeWindowTip,
                _ => Strings.ModeRegionTip,
            });
            _modeButtons[mode] = segment;
            segments.Controls.Add(segment);
        }

        return segments;
    }

    private TableLayoutPanel CreateTargetCard()
    {
        TableLayoutPanel card = UiTheme.Card();
        card.ColumnCount = 1;
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        _targetLabel = new Label
        {
            AutoSize = false,
            Size = new Size(10, 22),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = UiFonts.Bold,
            Margin = Padding.Empty,
        };

        _targetHintLabel = new Label
        {
            AutoSize = false,
            Size = new Size(10, 18),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = UiFonts.Small,
            ForeColor = UiTheme.SecondaryText,
            Tag = ThemeRole.SecondaryText,
            Margin = Padding.Empty,
        };

        _defineTargetButton = UiTheme.SecondaryButton(Strings.DefineArea);
        _defineTargetButton.Margin = Padding.Empty;
        _defineTargetButton.Click += (_, _) => _ = DefineTargetAsync();
        _toolTip.SetToolTip(_defineTargetButton, Strings.DefineAreaTip);

        _clearTargetButton = UiTheme.SecondaryButton(Strings.ClearArea);
        _clearTargetButton.Margin = new Padding(6, 0, 0, 0);
        _clearTargetButton.Click += (_, _) => ClearTarget();
        _toolTip.SetToolTip(_clearTargetButton, Strings.ClearAreaTip);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0),
            Anchor = AnchorStyles.Left,
        };
        buttons.Controls.Add(_defineTargetButton);
        buttons.Controls.Add(_clearTargetButton);

        card.Controls.Add(_targetLabel, 0, 0);
        card.Controls.Add(_targetHintLabel, 0, 1);
        card.Controls.Add(buttons, 0, 2);
        return card;
    }

    private TableLayoutPanel CreateHotkeyCard()
    {
        TableLayoutPanel card = UiTheme.Card();
        card.ColumnCount = 2;
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _hotkeyLabel = new Label
        {
            AutoSize = false,
            Size = new Size(10, 24),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = UiFonts.Medium,
            Margin = Padding.Empty,
        };

        _hotkeyKindLabel = new Label
        {
            AutoSize = false,
            Size = new Size(10, 18),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = UiFonts.Small,
            ForeColor = UiTheme.SecondaryText,
            Tag = ThemeRole.SecondaryText,
            Margin = Padding.Empty,
        };

        _changeHotkeyButton = UiTheme.SecondaryButton(Strings.ChangeHotkey);
        _changeHotkeyButton.Anchor = AnchorStyles.Right;
        _changeHotkeyButton.Margin = new Padding(8, 0, 0, 0);
        _changeHotkeyButton.Click += (_, _) => ShowHotkeyDialog();
        _toolTip.SetToolTip(_changeHotkeyButton, Strings.ChangeHotkeyTip);

        card.Controls.Add(_hotkeyLabel, 0, 0);
        card.Controls.Add(_changeHotkeyButton, 1, 0);
        card.SetRowSpan(_changeHotkeyButton, 2);
        card.Controls.Add(_hotkeyKindLabel, 0, 1);
        return card;
    }

    private TableLayoutPanel CreateActionRow()
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Height = 40,
            Margin = new Padding(0, 14, 0, 0),
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _captureButton = UiTheme.PrimaryButton(Strings.CaptureButton);
        _captureButton.Dock = DockStyle.Fill;
        _captureButton.Click += (_, _) => RunPrimaryAction(delayMs: 0);
        _toolTip.SetToolTip(_captureButton, Strings.CaptureButtonTip);

        _folderButton = UiTheme.SecondaryButton(Strings.FolderButton);
        _folderButton.Dock = DockStyle.Fill;
        _folderButton.Margin = new Padding(6, 0, 0, 0);
        _folderButton.Click += (_, _) => OpenCapturesFolder();
        _toolTip.SetToolTip(_folderButton, Strings.FolderButtonTip);

        _settingsButton = UiTheme.SecondaryButton("⚙");
        _settingsButton.Dock = DockStyle.Fill;
        _settingsButton.MinimumSize = new Size(40, 30);
        _settingsButton.Margin = new Padding(6, 0, 0, 0);
        _settingsButton.Click += (_, _) => ShowSettingsDialog();
        _toolTip.SetToolTip(_settingsButton, Strings.SettingsTip);

        row.Controls.Add(_captureButton, 0, 0);
        row.Controls.Add(_folderButton, 1, 0);
        row.Controls.Add(_settingsButton, 2, 0);
        return row;
    }

    private void BuildTrayMenu()
    {
        var menu = new ContextMenuStrip(_components);
        menu.Items.Add(new ToolStripMenuItem(Strings.TrayShow, null, (_, _) => RestoreWindow()) { Font = UiFonts.Bold });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayCaptureSelected, null, (_, _) => _ = RunCaptureAsync(_settings.Mode, MenuCaptureDelayMs));
        menu.Items.Add(Strings.TrayCaptureFullScreen, null, (_, _) => _ = RunCaptureAsync(CaptureMode.FullScreen, MenuCaptureDelayMs));
        menu.Items.Add(Strings.TrayCaptureWindow, null, (_, _) => _ = RunCaptureAsync(CaptureMode.Window, MenuCaptureDelayMs));
        menu.Items.Add(Strings.TrayCaptureRegion, null, (_, _) => _ = RunCaptureAsync(CaptureMode.Region, MenuCaptureDelayMs));
        menu.Items.Add(new ToolStripSeparator());
        var recordItem = new ToolStripMenuItem(Strings.TrayStartRecording, null, (_, _) => _ = ToggleRecordingAsync(MenuCaptureDelayMs));
        menu.Items.Add(recordItem);
        menu.Opening += (_, _) => recordItem.Text = IsRecording ? Strings.TrayStopRecording : Strings.TrayStartRecording;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayOpenFolder, null, (_, _) => OpenCapturesFolder());
        menu.Items.Add(Strings.TraySettings, null, (_, _) => ShowSettingsDialog());
        menu.Items.Add(Strings.TrayAbout, null, (_, _) => ShowAboutDialog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.TrayExit, null, (_, _) => Close());
        UiTheme.ApplyMenu(menu);

        ContextMenuStrip? previous = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = menu;
        if (previous is not null)
        {
            _components.Remove(previous);
            previous.Dispose();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Only texts change; controls stay, so DPI scaling and state are untouched.
        SuspendLayout();
        ApplyTexts();
        SetStatus(IsRecording ? Strings.StatusRecording : IdleStatusText());
        ResumeLayout(performLayout: true);
        BuildTrayMenu();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        UiTheme.Apply(this);
        UpdateTargetDisplay();
        UpdateCaptureButton();
        SetStatus(_statusLabel.Text, _statusKind);
        BuildTrayMenu();
    }

    private void ShowAboutDialog()
    {
        RestoreWindow();
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }

    private void ApplyWindowPosition()
    {
        if (_settings.WindowLeft is not int left || _settings.WindowTop is not int top)
        {
            return;
        }

        // Only restore if the title bar would be on a connected monitor.
        var probe = new Point(left + 40, top + 10);
        if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(probe)))
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(left, top);
        }
    }

    private void InitializeHotkey()
    {
        if (!_hotkeys.TryApply(_settings.Hotkey, _settings.SwallowMouseHotkey, out string? error))
        {
            string message = Strings.HotkeyActivationFailed(_settings.Hotkey.DisplayText, error);
            HotkeyDefinition fallback = HotkeyDefinition.Default;
            if (_settings.Hotkey != fallback && _hotkeys.TryApply(fallback, _settings.SwallowMouseHotkey, out _))
            {
                _settings.Hotkey = fallback;
                message += Strings.UsingDefaultHotkey(fallback.DisplayText);
            }
            else
            {
                message += Strings.ChooseNewHotkey;
            }

            BeginInvoke(() => ShowNotice(message, ToolTipIcon.Warning));
        }

        if (_saver.IsFallback)
        {
            BeginInvoke(() => ShowNotice(Strings.CapturesFallback(_saver.Directory), ToolTipIcon.Info));
        }

        UpdateHotkeyDisplay();
    }

    // ---- Window behaviour ---------------------------------------------------

    protected override void SetVisibleCore(bool value)
    {
        // Started with --minimized and tray mode: create the handle but never show the window.
        if (_startHidden)
        {
            _startHidden = false;
            if (!IsHandleCreated)
            {
                CreateHandle();
            }

            value = false;
        }

        base.SetVisibleCore(value);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Keep this window out of every screenshot (Windows 10 2004+).
        _excludedFromCapture = NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        UiTheme.ApplyTitleBar(this);
#if DEBUG
        // Diagnostics only: allow taking screenshots of the UI itself.
        if (Environment.GetEnvironmentVariable("SCREENCAPTURE_UI_VISIBLE") == "1")
        {
            NativeMethods.SetWindowDisplayAffinity(Handle, 0);
            _excludedFromCapture = false;
        }

        // Diagnostics only: switch the language shortly after start to test live re-translation.
        if (Enum.TryParse(Environment.GetEnvironmentVariable("SCREENCAPTURE_TEST_SWITCH_LANGUAGE"), out AppLanguage testLanguage))
        {
            var timer = new System.Windows.Forms.Timer(_components) { Interval = 1500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Strings.Current = testLanguage;
            };
            timer.Start();
        }

        // Diagnostics only: switch the theme shortly after start to test live recoloring.
        if (Enum.TryParse(Environment.GetEnvironmentVariable("SCREENCAPTURE_TEST_SWITCH_THEME"), out ThemeMode testTheme))
        {
            var themeTimer = new System.Windows.Forms.Timer(_components) { Interval = 1500 };
            themeTimer.Tick += (_, _) =>
            {
                themeTimer.Stop();
                UiTheme.Mode = testTheme;
            };
            themeTimer.Start();
        }

        // Diagnostics only: open a dialog after start so it can be inspected.
        string? testDialog = Environment.GetEnvironmentVariable("SCREENCAPTURE_TEST_DIALOG");
        if (!string.IsNullOrEmpty(testDialog))
        {
            var dialogTimer = new System.Windows.Forms.Timer(_components) { Interval = 1000 };
            dialogTimer.Tick += (_, _) =>
            {
                dialogTimer.Stop();
                switch (testDialog)
                {
                    case "About": ShowAboutDialog(); break;
                    case "Settings": ShowSettingsDialog(); break;
                    case "Hotkey": ShowHotkeyDialog(); break;
                }
            };
            dialogTimer.Start();
        }
#endif

        if (!_hotkeyInitialized)
        {
            _hotkeyInitialized = true;
            InitializeHotkey();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && _settings.MinimizeToTray && Visible)
        {
            Hide();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (_showInstanceMessage != 0 && (uint)m.Msg == _showInstanceMessage)
        {
            RestoreWindow();
            return;
        }

        base.WndProc(ref m);
    }

    private void RestoreWindow()
    {
        if (!Visible)
        {
            Show();
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        NativeMethods.ForceForeground(Handle);
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel)
        {
            return;
        }

        // A running recording is finished and saved first, then the window closes again.
        if (IsRecording || _recordingTransition)
        {
            e.Cancel = true;
            if (IsRecording && !_recordingTransition)
            {
                _ = StopRecordingAsync(closeAfterwards: true);
            }

            return;
        }

        Point location = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
        _settings.WindowLeft = location.X;
        _settings.WindowTop = location.Y;
        SettingsStore.Save(_settings);

        // Remove global hooks / hotkey registrations before the message loop ends.
        _hotkeys.Dispose();
        _tray.Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Strings.LanguageChanged -= OnLanguageChanged;
            UiTheme.ThemeChanged -= OnThemeChanged;
            _trayIcon.Dispose();
            _hotkeys.Dispose();
            _components.Dispose();
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    // ---- Mode and capture target ---------------------------------------------

    private void OnModeSelected(CaptureMode mode)
    {
        if (_settings.Mode != mode)
        {
            _settings.Mode = mode;
            SettingsStore.Save(_settings);
        }

        UpdateTargetDisplay();
    }

    private bool HasTarget(CaptureMode mode) => mode switch
    {
        CaptureMode.FullScreen => _settings.FullScreenTarget is not null,
        CaptureMode.Window => _settings.WindowTarget is not null,
        _ => _settings.RegionTarget is not null,
    };

    private void UpdateTargetDisplay()
    {
        CaptureMode mode = _settings.Mode;
        string description = mode switch
        {
            CaptureMode.FullScreen => CaptureTargetText.Describe(_settings.FullScreenTarget),
            CaptureMode.Window => CaptureTargetText.Describe(_settings.WindowTarget, _settings.CaptureActiveWindowDirectly),
            _ => CaptureTargetText.Describe(_settings.RegionTarget),
        };

        bool hasTarget = HasTarget(mode);
        _targetLabel.Text = description;
        _targetLabel.ForeColor = hasTarget ? UiTheme.AccentText : UiTheme.Text;
        _toolTip.SetToolTip(_targetLabel, description);
        _targetHintLabel.Text = hasTarget
            ? Strings.TargetSaved
            : mode == CaptureMode.FullScreen ? Strings.TargetDefault : Strings.TargetEachTime;
        _toolTip.SetToolTip(_targetHintLabel, hasTarget ? Strings.TargetSavedTip : Strings.TargetNoneTip);
        _clearTargetButton.Enabled = hasTarget;
    }

    private async Task DefineTargetAsync()
    {
        if (_isBusy)
        {
            return;
        }

        CaptureMode mode = _settings.Mode;
        if (mode == CaptureMode.FullScreen)
        {
            ShowMonitorMenu();
            return;
        }

        _isBusy = true;
        try
        {
            if (mode == CaptureMode.Window)
            {
                WindowTarget? target = await _captureController.PickWindowTargetAsync();
                if (target is null)
                {
                    SetStatus(Strings.StatusAreaCancelled);
                    return;
                }

                _settings.WindowTarget = target;
            }
            else
            {
                RegionTarget? target = await _captureController.PickRegionTargetAsync();
                if (target is null)
                {
                    SetStatus(Strings.StatusAreaCancelled);
                    return;
                }

                _settings.RegionTarget = target;
            }

            SaveTargetChange();
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Capture area could not be set");
            SetStatus(Strings.StatusAreaFailed(ex.Message), isError: true);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void ShowMonitorMenu()
    {
        _monitorMenu.Items.Clear();
        MonitorTarget? current = _settings.FullScreenTarget;

        AddMonitorItem(Strings.MonitorUnderCursor + Strings.DefaultSuffix, null, current is null);
        _monitorMenu.Items.Add(new ToolStripSeparator());

        Screen[] screens = CaptureTargetText.OrderedScreens();
        for (int i = 0; i < screens.Length; i++)
        {
            string device = screens[i].DeviceName;
            AddMonitorItem(CaptureTargetText.DescribeScreen(screens[i], i), new MonitorTarget(false, device),
                current is { AllMonitors: false } && current.DeviceName == device);
        }

        if (screens.Length > 1)
        {
            _monitorMenu.Items.Add(new ToolStripSeparator());
            var all = new MonitorTarget(true, null);
            AddMonitorItem(CaptureTargetText.Describe(all), all, current is { AllMonitors: true });
        }

        UiTheme.ApplyMenu(_monitorMenu);
        _monitorMenu.Show(_defineTargetButton, new Point(0, _defineTargetButton.Height));
    }

    private void AddMonitorItem(string text, MonitorTarget? target, bool isChecked)
    {
        var item = new ToolStripMenuItem(text) { Checked = isChecked };
        item.Click += (_, _) =>
        {
            _settings.FullScreenTarget = target;
            SaveTargetChange();
        };
        _monitorMenu.Items.Add(item);
    }

    private void ClearTarget()
    {
        switch (_settings.Mode)
        {
            case CaptureMode.FullScreen:
                _settings.FullScreenTarget = null;
                break;
            case CaptureMode.Window:
                _settings.WindowTarget = null;
                break;
            default:
                _settings.RegionTarget = null;
                break;
        }

        SettingsStore.Save(_settings);
        UpdateTargetDisplay();
        SetStatus(Strings.StatusAreaCleared);
    }

    private void SaveTargetChange()
    {
        SettingsStore.Save(_settings);
        UpdateTargetDisplay();
        SetStatus(Strings.StatusAreaSaved, isSuccess: true);
    }

    // ---- Capture ------------------------------------------------------------

    private async Task RunCaptureAsync(CaptureMode mode, int delayMs)
    {
        if (_isBusy || _hotkeyDialogOpen)
        {
            return; // re-entrancy guard (e.g. hotkey pressed while an overlay is open)
        }

        _isBusy = true;
        bool hiddenForCapture = false;
        Task<string> saveTask;

        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            // Fallback when display affinity is unavailable: make the window invisible for a moment.
            if (!_excludedFromCapture && Visible && WindowState != FormWindowState.Minimized)
            {
                Opacity = 0;
                hiddenForCapture = true;
                await Task.Delay(150);
            }

            DateTime timestamp = DateTime.Now;
            var options = new CaptureOptions(
                _settings.IncludeCursor,
                _settings.CaptureActiveWindowDirectly,
                _settings.FullScreenTarget,
                _settings.WindowTarget,
                _settings.RegionTarget);

            Bitmap? bitmap = await _captureController.CaptureAsync(mode, options);
            if (bitmap is null)
            {
                SetStatus(Strings.StatusCancelled);
                return;
            }

            if (_settings.CopyToClipboard)
            {
                CopyToClipboard(bitmap);
            }

            // Encoding and disk I/O run in the background; the saver disposes the bitmap.
            saveTask = _saver.SaveAsync(bitmap, timestamp);
        }
        catch (CaptureTargetException ex)
        {
            SetStatus(ex.Message, isError: true);
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                _tray.ShowBalloonTip(4000, Strings.NotifyCaptureFailedTitle, ex.Message, ToolTipIcon.Warning);
            }

            return;
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, $"Capture failed ({mode})");
            SetStatus(Strings.StatusCaptureFailed(ex.Message), isError: true);
            return;
        }
        finally
        {
            if (hiddenForCapture)
            {
                Opacity = 1;
            }

            _isBusy = false;
        }

        try
        {
            string path = await saveTask;
            OnCaptureSaved(path);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Screenshot could not be saved");
            SetStatus(Strings.StatusSaveFailed(ex.Message), isError: true);
            if (_settings.ShowNotification)
            {
                _tray.ShowBalloonTip(3000, Strings.NotifySaveFailedTitle, ex.Message, ToolTipIcon.Error);
            }
        }
    }

    private static void CopyToClipboard(Bitmap bitmap)
    {
        try
        {
            Clipboard.SetImage(bitmap);
        }
        catch (ExternalException ex)
        {
            ErrorLog.Write(ex, "Clipboard copy failed");
        }
    }

    private void OnCaptureSaved(string path)
    {
        _lastSavedPath = path;
        string fileName = Path.GetFileName(path);
        SetStatus(Strings.StatusSaved(fileName), isSuccess: true);
        _toolTip.SetToolTip(_statusLabel, Strings.StatusSavedTip(path));

        if (_settings.PlaySound)
        {
            SystemSounds.Asterisk.Play();
        }

        if (_settings.ShowNotification)
        {
            _tray.ShowBalloonTip(2000, Strings.NotifySavedTitle, fileName, ToolTipIcon.Info);
        }
    }

    // ---- Dialogs and commands -------------------------------------------------

    private void ShowHotkeyDialog()
    {
        RestoreWindow();

        HotkeyDefinition? result;
        _hotkeys.Suspend();
        _hotkeyDialogOpen = true;
        try
        {
            using var dialog = new HotkeyCaptureDialog(_settings.Hotkey);
            result = dialog.ShowDialog(this) == DialogResult.OK ? dialog.Result : null;
        }
        finally
        {
            _hotkeyDialogOpen = false;
            _hotkeys.Resume();
        }

        if (result is null)
        {
            return;
        }

        if (!_hotkeys.TryApply(result, _settings.SwallowMouseHotkey, out string? error))
        {
            MessageBox.Show(this, error, Strings.HotkeyAssignFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Hotkey = result;
        SettingsStore.Save(_settings);
        UpdateHotkeyDisplay();
        SetStatus(Strings.StatusNewHotkey(result.DisplayText), isSuccess: true);

        if (result is { Kind: HotkeyKind.Keyboard, Key: Keys.PrintScreen } && HotkeyManager.IsPrintScreenSnippingEnabled())
        {
            MessageBox.Show(this, Strings.PrintScreenConflict, "PrintScreen", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowSettingsDialog()
    {
        RestoreWindow();

        using var dialog = new SettingsDialog(_settings, _saver.Directory);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AppSettings updated = dialog.Result;

        if (updated.StartWithWindows != _settings.StartWithWindows && !StartupRegistration.SetEnabled(updated.StartWithWindows))
        {
            MessageBox.Show(this, Strings.StartupWriteFailed, Strings.SettingsTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            updated.StartWithWindows = _settings.StartWithWindows;
        }

        bool reapplyHotkey = updated.SwallowMouseHotkey != _settings.SwallowMouseHotkey
                             && updated.Hotkey.Kind == HotkeyKind.Mouse;

        _settings = updated;
        TopMost = _settings.AlwaysOnTop;

        if (reapplyHotkey && !_hotkeys.TryApply(_settings.Hotkey, _settings.SwallowMouseHotkey, out string? error))
        {
            MessageBox.Show(this, error, Strings.SectionHotkey, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        SettingsStore.Save(_settings);

        // Switching the language rebuilds the whole UI through Strings.LanguageChanged.
        Strings.Current = _settings.Language ?? Strings.Current;
        UiTheme.Mode = _settings.Theme;
        UpdateTargetDisplay();
        if (_settings.Output == CaptureOutput.Video)
        {
            SetStatus(IdleStatusText()); // shows the new audio / frame-rate choice
        }
        else
        {
            SetStatus(Strings.StatusSettingsSaved, isSuccess: true);
        }
    }

    private void OpenCapturesFolder()
    {
        try
        {
            Directory.CreateDirectory(_saver.Directory);
            Process.Start(new ProcessStartInfo { FileName = _saver.Directory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Folder could not be opened");
            SetStatus(Strings.StatusFolderFailed, isError: true);
        }
    }

    private void ShowLastCaptureInExplorer()
    {
        if (_lastSavedPath is null || !File.Exists(_lastSavedPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastSavedPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Explorer could not be opened");
        }
    }

    // ---- UI helpers -----------------------------------------------------------

    private void UpdateHotkeyDisplay()
    {
        HotkeyDefinition? hotkey = _hotkeys.Current;
        string text = hotkey?.DisplayText ?? Strings.HotkeyNone;
        _hotkeyLabel.Text = text;
        _hotkeyKindLabel.Text = hotkey switch
        {
            null => Strings.HotkeyInactive,
            { Kind: HotkeyKind.Mouse } => Strings.HotkeyKindMouse,
            _ => Strings.HotkeyKindKeyboard,
        };

        string trayText = $"ScreenCapture – {text}";
        _tray.Text = trayText.Length <= 63 ? trayText : trayText[..63];
    }

    private void SetStatus(string text, bool isError = false, bool isSuccess = false) =>
        SetStatus(text, isError ? StatusKind.Error : isSuccess ? StatusKind.Success : StatusKind.Normal);

    private void SetStatus(string text, StatusKind kind)
    {
        _statusKind = kind;
        _statusLabel.Text = text;
        _statusLabel.ForeColor = kind switch
        {
            StatusKind.Error => UiTheme.Error,
            StatusKind.Success => UiTheme.Success,
            _ => UiTheme.SecondaryText,
        };
        _toolTip.SetToolTip(_statusLabel, text);
    }

    private enum StatusKind
    {
        Normal,
        Success,
        Error,
    }

    /// <summary>Message box when the window is visible, otherwise a tray balloon (no surprise pop-ups).</summary>
    private void ShowNotice(string message, ToolTipIcon icon)
    {
        if (Visible && WindowState != FormWindowState.Minimized)
        {
            MessageBox.Show(this, message, "ScreenCapture", MessageBoxButtons.OK,
                icon == ToolTipIcon.Warning ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        else
        {
            _tray.ShowBalloonTip(5000, "ScreenCapture", message, icon);
        }
    }
}
