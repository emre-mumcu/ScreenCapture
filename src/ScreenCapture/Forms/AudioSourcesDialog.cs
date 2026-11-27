using ScreenCapture.Localization;
using ScreenCapture.Recording;

namespace ScreenCapture.Forms;

/// <summary>
/// Chooses the audio sources of videos: any number of output devices (what they play) and input devices,
/// or none for a silent video. The Windows default devices can be chosen as such, so they follow a change
/// of the default; saved devices that are not connected stay listed so they are not lost silently.
/// </summary>
internal sealed class AudioSourcesDialog : ThemedDialog
{
    private const int ContentWidth = 440;

    private readonly CheckBox _silent;
    private readonly List<(CheckBox Box, AudioSourceSetting Source)> _sources = [];
    private bool _updating;

    public AudioSourcesDialog(IReadOnlyList<AudioSourceSetting> selected, IReadOnlyList<AudioDeviceInfo>? devices)
    {
        BeginBuild(Strings.AudioTitle);

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };

        layout.Controls.Add(Note(Strings.AudioIntro, secondary: false));

        _silent = new CheckBox { Text = Strings.AudioSilent, AutoSize = true, Margin = new Padding(3, 8, 3, 2) };
        _silent.CheckedChanged += (_, _) => OnSilentChanged();
        layout.Controls.Add(_silent);

        if (devices is null)
        {
            Label warning = Note("⚠ " + Strings.AudioDevicesTimeout, secondary: false);
            warning.Margin = new Padding(3, 6, 3, 2);
            layout.Controls.Add(warning);
        }

        AddGroup(layout, AudioSourceKind.Playback, Strings.AudioPlaybackHeader, Strings.AudioPlaybackHint, selected, devices);
        AddGroup(layout, AudioSourceKind.Input, Strings.AudioInputHeader, null, selected, devices);
        AddApplicationGroup(layout, selected, devices);

        layout.Controls.Add(CreateButtonRow());
        Controls.Add(layout);

        _updating = true;
        _silent.Checked = !_sources.Any(s => s.Box.Checked);
        _updating = false;

        EndBuild();
    }

    /// <summary>The chosen sources (empty = silent video); valid when the dialog returns OK.</summary>
    public List<AudioSourceSetting> Result { get; private set; } = [];

    private void AddGroup(Control parent, AudioSourceKind kind, string header, string? hint,
        IReadOnlyList<AudioSourceSetting> selected, IReadOnlyList<AudioDeviceInfo>? devices)
    {
        parent.Controls.Add(new Label { Text = header, AutoSize = true, Font = UiFonts.Bold, Margin = new Padding(0, 12, 0, 2) });
        if (hint is not null)
        {
            parent.Controls.Add(Note(hint, secondary: true));
        }

        List<AudioDeviceInfo> ofKind = devices?.Where(d => d.Kind == kind).ToList() ?? [];
        string? defaultName = ofKind.FirstOrDefault(d => d.IsDefault)?.Name;

        AddSource(parent, new AudioSourceSetting(kind, null, null),
            kind == AudioSourceKind.Playback ? Strings.AudioDefaultPlayback(defaultName) : Strings.AudioDefaultInput(defaultName),
            selected);

        foreach (AudioDeviceInfo device in ofKind)
        {
            AddSource(parent, new AudioSourceSetting(kind, device.Id, device.Name), device.Name, selected);
        }

        // Saved devices that are unplugged right now.
        if (devices is not null)
        {
            foreach (AudioSourceSetting missing in selected.Where(s => s.Kind == kind && !s.IsDefault &&
                         !ofKind.Any(d => string.Equals(d.Id, s.DeviceId, StringComparison.OrdinalIgnoreCase))))
            {
                AddSource(parent, missing, missing.DisplayName + Strings.AudioNotConnected, selected);
            }

            if (ofKind.Count == 0)
            {
                parent.Controls.Add(Note(Strings.AudioNoDevices, secondary: true));
            }
        }
        else
        {
            // Device list unavailable: keep the saved named devices as they are.
            foreach (AudioSourceSetting saved in selected.Where(s => s.Kind == kind && !s.IsDefault))
            {
                AddSource(parent, saved, saved.DisplayName, selected);
            }
        }
    }

    /// <summary>Programs with an audio session; saved programs that are not running stay listed.</summary>
    private void AddApplicationGroup(Control parent, IReadOnlyList<AudioSourceSetting> selected, IReadOnlyList<AudioDeviceInfo>? devices)
    {
        parent.Controls.Add(new Label { Text = Strings.AudioAppsHeader, AutoSize = true, Font = UiFonts.Bold, Margin = new Padding(0, 12, 0, 2) });
        if (!Interop.CoreAudio.IsProcessLoopbackSupported)
        {
            parent.Controls.Add(Note(Strings.AudioAppsUnsupported, secondary: true));
            return;
        }

        parent.Controls.Add(Note(Strings.AudioAppsHint, secondary: true));

        List<AudioDeviceInfo> apps = devices?.Where(d => d.Kind == AudioSourceKind.Application).ToList() ?? [];
        foreach (AudioDeviceInfo app in apps)
        {
            AddSource(parent, new AudioSourceSetting(AudioSourceKind.Application, app.Id, app.Name),
                app.IsDefault ? app.Name + Strings.AudioAppPlaying : app.Name, selected);
        }

        foreach (AudioSourceSetting saved in selected.Where(s => s.Kind == AudioSourceKind.Application &&
                     !apps.Any(a => string.Equals(a.Id, s.DeviceId, StringComparison.OrdinalIgnoreCase))))
        {
            AddSource(parent, saved, saved.DisplayName + Strings.AudioAppNotRunning, selected);
        }

        if (apps.Count == 0 && devices is not null)
        {
            parent.Controls.Add(Note(Strings.AudioAppsNone, secondary: true));
        }
    }

    private void AddSource(Control parent, AudioSourceSetting source, string text, IReadOnlyList<AudioSourceSetting> selected)
    {
        var box = new CheckBox
        {
            Text = text,
            AutoSize = true,
            UseMnemonic = false, // device names may contain '&'
            Checked = selected.Any(s => s.SameSource(source)),
            Margin = new Padding(3, 2, 3, 2),
        };
        box.CheckedChanged += (_, _) => OnSourceChanged();
        _sources.Add((box, source));
        parent.Controls.Add(box);
    }

    private void OnSilentChanged()
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        if (_silent.Checked)
        {
            foreach ((CheckBox box, _) in _sources)
            {
                box.Checked = false;
            }
        }
        else if (!_sources.Any(s => s.Box.Checked) && _sources.Count > 0)
        {
            _sources[0].Box.Checked = true; // unchecking "silent" means: record something (default output)
        }

        _updating = false;
    }

    private void OnSourceChanged()
    {
        if (_updating)
        {
            return;
        }

        _updating = true;
        _silent.Checked = !_sources.Any(s => s.Box.Checked);
        _updating = false;
    }

    private static Label Note(string text, bool secondary) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(ContentWidth, 0),
        Margin = new Padding(3, 0, 3, 2),
        Tag = secondary ? ThemeRole.SecondaryText : null,
    };

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

        var okButton = new Button { Text = Strings.Ok, AutoSize = true, MinimumSize = new Size(80, 28), DialogResult = DialogResult.OK, Margin = new Padding(6, 0, 0, 0) };
        var cancelButton = new Button { Text = Strings.Cancel, AutoSize = true, MinimumSize = new Size(80, 28), DialogResult = DialogResult.Cancel, Margin = new Padding(6, 0, 0, 0) };
        okButton.Click += (_, _) => Result = _sources.Where(s => s.Box.Checked).Select(s => s.Source).ToList();

        row.Controls.Add(new Label { AutoSize = true }, 0, 0);
        row.Controls.Add(okButton, 1, 0);
        row.Controls.Add(cancelButton, 2, 0);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return row;
    }
}
