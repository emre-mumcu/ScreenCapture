using System.Runtime.InteropServices;
using ScreenCapture.Interop;
using static ScreenCapture.Interop.MediaFoundation;

namespace ScreenCapture.Recording;

/// <summary>
/// Writes an MP4 file with an H.264 video stream (from top-down RGB32 frames) and an optional
/// AAC audio stream (from 48 kHz stereo 16-bit PCM) using the Media Foundation sink writer.
/// Thread-safe: video and audio may be written from different threads.
/// </summary>
internal sealed unsafe class Mp4Writer : IDisposable
{
    public const int AudioSampleRate = 48000;
    public const int AudioChannels = 2;

    private readonly Lock _lock = new();
    private readonly int _width;
    private readonly int _height;
    private nint _writer;
    private int _videoStream;
    private int _audioStream = -1;
    private bool _started;

    public Mp4Writer(string path, int width, int height, int fps, bool withAudio)
    {
        _width = width;
        _height = height;
        Startup();
        try
        {
            // Prefer the High profile; some (mostly older hardware) encoders only accept Main.
            if (!TryCreate(path, fps, withAudio, eAVEncH264VProfile_High, out int hr) &&
                !TryCreate(path, fps, withAudio, eAVEncH264VProfile_Main, out hr))
            {
                throw new COMException($"No H.264 encoder accepted the video format {width}x{height} (0x{hr:X8}).", hr);
            }
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public bool HasAudio => _audioStream >= 0;

    /// <summary>Bit rate tuned for screen content (sharp text), clamped to a sensible range.</summary>
    public static uint VideoBitrate(int width, int height, int fps) =>
        (uint)Math.Clamp((long)width * height * fps / 8, 1_500_000, 40_000_000);

    private bool TryCreate(string path, int fps, bool withAudio, uint profile, out int hr)
    {
        nint attributes = 0, writer = 0;
        try
        {
            attributes = CreateAttributes(2);
            SetUInt32(attributes, MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);
            SetGuid(attributes, MF_TRANSCODE_CONTAINERTYPE, MFTranscodeContainerType_MPEG4);
            writer = CreateSinkWriter(path, attributes);

            hr = AddVideoStream(writer, fps, profile, out int videoStream);
            if (hr < 0)
            {
                ComCall.Release(ref writer);
                TryDelete(path);
                return false;
            }

            int audioStream = -1;
            if (withAudio)
            {
                audioStream = AddAudioStream(writer);
            }

            SinkBeginWriting(writer);
            _writer = writer;
            writer = 0;
            _videoStream = videoStream;
            _audioStream = audioStream;
            _started = true;
            return true;
        }
        finally
        {
            ComCall.Release(ref writer);
            ComCall.Release(ref attributes);
        }
    }

    private int AddVideoStream(nint writer, int fps, uint profile, out int stream)
    {
        nint output = 0, input = 0;
        try
        {
            output = CreateMediaType();
            SetGuid(output, MF_MT_MAJOR_TYPE, MFMediaType_Video);
            SetGuid(output, MF_MT_SUBTYPE, MFVideoFormat_H264);
            SetUInt32(output, MF_MT_AVG_BITRATE, VideoBitrate(_width, _height, fps));
            SetUInt32(output, MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
            SetUInt32(output, MF_MT_MPEG2_PROFILE, profile);
            SetRatio(output, MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
            SetRatio(output, MF_MT_FRAME_RATE, (uint)fps, 1);
            SetRatio(output, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            stream = SinkAddStream(writer, output);

            input = CreateMediaType();
            SetGuid(input, MF_MT_MAJOR_TYPE, MFMediaType_Video);
            SetGuid(input, MF_MT_SUBTYPE, MFVideoFormat_RGB32);
            SetUInt32(input, MF_MT_INTERLACE_MODE, MFVideoInterlace_Progressive);
            SetRatio(input, MF_MT_FRAME_SIZE, (uint)_width, (uint)_height);
            SetRatio(input, MF_MT_FRAME_RATE, (uint)fps, 1);
            SetRatio(input, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
            SetUInt32(input, MF_MT_DEFAULT_STRIDE, (uint)(_width * 4)); // positive stride = top-down rows
            return SinkSetInputMediaType(writer, stream, input);
        }
        finally
        {
            ComCall.Release(ref input);
            ComCall.Release(ref output);
        }
    }

    private static int AddAudioStream(nint writer)
    {
        nint output = 0, input = 0;
        try
        {
            output = CreateMediaType();
            SetGuid(output, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
            SetGuid(output, MF_MT_SUBTYPE, MFAudioFormat_AAC);
            SetUInt32(output, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
            SetUInt32(output, MF_MT_AUDIO_SAMPLES_PER_SECOND, AudioSampleRate);
            SetUInt32(output, MF_MT_AUDIO_NUM_CHANNELS, AudioChannels);
            SetUInt32(output, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 24000); // 192 kbit/s
            SetUInt32(output, MF_MT_AAC_PAYLOAD_TYPE, 0);
            SetUInt32(output, MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION, 0x29);
            int stream = SinkAddStream(writer, output);

            input = CreateMediaType();
            SetGuid(input, MF_MT_MAJOR_TYPE, MFMediaType_Audio);
            SetGuid(input, MF_MT_SUBTYPE, MFAudioFormat_PCM);
            SetUInt32(input, MF_MT_AUDIO_BITS_PER_SAMPLE, 16);
            SetUInt32(input, MF_MT_AUDIO_SAMPLES_PER_SECOND, AudioSampleRate);
            SetUInt32(input, MF_MT_AUDIO_NUM_CHANNELS, AudioChannels);
            SetUInt32(input, MF_MT_AUDIO_BLOCK_ALIGNMENT, AudioChannels * 2);
            SetUInt32(input, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, AudioSampleRate * AudioChannels * 2);
            ComCall.Check(SinkSetInputMediaType(writer, stream, input), "IMFSinkWriter.SetInputMediaType(audio)");
            return stream;
        }
        finally
        {
            ComCall.Release(ref input);
            ComCall.Release(ref output);
        }
    }

    /// <summary>Writes one top-down RGB32 frame (stride = width * 4). Times are in 100 ns units.</summary>
    public void WriteVideoFrame(byte* pixels, long time, long duration) =>
        WriteSample(_videoStream, pixels, _width * _height * 4, time, duration);

    /// <summary>Writes interleaved 48 kHz stereo 16-bit PCM. Times are in 100 ns units.</summary>
    public void WriteAudio(ReadOnlySpan<short> samples, long time, long duration)
    {
        if (_audioStream < 0 || samples.IsEmpty)
        {
            return;
        }

        fixed (short* data = samples)
        {
            WriteSample(_audioStream, (byte*)data, samples.Length * sizeof(short), time, duration);
        }
    }

    private void WriteSample(int stream, byte* data, int length, long time, long duration)
    {
        nint buffer = 0, sample = 0;
        try
        {
            buffer = CreateMemoryBuffer(length);
            byte* target = BufferLock(buffer);
            Buffer.MemoryCopy(data, target, length, length);
            BufferUnlock(buffer);
            BufferSetCurrentLength(buffer, length);

            sample = CreateSample();
            SampleAddBuffer(sample, buffer);
            SampleSetTime(sample, time);
            SampleSetDuration(sample, duration);

            lock (_lock)
            {
                if (_started)
                {
                    SinkWriteSample(_writer, stream, sample);
                }
            }
        }
        finally
        {
            ComCall.Release(ref sample);
            ComCall.Release(ref buffer);
        }
    }

    /// <summary>Flushes the encoders and writes the MP4 index. The file is complete afterwards.</summary>
    public void Complete()
    {
        lock (_lock)
        {
            if (_started)
            {
                _started = false;
                SinkFinalize(_writer);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _started = false;
            if (_writer != 0)
            {
                ComCall.Release(ref _writer);
                Shutdown();
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* ignore */ }
    }
}
