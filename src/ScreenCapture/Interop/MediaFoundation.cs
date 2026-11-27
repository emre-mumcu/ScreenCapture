using System.Runtime.InteropServices;
using static ScreenCapture.Interop.ComCall;

namespace ScreenCapture.Interop;

/// <summary>Media Foundation functions, GUIDs and vtable calls used to write H.264/AAC MP4 files.</summary>
internal static unsafe partial class MediaFoundation
{
    private const uint MF_VERSION = 0x00020070;

    // Media types
    public static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFMediaType_Audio = new("73647561-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFAudioFormat_AAC = new("00001610-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFAudioFormat_PCM = new("00000001-0000-0010-8000-00AA00389B71");

    // Video attributes
    public static readonly Guid MF_MT_AVG_BITRATE = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    public static readonly Guid MF_MT_INTERLACE_MODE = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    public static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    public static readonly Guid MF_MT_FRAME_RATE = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    public static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    public static readonly Guid MF_MT_DEFAULT_STRIDE = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    public static readonly Guid MF_MT_MPEG2_PROFILE = new("ad76a80b-2d5c-4e0b-b375-64e520137036");
    public const uint MFVideoInterlace_Progressive = 2;
    public const uint eAVEncH264VProfile_Main = 77;
    public const uint eAVEncH264VProfile_High = 100;

    // Audio attributes
    public static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    public static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    public static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    public static readonly Guid MF_MT_AAC_PAYLOAD_TYPE = new("bfbabe79-7434-4d1c-94f0-72a3b9e17188");
    public static readonly Guid MF_MT_AAC_AUDIO_PROFILE_LEVEL_INDICATION = new("7632f0e6-9538-4d61-acda-ea29c8c14456");

    // Sink writer attributes
    public static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = new("a634a91c-822b-41b9-a494-4de4643612b0");
    public static readonly Guid MF_TRANSCODE_CONTAINERTYPE = new("150ff23f-4abc-478b-ac4f-e1916fba1cca");
    public static readonly Guid MFTranscodeContainerType_MPEG4 = new("dc6cd05d-b9d0-40ef-bd35-fa622c1ab28a");

    private static readonly Lock s_startupLock = new();
    private static int s_startupCount;

    [LibraryImport("mfplat.dll")]
    private static partial int MFStartup(uint version, uint flags);

    [LibraryImport("mfplat.dll")]
    private static partial int MFShutdown();

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateAttributes(nint* attributes, uint initialSize);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMediaType(nint* mediaType);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateSample(nint* sample);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMemoryBuffer(uint maxLength, nint* buffer);

    [LibraryImport("mfreadwrite.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MFCreateSinkWriterFromURL(string url, nint byteStream, nint attributes, nint* sinkWriter);

    public static void Startup()
    {
        lock (s_startupLock)
        {
            if (s_startupCount++ == 0)
            {
                Check(MFStartup(MF_VERSION, 0), "MFStartup");
            }
        }
    }

    public static void Shutdown()
    {
        lock (s_startupLock)
        {
            if (s_startupCount > 0 && --s_startupCount == 0)
            {
                MFShutdown();
            }
        }
    }

    public static nint CreateAttributes(uint size)
    {
        nint result;
        Check(MFCreateAttributes(&result, size), "MFCreateAttributes");
        return result;
    }

    public static nint CreateMediaType()
    {
        nint result;
        Check(MFCreateMediaType(&result), "MFCreateMediaType");
        return result;
    }

    public static nint CreateSample()
    {
        nint result;
        Check(MFCreateSample(&result), "MFCreateSample");
        return result;
    }

    public static nint CreateMemoryBuffer(int length)
    {
        nint result;
        Check(MFCreateMemoryBuffer((uint)length, &result), "MFCreateMemoryBuffer");
        return result;
    }

    public static nint CreateSinkWriter(string path, nint attributes)
    {
        nint result;
        Check(MFCreateSinkWriterFromURL(path, 0, attributes, &result), "MFCreateSinkWriterFromURL");
        return result;
    }

    // ---- IMFAttributes (also the first 30 methods of IMFMediaType and IMFSample) ----

    public static void SetUInt32(nint attributes, Guid key, uint value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)Slot(attributes, 21))(attributes, &key, value), "IMFAttributes.SetUINT32");

    public static void SetUInt64(nint attributes, Guid key, ulong value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)Slot(attributes, 22))(attributes, &key, value), "IMFAttributes.SetUINT64");

    public static void SetGuid(nint attributes, Guid key, Guid value) =>
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)Slot(attributes, 24))(attributes, &key, &value), "IMFAttributes.SetGUID");

    public static void SetRatio(nint attributes, Guid key, uint high, uint low) =>
        SetUInt64(attributes, key, ((ulong)high << 32) | low);

    // ---- IMFSinkWriter ----

    public static int SinkAddStream(nint writer, nint mediaType)
    {
        uint index;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)Slot(writer, 3))(writer, mediaType, &index), "IMFSinkWriter.AddStream");
        return (int)index;
    }

    public static int SinkSetInputMediaType(nint writer, int stream, nint mediaType) =>
        ((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Slot(writer, 4))(writer, (uint)stream, mediaType, 0);

    public static void SinkBeginWriting(nint writer) =>
        Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 5))(writer), "IMFSinkWriter.BeginWriting");

    public static void SinkWriteSample(nint writer, int stream, nint sample) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(writer, 6))(writer, (uint)stream, sample), "IMFSinkWriter.WriteSample");

    public static void SinkFinalize(nint writer) =>
        Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(writer, 11))(writer), "IMFSinkWriter.Finalize");

    // ---- IMFSample (IMFAttributes occupies slots 3..32) ----

    public static void SampleSetTime(nint sample, long time) =>
        Check(((delegate* unmanaged[Stdcall]<nint, long, int>)Slot(sample, 36))(sample, time), "IMFSample.SetSampleTime");

    public static void SampleSetDuration(nint sample, long duration) =>
        Check(((delegate* unmanaged[Stdcall]<nint, long, int>)Slot(sample, 38))(sample, duration), "IMFSample.SetSampleDuration");

    public static void SampleAddBuffer(nint sample, nint buffer) =>
        Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(sample, 42))(sample, buffer), "IMFSample.AddBuffer");

    // ---- IMFMediaBuffer ----

    public static byte* BufferLock(nint buffer)
    {
        byte* data;
        uint max, current;
        Check(((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, &max, &current), "IMFMediaBuffer.Lock");
        return data;
    }

    public static void BufferUnlock(nint buffer) =>
        Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(buffer, 4))(buffer), "IMFMediaBuffer.Unlock");

    public static void BufferSetCurrentLength(nint buffer, int length) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(buffer, 6))(buffer, (uint)length), "IMFMediaBuffer.SetCurrentLength");
}
