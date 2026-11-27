using ScreenCapture.Capture;
using ScreenCapture.Forms;
using ScreenCapture.Localization;
using ScreenCapture.Recording;
using ScreenCapture.Storage;

namespace ScreenCapture;

/// <summary>Video recording part of the main window: output selector, start/stop, timer and recording frame.</summary>
internal sealed partial class MainForm
{
    private static readonly Color RecordingColor = Color.FromArgb(196, 43, 28);
    private static readonly Color RecordingHoverColor = Color.FromArgb(214, 58, 42);

    private ScreenRecorder? _recorder;
    private RecordingArea? _recordingArea;
    private RecordingFrame? _recordingFrame;
    private System.Windows.Forms.Timer? _recordingTimer;
    private DateTime _recordingTimestamp;
    private string? _recordingTempPath;
    private bool _recordingTransition; // starting or stopping

    private bool IsRecording => _recorder is not null;

    private TableLayoutPanel CreateOutputSelector()
    {
        var segments = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Height = 34,
            Margin = Padding.Empty,
        };

        foreach (CaptureOutput output in Enum.GetValues<CaptureOutput>())
        {
            segments.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            RadioButton segment = UiTheme.Segment(output.ToString());
            segment.CheckedChanged += (_, _) =>
            {
                if (segment.Checked)
                {
                    OnOutputSelected(output);
                }
            };
            _outputButtons[output] = segment;
            segments.Controls.Add(segment);
        }

        return segments;
    }

    private void OnOutputSelected(CaptureOutput output)
    {
        if (_settings.Output != output)
        {
            _settings.Output = output;
            SettingsStore.Save(_settings);
        }

        UpdateCaptureButton();
        if (!IsRecording)
        {
            SetStatus(IdleStatusText());
        }
    }

    /// <summary>Status text when nothing is happening: the audio/frame-rate summary in video mode.</summary>
    private string IdleStatusText()
    {
        if (_settings.Output != CaptureOutput.Video)
        {
            return Strings.Ready;
        }

        IReadOnlyList<AudioSourceSetting> audio = _settings.AudioSources;
        return audio.Count == 0
            ? $"{Strings.VideoNoAudio}  •  {_settings.VideoFrameRate} fps"
            : Strings.VideoSummary(AudioDevices.Describe(audio), _settings.VideoFrameRate);
    }

    /// <summary>Capture button, shortcut: a screenshot in image mode, start/stop in video mode.</summary>
    private void RunPrimaryAction(int delayMs)
    {
        if (IsRecording || _settings.Output == CaptureOutput.Video)
        {
            _ = ToggleRecordingAsync(delayMs);
        }
        else
        {
            _ = RunCaptureAsync(_settings.Mode, delayMs);
        }
    }

    private Task ToggleRecordingAsync(int delayMs) =>
        IsRecording ? StopRecordingAsync(closeAfterwards: false) : StartRecordingAsync(delayMs);

    private async Task StartRecordingAsync(int delayMs)
    {
        if (_isBusy || _hotkeyDialogOpen || _recordingTransition || IsRecording)
        {
            return;
        }

        _isBusy = true;
        _recordingTransition = true;
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs);
            }

            var options = new CaptureOptions(
                _settings.VideoIncludeCursor,
                _settings.CaptureActiveWindowDirectly,
                _settings.FullScreenTarget,
                _settings.WindowTarget,
                _settings.RegionTarget);

            RecordingArea? area = await _captureController.ResolveRecordingAreaAsync(_settings.Mode, options);
            if (area is null)
            {
                SetStatus(Strings.StatusCancelled);
                return;
            }

            SetStatus(Strings.StatusRecordingStarting);
            await Task.Delay(150); // let the selection overlay disappear from the screen

            _recordingTimestamp = DateTime.Now;
            _recordingTempPath = _saver.CreateVideoTempPath(_recordingTimestamp);
            var recordingOptions = new RecordingOptions(
                area,
                _settings.VideoFrameRate,
                _settings.VideoIncludeCursor,
                _settings.AudioSources.ToList(),
                _recordingTempPath);

            ScreenRecorder recorder = await ScreenRecorder.StartAsync(recordingOptions);
            recorder.Failed += OnRecorderFailed;
            _recorder = recorder;
            _recordingArea = area;

            ShowRecordingFrame(area.Bounds);
            _recordingTimer = new System.Windows.Forms.Timer(_components) { Interval = 250 };
            _recordingTimer.Tick += (_, _) => OnRecordingTick();
            _recordingTimer.Start();

            SetRecordingControlsEnabled(false);
            UpdateCaptureButton();

            if (recorder.Warnings.Count > 0)
            {
                SetStatus(string.Join(" ", recorder.Warnings), isError: true);
                _tray.ShowBalloonTip(4000, Strings.NotifyRecordingStartedTitle, string.Join("\n", recorder.Warnings), ToolTipIcon.Warning);
            }
            else
            {
                SetStatus(Strings.StatusRecording, isError: true);
                if (!Visible || WindowState == FormWindowState.Minimized)
                {
                    _tray.ShowBalloonTip(2000, Strings.NotifyRecordingStartedTitle, Strings.StatusRecording, ToolTipIcon.Info);
                }
            }
        }
        catch (CaptureTargetException ex)
        {
            DeleteTempRecording();
            SetStatus(ex.Message, isError: true);
            if (!Visible || WindowState == FormWindowState.Minimized)
            {
                _tray.ShowBalloonTip(4000, Strings.NotifyCaptureFailedTitle, ex.Message, ToolTipIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Recording could not be started");
            DeleteTempRecording();
            SetStatus(Strings.StatusRecordingFailed(ex.Message), isError: true);
        }
        finally
        {
            _recordingTransition = false;
            _isBusy = false;
        }
    }

    private async Task StopRecordingAsync(bool closeAfterwards)
    {
        ScreenRecorder? recorder = _recorder;
        if (recorder is null || _recordingTransition)
        {
            return;
        }

        _recordingTransition = true;
        _recordingTimer?.Stop();
        HideRecordingFrame();
        SetStatus(Strings.StatusFinalizing);
        _captureButton.Enabled = false;

        try
        {
            string tempPath = await recorder.StopAsync();
            string finalPath = await Task.Run(() => _saver.PublishVideo(tempPath, _recordingTimestamp));
            _lastSavedPath = finalPath;
            string fileName = Path.GetFileName(finalPath);
            SetStatus(Strings.StatusSaved(fileName), isSuccess: true);
            _toolTip.SetToolTip(_statusLabel, Strings.StatusSavedTip(finalPath));

            if (_settings.PlaySound)
            {
                System.Media.SystemSounds.Asterisk.Play();
            }

            if (_settings.ShowNotification || !Visible || WindowState == FormWindowState.Minimized)
            {
                _tray.ShowBalloonTip(2000, Strings.NotifyVideoSavedTitle, fileName, ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Recording could not be finished");
            SetStatus(Strings.StatusRecordingFailed(ex.Message), isError: true);
        }
        finally
        {
            recorder.Failed -= OnRecorderFailed;
            recorder.Dispose();
            _recorder = null;
            _recordingArea = null;
            _recordingTempPath = null;
            _recordingTimer?.Dispose();
            _recordingTimer = null;
            _recordingTransition = false;
            _captureButton.Enabled = true;
            SetRecordingControlsEnabled(true);
            UpdateCaptureButton();
        }

        if (closeAfterwards)
        {
            Close();
        }
    }

    private void OnRecorderFailed(object? sender, Exception ex)
    {
        ErrorLog.Write(ex, "Recording stopped because of an error");
        BeginInvoke(async () =>
        {
            await StopRecordingAsync(closeAfterwards: false);
            SetStatus(Strings.StatusRecordingFailed(ex.Message), isError: true);
        });
    }

    private void OnRecordingTick()
    {
        if (_recorder is null || _recordingArea is null)
        {
            return;
        }

        UpdateCaptureButton();

        // Keep the frame on a followed window that moves.
        if (_recordingArea.FollowWindow != 0 && _recordingFrame is not null &&
            Interop.NativeMethods.IsWindow(_recordingArea.FollowWindow))
        {
            Rectangle current = WindowFinder.GetFrameBounds(_recordingArea.FollowWindow);
            _recordingFrame.Track(new Rectangle(current.Location, _recordingArea.Bounds.Size));
        }
    }

    /// <summary>Text and color of the primary button for image mode, video idle and recording.</summary>
    private void UpdateCaptureButton()
    {
        if (IsRecording)
        {
            TimeSpan elapsed = _recorder!.Elapsed;
            _captureButton.Text = Strings.StopRecording(elapsed.ToString(elapsed.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss"));
            _captureButton.BackColor = RecordingColor;
            _captureButton.FlatAppearance.MouseOverBackColor = RecordingHoverColor;
            _captureButton.FlatAppearance.MouseDownBackColor = RecordingColor;
            _toolTip.SetToolTip(_captureButton, Strings.StopRecordingTip);
            return;
        }

        UiTheme.Apply(_captureButton);
        bool video = _settings.Output == CaptureOutput.Video;
        _captureButton.Text = video ? Strings.StartRecording : Strings.CaptureButton;
        _toolTip.SetToolTip(_captureButton, video ? Strings.StartRecordingTip : Strings.CaptureButtonTip);
    }

    /// <summary>While recording, settings that would change the recording are locked.</summary>
    private void SetRecordingControlsEnabled(bool enabled)
    {
        // Only the unselected segments are locked: a disabled selected segment would be unreadable.
        foreach (RadioButton segment in _outputButtons.Values.Concat(_modeButtons.Values))
        {
            segment.Enabled = enabled || segment.Checked;
        }

        _defineTargetButton.Enabled = enabled;
        _clearTargetButton.Enabled = enabled && HasTarget(_settings.Mode);
        _changeHotkeyButton.Enabled = enabled;
        _settingsButton.Enabled = enabled;
    }

    private void ShowRecordingFrame(Rectangle area)
    {
        _recordingFrame = new RecordingFrame();
        _recordingFrame.Show();
        _recordingFrame.Track(area);
    }

    private void HideRecordingFrame()
    {
        _recordingFrame?.Close();
        _recordingFrame?.Dispose();
        _recordingFrame = null;
    }

    private void DeleteTempRecording()
    {
        if (_recordingTempPath is not null)
        {
            try { File.Delete(_recordingTempPath); } catch { /* ignore */ }
            _recordingTempPath = null;
        }
    }
}
