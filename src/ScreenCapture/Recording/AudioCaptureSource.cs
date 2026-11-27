using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenCapture.Interop;
using static ScreenCapture.Interop.CoreAudio;

namespace ScreenCapture.Recording;

/// <summary>
/// Captures one audio endpoint with WASAPI in shared mode: a playback device in loopback
/// mode ("what you hear") or an input device such as a microphone. Audio is converted to 48 kHz stereo float and
/// placed on the recording timeline using the QPC timestamps WASAPI reports, so silence (loopback
/// delivers no packets while nothing plays) and device clock drift stay in sync with the video.
/// </summary>
internal sealed unsafe class AudioCaptureSource : IDisposable
{
    private const int OutputRate = Mp4Writer.AudioSampleRate;
    private const long BufferDuration = 2_000_000; // 200 ms in 100 ns units

    private readonly long _startHns;
    private readonly Lock _lock = new();
    private readonly Thread _thread;
    private volatile bool _stop;

    private nint _client;
    private nint _capture;
    private nint _event;
    private int _channels;
    private int _sampleRate;
    private bool _isFloat;
    private int _bitsPerSample;
    private float _peak;   // diagnostics only (RecorderTrace)
    private int _packets;

    // Timeline buffer: stereo float frames starting at timeline frame _bufferStart.
    private float[] _buffer = new float[OutputRate * 2];
    private int _bufferFrames;
    private long _bufferStart;
    private long _nextFrame = -1;

    // Linear resampler state.
    private double _resamplePosition;
    private float _previousLeft;
    private float _previousRight;
    private float[] _converted = [];

    private AudioCaptureSource(string name, long startHns)
    {
        Name = name;
        _startHns = startHns;
        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = $"Audio capture ({name})" };
        _thread.SetApartmentState(ApartmentState.MTA);
    }

    public string Name { get; }

    /// <summary>Error that stopped the capture thread, if any.</summary>
    public Exception? Error { get; private set; }

    /// <summary>Endpoint id of the opened device (the resolved id for "default device" settings).</summary>
    public string DeviceId { get; private set; } = string.Empty;

    public bool Loopback { get; private set; }

    /// <summary>
    /// Opens the device of <paramref name="setting"/>: the current default device, or the saved device id.
    /// Returns null if there is no such (active) device.
    /// </summary>
    public static AudioCaptureSource? TryOpen(AudioSourceSetting setting, long startHns)
    {
        bool loopback = setting.Kind == AudioSourceKind.Playback;
        nint enumerator = 0, device = 0;
        var source = new AudioCaptureSource(setting.DisplayName, startHns);
        try
        {
            enumerator = CreateDeviceEnumerator();
            int hr = setting.DeviceId is null
                ? GetDefaultEndpoint(enumerator, loopback ? eRender : eCapture, out device)
                : GetDevice(enumerator, setting.DeviceId, out device);
            uint state = hr >= 0 ? DeviceGetState(device) : 0;
            RecorderTrace.Write($"{setting.DisplayName}: open hr=0x{hr:X8} state={state}");
            if (hr < 0 || state != DEVICE_STATE_ACTIVE)
            {
                source.Dispose();
                return null;
            }

            source.DeviceId = DeviceGetId(device);
            source.Loopback = loopback;
            source.Initialize(device, loopback);
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
        finally
        {
            ComCall.Release(ref device);
            ComCall.Release(ref enumerator);
        }
    }

    /// <summary>
    /// Opens a capture of what process <paramref name="processId"/> (and its child processes) plays.
    /// <paramref name="name"/> is used in messages.
    /// </summary>
    public static AudioCaptureSource OpenProcess(uint processId, string name, long startHns, TimeSpan timeout)
    {
        var source = new AudioCaptureSource(name, startHns) { DeviceId = "process:" + processId, Loopback = true };
        try
        {
            RecorderTrace.Write($"{name}: activate process loopback for {processId}");
            source._client = ActivateProcessLoopbackClient(processId, timeout);

            // Process loopback has no mix format: ask for 48 kHz stereo float, Windows converts.
            source._channels = 2;
            source._sampleRate = OutputRate;
            source._bitsPerSample = 32;
            source._isFloat = true;
            var format = new WAVEFORMATEX
            {
                wFormatTag = WAVE_FORMAT_IEEE_FLOAT,
                nChannels = 2,
                nSamplesPerSec = OutputRate,
                wBitsPerSample = 32,
                nBlockAlign = 8,
                nAvgBytesPerSec = OutputRate * 8,
            };
            ClientInitialize(source._client,
                AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY,
                BufferDuration, &format);

            // Event-driven mode is required by process loopback; the loop still polls, the event just has to exist.
            source._event = CreateEvent(0, false, false, 0);
            ClientSetEventHandle(source._client, source._event);
            source._capture = ClientGetCaptureService(source._client);
            RecorderTrace.Write($"{name}: initialized");
            return source;
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    private void Initialize(nint device, bool loopback)
    {
        RecorderTrace.Write($"{Name}: activate");
        _client = ActivateAudioClient(device);
        RecorderTrace.Write($"{Name}: activated");
        WAVEFORMATEX* format = ClientGetMixFormat(_client);
        try
        {
            _channels = format->nChannels;
            _sampleRate = (int)format->nSamplesPerSec;
            _bitsPerSample = format->wBitsPerSample;
            _isFloat = format->wFormatTag == WAVE_FORMAT_IEEE_FLOAT ||
                       (format->wFormatTag == WAVE_FORMAT_EXTENSIBLE &&
                        ((WAVEFORMATEXTENSIBLE*)format)->SubFormat == KSDATAFORMAT_SUBTYPE_IEEE_FLOAT);

            if (_channels < 1 || (_isFloat && _bitsPerSample != 32) || (!_isFloat && _bitsPerSample is not (16 or 24 or 32)))
            {
                throw new NotSupportedException($"Unsupported audio format: {_bitsPerSample}-bit, {_channels} channels.");
            }

            RecorderTrace.Write($"{Name}: initialize {_sampleRate} Hz {_channels} ch {_bitsPerSample} bit");
            ClientInitialize(_client, loopback ? AUDCLNT_STREAMFLAGS_LOOPBACK : 0, BufferDuration, format);
            RecorderTrace.Write($"{Name}: initialized");
        }
        finally
        {
            CoTaskMemFree((nint)format);
        }

        _capture = ClientGetCaptureService(_client);
    }

    public void Start()
    {
        ClientStart(_client);
        _thread.Start();
    }

    public void Stop()
    {
        _stop = true;
        if (_thread.IsAlive)
        {
            _thread.Join();
        }

        if (_client != 0)
        {
            ClientStop(_client);
        }

        RecorderTrace.Write($"{Name}: stopped, {_packets} packets, peak {_peak:0.000}");
    }

    /// <summary>
    /// Adds the frames for timeline range [<paramref name="fromFrame"/>, fromFrame + frames) into
    /// <paramref name="mix"/> (stereo interleaved). Missing frames count as silence. Consumed data is discarded.
    /// </summary>
    public void MixInto(long fromFrame, int frames, Span<float> mix)
    {
        lock (_lock)
        {
            long overlapStart = Math.Max(fromFrame, _bufferStart);
            long overlapEnd = Math.Min(fromFrame + frames, _bufferStart + _bufferFrames);
            for (long f = overlapStart; f < overlapEnd; f++)
            {
                int src = (int)(f - _bufferStart) * 2;
                int dst = (int)(f - fromFrame) * 2;
                mix[dst] += _buffer[src];
                mix[dst + 1] += _buffer[src + 1];
            }

            Discard(fromFrame + frames);
        }
    }

    private void CaptureLoop()
    {
        try
        {
            while (!_stop)
            {
                Thread.Sleep(10);
                Drain();
            }

            Drain();
        }
        catch (Exception ex)
        {
            Error = ex;
        }
    }

    private void Drain()
    {
        uint packet = CaptureGetNextPacketSize(_capture);
        while (packet > 0)
        {
            byte* data;
            uint frames, flags;
            ulong devicePosition, qpcPosition;
            ComCall.Check(CaptureGetBuffer(_capture, &data, &frames, &flags, &devicePosition, &qpcPosition), "IAudioCaptureClient.GetBuffer");
            try
            {
                if (frames > 0)
                {
                    long time = (long)qpcPosition;
                    if (time == 0 || (flags & AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR) != 0)
                    {
                        // No usable device timestamp: the packet ends about now.
                        time = NowHns() - frames * 10_000_000L / _sampleRate;
                    }

                    Append(data, (int)frames, (flags & AUDCLNT_BUFFERFLAGS_SILENT) != 0, time);
                }
            }
            finally
            {
                CaptureReleaseBuffer(_capture, frames);
            }

            packet = CaptureGetNextPacketSize(_capture);
        }
    }

    private void Append(byte* data, int frames, bool silent, long qpcHns)
    {
        int outputFrames = ConvertAndResample(data, frames, silent);
        if (RecorderTrace.FilePath is not null)
        {
            for (int i = 0; i < outputFrames * 2; i++) _peak = Math.Max(_peak, Math.Abs(_converted[i]));
            _packets++;
        }

        long position = (qpcHns - _startHns) * OutputRate / 10_000_000;

        lock (_lock)
        {
            int skip = 0;
            if (_nextFrame < 0)
            {
                _nextFrame = Math.Max(0, position);
                _bufferStart = _nextFrame;
                skip = position < 0 ? (int)Math.Min(outputFrames, -position) : 0;
            }
            else
            {
                long drift = position - _nextFrame;
                if (drift > OutputRate * 30 / 1000)
                {
                    // Gap (e.g. loopback was silent): keep the timeline by inserting silence.
                    AppendSilence((int)Math.Min(drift, OutputRate * 10L));
                }
                else if (drift < -OutputRate * 50 / 1000)
                {
                    // Device clock ran ahead of the timeline: drop the overlap.
                    skip = (int)Math.Min(outputFrames, -drift);
                }
            }

            AppendFrames(_converted.AsSpan(skip * 2, (outputFrames - skip) * 2));
        }
    }

    /// <summary>Converts a WASAPI packet to stereo float at 48 kHz into <see cref="_converted"/>; returns frames.</summary>
    private int ConvertAndResample(byte* data, int frames, bool silent)
    {
        // 1) Source format -> stereo float at the source rate.
        var stereo = new float[frames * 2];
        if (!silent)
        {
            int bytesPerSample = _bitsPerSample / 8;
            for (int i = 0; i < frames; i++)
            {
                byte* frame = data + (long)i * _channels * bytesPerSample;
                float left = ReadSample(frame);
                float right = _channels > 1 ? ReadSample(frame + bytesPerSample) : left;
                stereo[i * 2] = left;
                stereo[i * 2 + 1] = right;
            }
        }

        // 2) Resample to 48 kHz (linear interpolation, continuous across packets).
        if (_sampleRate == OutputRate)
        {
            _converted = stereo;
            return frames;
        }

        double step = (double)_sampleRate / OutputRate;
        int capacity = (int)(frames / step) + 4;
        if (_converted.Length < capacity * 2)
        {
            _converted = new float[capacity * 2];
        }

        int count = 0;
        double t = _resamplePosition; // position relative to this packet; -1 = previous packet's last frame
        while (t <= frames - 1 && count < capacity)
        {
            int i = (int)Math.Floor(t);
            float fraction = (float)(t - i);
            float l0 = i < 0 ? _previousLeft : stereo[i * 2];
            float r0 = i < 0 ? _previousRight : stereo[i * 2 + 1];
            float l1 = i + 1 < frames ? stereo[(i + 1) * 2] : l0;
            float r1 = i + 1 < frames ? stereo[(i + 1) * 2 + 1] : r0;
            _converted[count * 2] = l0 + (l1 - l0) * fraction;
            _converted[count * 2 + 1] = r0 + (r1 - r0) * fraction;
            count++;
            t += step;
        }

        _resamplePosition = t - frames;
        _previousLeft = stereo[(frames - 1) * 2];
        _previousRight = stereo[(frames - 1) * 2 + 1];
        return count;
    }

    private float ReadSample(byte* p) => _isFloat
        ? *(float*)p
        : _bitsPerSample switch
        {
            16 => *(short*)p / 32768f,
            24 => ((p[0] << 8) | (p[1] << 16) | (p[2] << 24)) / 2147483648f,
            _ => *(int*)p / 2147483648f,
        };

    private void AppendSilence(int frames)
    {
        EnsureCapacity(frames);
        Array.Clear(_buffer, _bufferFrames * 2, frames * 2);
        _bufferFrames += frames;
        _nextFrame += frames;
    }

    private void AppendFrames(ReadOnlySpan<float> samples)
    {
        int frames = samples.Length / 2;
        EnsureCapacity(frames);
        samples.CopyTo(_buffer.AsSpan(_bufferFrames * 2));
        _bufferFrames += frames;
        _nextFrame += frames;
    }

    private void EnsureCapacity(int extraFrames)
    {
        int needed = (_bufferFrames + extraFrames) * 2;
        if (needed > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));
        }
    }

    /// <summary>Drops buffered frames before timeline frame <paramref name="frame"/>.</summary>
    private void Discard(long frame)
    {
        long drop = Math.Min(frame - _bufferStart, _bufferFrames);
        if (drop <= 0)
        {
            if (_bufferFrames == 0 && frame > _bufferStart && _nextFrame >= 0)
            {
                _bufferStart = Math.Max(_bufferStart, Math.Min(frame, _nextFrame));
            }

            return;
        }

        int remaining = _bufferFrames - (int)drop;
        Array.Copy(_buffer, (int)drop * 2, _buffer, 0, remaining * 2);
        _bufferFrames = remaining;
        _bufferStart += drop;
    }

    public void Dispose()
    {
        if (!_stop)
        {
            try { Stop(); } catch { /* ignore */ }
        }

        ComCall.Release(ref _capture);
        ComCall.Release(ref _client);
        if (_event != 0)
        {
            CloseHandle(_event);
            _event = 0;
        }
    }

    /// <summary>Current time in the 100 ns QPC domain used by WASAPI timestamps.</summary>
    public static long NowHns() => (long)(Stopwatch.GetTimestamp() * (10_000_000.0 / Stopwatch.Frequency));
}
