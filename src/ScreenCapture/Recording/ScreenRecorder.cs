using System.Diagnostics;
using ScreenCapture.Capture;
using ScreenCapture.Interop;
using ScreenCapture.Localization;

namespace ScreenCapture.Recording;

/// <summary>What to record: a fixed screen rectangle, or a window whose position is followed.</summary>
internal sealed record RecordingArea(Rectangle Bounds, nint FollowWindow = 0);

internal sealed record RecordingOptions(
    RecordingArea Area,
    int FrameRate,
    bool IncludeCursor,
    bool SystemAudio,
    bool Microphone,
    string OutputPath);

/// <summary>
/// Records the screen (and optionally system audio / microphone) into an MP4 file.
/// Video frames are grabbed on a dedicated thread at the requested frame rate with real timestamps
/// (variable frame rate if the machine cannot keep up); audio is mixed on another thread with a
/// small latency so both sources line up on the same clock.
/// </summary>
internal sealed class ScreenRecorder : IDisposable
{
    /// <summary>H.264 level 5.2 limit; larger areas are scaled down.</summary>
    private const int MaxWidth = 4096;
    private const int MaxHeight = 2304;
    private const int MinSize = 32;
    private static readonly long MixLatencyFrames = Mp4Writer.AudioSampleRate * 200L / 1000;
    private static readonly TimeSpan AudioOpenTimeout = TimeSpan.FromSeconds(5);

#if DEBUG
    /// <summary>Diagnostics: mix a 440 Hz tone instead of opening audio devices.</summary>
    private static readonly bool TestTone = Environment.GetEnvironmentVariable("SCREENCAPTURE_TEST_TONE") == "1";
#endif

    private readonly RecordingOptions _options;
    private readonly Mp4Writer _writer;
    private readonly List<AudioCaptureSource> _audioSources = [];
    private readonly Stopwatch _clock = new();
    private readonly Thread _videoThread;
    private readonly Thread? _audioThread;
    private readonly ManualResetEventSlim _stopSignal = new();
    private readonly Size _sourceSize;
    private readonly Size _frameSize;
    private long _startHns;
    private long _audioFramesWritten;
    private Exception? _error;
    private int _stopped;

    private ScreenRecorder(RecordingOptions options, List<string> warnings)
    {
        _options = options;
        Warnings = warnings;

        _sourceSize = new Size(options.Area.Bounds.Width, options.Area.Bounds.Height);
        _frameSize = ComputeFrameSize(_sourceSize);

        bool wantsAudio = options.SystemAudio || options.Microphone;
        RecorderTrace.Write($"Creating writer {_frameSize.Width}x{_frameSize.Height} @ {options.FrameRate} fps, audio={wantsAudio}");
        _writer = new Mp4Writer(options.OutputPath, _frameSize.Width, _frameSize.Height, options.FrameRate, wantsAudio);
        RecorderTrace.Write("Writer created");

        _videoThread = new Thread(VideoLoop) { IsBackground = true, Name = "Video capture", Priority = ThreadPriority.AboveNormal };
        _videoThread.SetApartmentState(ApartmentState.MTA);
        if (wantsAudio)
        {
            _audioThread = new Thread(AudioLoop) { IsBackground = true, Name = "Audio mixer" };
            _audioThread.SetApartmentState(ApartmentState.MTA);
        }
    }

    /// <summary>Raised on a background thread if recording stops because of an error.</summary>
    public event EventHandler<Exception>? Failed;

    /// <summary>Non-fatal problems (e.g. no microphone found) in the UI language.</summary>
    public IReadOnlyList<string> Warnings { get; }

    public TimeSpan Elapsed => _clock.Elapsed;

    public Size FrameSize => _frameSize;

    /// <summary>Creates the file and starts recording. Runs the COM setup on an MTA thread.</summary>
    public static Task<ScreenRecorder> StartAsync(RecordingOptions options)
    {
        Rectangle bounds = options.Area.Bounds;
        if (bounds.Width < MinSize || bounds.Height < MinSize)
        {
            throw new CaptureTargetException(Strings.VideoAreaTooSmall(MinSize));
        }

        return Task.Run(() =>
        {
            var warnings = new List<string>();
            var recorder = new ScreenRecorder(options, warnings);
            try
            {
                recorder.OpenAudio(warnings);
                RecorderTrace.Write($"Audio sources: {recorder._audioSources.Count}");
                recorder.Begin();
                return recorder;
            }
            catch
            {
                recorder.Dispose();
                throw;
            }
        });
    }

    private static Size ComputeFrameSize(Size source)
    {
        double scale = Math.Min(1.0, Math.Min((double)MaxWidth / source.Width, (double)MaxHeight / source.Height));
        int width = (int)(source.Width * scale) & ~1; // H.264 needs even dimensions
        int height = (int)(source.Height * scale) & ~1;
        return new Size(Math.Max(width, 2), Math.Max(height, 2));
    }

    private void OpenAudio(List<string> warnings)
    {
        if (!_writer.HasAudio)
        {
            return;
        }

        _startHns = AudioCaptureSource.NowHns();
#if DEBUG
        if (TestTone)
        {
            return; // diagnostics: a synthetic tone replaces the devices
        }
#endif
        TryAddSource(_options.SystemAudio, loopback: true, Strings.VideoSystemAudio, warnings);
        TryAddSource(_options.Microphone, loopback: false, Strings.VideoMicrophone, warnings);
    }

    private void TryAddSource(bool enabled, bool loopback, string name, List<string> warnings)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            // A hung Windows Audio service can block IAudioClient calls for ~40 s; do not hold up the recording.
            Task<AudioCaptureSource?> open = Task.Run(() => AudioCaptureSource.TryOpen(loopback, name, _startHns));
            if (!open.Wait(AudioOpenTimeout))
            {
                warnings.Add(Strings.VideoAudioDeviceTimeout(name));
                Storage.ErrorLog.Write(new TimeoutException($"Opening '{name}' took longer than {AudioOpenTimeout}."), "Audio source timeout");
                _ = open.ContinueWith(t => t.Result?.Dispose(), TaskContinuationOptions.OnlyOnRanToCompletion);
                return;
            }

            AudioCaptureSource? source = open.GetAwaiter().GetResult();
            if (source is null)
            {
                warnings.Add(Strings.VideoAudioDeviceMissing(name));
                return;
            }

            _audioSources.Add(source);
        }
        catch (Exception ex)
        {
            Storage.ErrorLog.Write(ex, $"Audio source '{name}' could not be opened");
            warnings.Add(Strings.VideoAudioDeviceFailed(name));
        }
    }

    private void Begin()
    {
        Gdi.timeBeginPeriod(1); // precise frame pacing
        _startHns = AudioCaptureSource.NowHns();
        _clock.Start();
        foreach (AudioCaptureSource source in _audioSources)
        {
            source.Start();
        }

        _videoThread.Start();
        _audioThread?.Start();
    }

    /// <summary>Time on the recording clock in 100 ns units (same domain as WASAPI timestamps minus start).</summary>
    private long NowTicks() => AudioCaptureSource.NowHns() - _startHns;

    private void VideoLoop()
    {
        try
        {
            bool scaled = _frameSize != _sourceSize;
            using var grabber = new FrameGrabber(_frameSize.Width, _frameSize.Height, scaled);
            long interval = 10_000_000L / _options.FrameRate;
            long next = 0;
            long lastTime = -1;

            while (!_stopSignal.IsSet)
            {
                long now = NowTicks();
                if (now < next)
                {
                    _stopSignal.Wait(TimeSpan.FromTicks(next - now));
                    continue;
                }

                grabber.Capture(CurrentSourceRectangle(), _options.IncludeCursor);
                if (lastTime < 0) RecorderTrace.Write("First frame grabbed");
                long time = Math.Max(now, lastTime + 1);
                unsafe
                {
                    _writer.WriteVideoFrame(grabber.Pixels, time, interval);
                }

                if (lastTime < 0) RecorderTrace.Write("First frame written");
                lastTime = time;
                next += interval;
                if (next < now)
                {
                    next = now + interval; // machine is too slow for this frame rate: drop frames
                }
            }
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private Rectangle CurrentSourceRectangle()
    {
        Rectangle bounds = _options.Area.Bounds;
        nint window = _options.Area.FollowWindow;
        if (window != 0 && NativeMethods.IsWindow(window) && !NativeMethods.IsIconic(window))
        {
            Point location = WindowFinder.GetFrameBounds(window).Location;
            bounds = new Rectangle(location, _sourceSize);
        }

        return bounds;
    }

    private void AudioLoop()
    {
        try
        {
            while (!_stopSignal.Wait(20))
            {
                long target = NowTicks() * Mp4Writer.AudioSampleRate / 10_000_000 - MixLatencyFrames;
                WriteAudioUntil(target);
            }
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private void WriteAudioUntil(long targetFrame)
    {
        while (targetFrame > _audioFramesWritten)
        {
            int frames = (int)Math.Min(targetFrame - _audioFramesWritten, Mp4Writer.AudioSampleRate / 2);
            var mix = new float[frames * 2];
            foreach (AudioCaptureSource source in _audioSources)
            {
                source.MixInto(_audioFramesWritten, frames, mix);
            }

#if DEBUG
            if (TestTone)
            {
                for (int i = 0; i < frames; i++)
                {
                    float v = 0.3f * MathF.Sin(2 * MathF.PI * 440 * (_audioFramesWritten + i) / Mp4Writer.AudioSampleRate);
                    mix[i * 2] += v;
                    mix[i * 2 + 1] += v;
                }
            }
#endif

            var pcm = new short[mix.Length];
            for (int i = 0; i < mix.Length; i++)
            {
                pcm[i] = (short)Math.Clamp(mix[i] * 32767f, short.MinValue, short.MaxValue);
            }

            long time = _audioFramesWritten * 10_000_000 / Mp4Writer.AudioSampleRate;
            long duration = (long)frames * 10_000_000 / Mp4Writer.AudioSampleRate;
            _writer.WriteAudio(pcm, time, duration);
            _audioFramesWritten += frames;
        }
    }

    private void Fail(Exception ex)
    {
        _error ??= ex;
        _stopSignal.Set();
        Failed?.Invoke(this, ex);
    }

    /// <summary>Stops recording and finalizes the MP4 file. Returns the output path.</summary>
    public Task<string> StopAsync() => Task.Run(() =>
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return _options.OutputPath;
        }

        _stopSignal.Set();
        _videoThread.Join();
        _audioThread?.Join();
        long endTicks = NowTicks();
        _clock.Stop();

        foreach (AudioCaptureSource source in _audioSources)
        {
            source.Stop();
        }

        if (_error is null && _writer.HasAudio)
        {
            // Flush everything that was captured up to the end of the video.
            WriteAudioUntil(endTicks * Mp4Writer.AudioSampleRate / 10_000_000);
        }

        _writer.Complete();
        Gdi.timeEndPeriod(1);
        return _options.OutputPath;
    });

    public void Dispose()
    {
        _stopSignal.Set();
        if (_videoThread.IsAlive) _videoThread.Join();
        if (_audioThread?.IsAlive == true) _audioThread.Join();

        foreach (AudioCaptureSource source in _audioSources)
        {
            source.Dispose();
        }

        _writer.Dispose();
        if (_stopped == 0 && _clock.IsRunning)
        {
            Gdi.timeEndPeriod(1);
        }

        _stopSignal.Dispose();
    }
}
