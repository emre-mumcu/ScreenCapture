using ScreenCapture.Hotkeys;
using ScreenCapture.Interop;
using ScreenCapture.Localization;

namespace ScreenCapture.Forms;

/// <summary>
/// Records a new keyboard or mouse shortcut. Keys are read through an application message filter so
/// that every key (Tab, Enter, Alt combinations, PrintScreen) reaches the dialog instead of the controls.
/// </summary>
internal sealed class HotkeyCaptureDialog : ThemedDialog, IMessageFilter
{
    private readonly Label _shortcutLabel;
    private readonly Label _messageLabel;
    private readonly Panel _mouseArea;
    private readonly Button _saveButton;
    private bool _filterInstalled;

    public HotkeyCaptureDialog(HotkeyDefinition current)
    {
        BeginBuild(Strings.HotkeyDialogTitle);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
        };

        var instruction = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Text = Strings.HotkeyDialogInstruction,
            Margin = new Padding(0, 0, 0, 8),
        };

        _shortcutLabel = new Label
        {
            AutoSize = false,
            Size = new Size(340, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle,
            Font = UiFonts.Large,
            Text = current.DisplayText,
            Tag = ThemeRole.Input,
        };

        _mouseArea = new Panel
        {
            Size = new Size(340, 64),
            BorderStyle = BorderStyle.FixedSingle,
            Tag = ThemeRole.Card,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 8, 0, 0),
        };
        var mouseHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = Strings.HotkeyDialogMouseArea,
            Tag = ThemeRole.SecondaryText,
        };
        mouseHint.MouseDown += OnMouseAreaMouseDown;
        _mouseArea.MouseDown += OnMouseAreaMouseDown;
        _mouseArea.Controls.Add(mouseHint);

        _messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            MinimumSize = new Size(340, 36),
            Margin = new Padding(0, 8, 0, 0),
        };

        _saveButton = new Button { Text = Strings.Save, AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = Strings.Cancel, AutoSize = true, DialogResult = DialogResult.Cancel };
        _saveButton.Click += (_, _) => Result = Candidate;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(_saveButton);

        layout.Controls.Add(instruction);
        layout.Controls.Add(_shortcutLabel);
        layout.Controls.Add(_mouseArea);
        layout.Controls.Add(_messageLabel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        CancelButton = cancelButton;
        EndBuild();
    }

    /// <summary>The recorded shortcut when the dialog returns OK.</summary>
    public HotkeyDefinition? Result { get; private set; }

    private HotkeyDefinition? Candidate { get; set; }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Application.AddMessageFilter(this);
        _filterInstalled = true;
        ActiveControl = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_filterInstalled)
        {
            Application.RemoveMessageFilter(this);
            _filterInstalled = false;
        }

        base.OnFormClosed(e);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (!ContainsFocus)
        {
            return false;
        }

        switch (m.Msg)
        {
            case NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN:
            {
                Keys key = (Keys)(int)m.WParam & Keys.KeyCode;
                HotkeyModifiers modifiers = ModifierState.GetCurrent(asynchronous: false);

                if (key == Keys.Escape && modifiers == HotkeyModifiers.None)
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return true;
                }

                if (HotkeyDefinition.IsModifierKey(key))
                {
                    ShowPendingModifiers(modifiers);
                    return true;
                }

                SetCandidate(HotkeyDefinition.ForKey(key, modifiers));
                return true;
            }

            case NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP:
            {
                Keys key = (Keys)(int)m.WParam & Keys.KeyCode;

                // PrintScreen only produces a key-up message in a normal window.
                if (key == Keys.PrintScreen)
                {
                    SetCandidate(HotkeyDefinition.ForKey(key, ModifierState.GetCurrent(asynchronous: false)));
                    return true;
                }

                if (HotkeyDefinition.IsModifierKey(key) && Candidate is null)
                {
                    ShowPendingModifiers(ModifierState.GetCurrent(asynchronous: false));
                }

                // Swallow Alt key-up so it does not activate the (non-existent) menu loop.
                return true;
            }
        }

        return false;
    }

    private void OnMouseAreaMouseDown(object? sender, MouseEventArgs e)
    {
        MouseHotkeyButton button = e.Button switch
        {
            MouseButtons.Left => MouseHotkeyButton.Left,
            MouseButtons.Right => MouseHotkeyButton.Right,
            MouseButtons.Middle => MouseHotkeyButton.Middle,
            MouseButtons.XButton1 => MouseHotkeyButton.XButton1,
            MouseButtons.XButton2 => MouseHotkeyButton.XButton2,
            _ => MouseHotkeyButton.None,
        };

        SetCandidate(HotkeyDefinition.ForMouse(button, ModifierState.GetCurrent(asynchronous: false)));
    }

    private void ShowPendingModifiers(HotkeyModifiers modifiers)
    {
        if (modifiers == HotkeyModifiers.None)
        {
            return;
        }

        Candidate = null;
        _saveButton.Enabled = false;
        _shortcutLabel.Text = HotkeyDefinition.GetModifiersText(modifiers) + "…";
        _messageLabel.ForeColor = UiTheme.Text;
        _messageLabel.Text = string.Empty;
    }

    private void SetCandidate(HotkeyDefinition hotkey)
    {
        _shortcutLabel.Text = hotkey.DisplayText;
        HotkeyValidation validation = hotkey.Validate();

        if (!validation.IsValid)
        {
            Candidate = null;
            _saveButton.Enabled = false;
            _messageLabel.ForeColor = UiTheme.Error;
            _messageLabel.Text = validation.Error;
            return;
        }

        Candidate = hotkey;
        _saveButton.Enabled = true;
        if (validation.Warning is not null)
        {
            _messageLabel.ForeColor = UiTheme.Warning;
            _messageLabel.Text = validation.Warning;
        }
        else
        {
            _messageLabel.ForeColor = UiTheme.Text;
            _messageLabel.Text = Strings.HotkeyDialogConfirm;
        }
    }
}
