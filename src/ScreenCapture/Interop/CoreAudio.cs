using System.Runtime.InteropServices;
using static ScreenCapture.Interop.ComCall;

namespace ScreenCapture.Interop;

/// <summary>WASAPI (Core Audio) functions and vtable calls for shared-mode and loopback capture.</summary>
internal static unsafe partial class CoreAudio
{
    private static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid IID_IAudioCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    public static readonly Guid KSDATAFORMAT_SUBTYPE_PCM = new("00000001-0000-0010-8000-00aa00389b71");
    public static readonly Guid KSDATAFORMAT_SUBTYPE_IEEE_FLOAT = new("00000003-0000-0010-8000-00aa00389b71");

    public const int eRender = 0;
    public const int eCapture = 1;
    public const int eConsole = 0;
    public const uint CLSCTX_ALL = 0x17;
    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    public const uint AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
    public const ushort WAVE_FORMAT_PCM = 1;
    public const ushort WAVE_FORMAT_IEEE_FLOAT = 3;
    public const ushort WAVE_FORMAT_EXTENSIBLE = 0xFFFE;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEX
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEXTENSIBLE
    {
        public WAVEFORMATEX Format;
        public ushort wValidBitsPerSample;
        public uint dwChannelMask;
        public Guid SubFormat;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* instance);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(nint pv);

    public static nint CreateDeviceEnumerator()
    {
        Guid clsid = CLSID_MMDeviceEnumerator, iid = IID_IMMDeviceEnumerator;
        nint result;
        Check(CoCreateInstance(&clsid, 0, CLSCTX_ALL, &iid, &result), "CoCreateInstance(MMDeviceEnumerator)");
        return result;
    }

    /// <summary>IMMDeviceEnumerator.GetDefaultAudioEndpoint. Returns the HRESULT (device may not exist).</summary>
    public static int GetDefaultEndpoint(nint enumerator, int dataFlow, out nint device)
    {
        nint result;
        int hr = ((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Slot(enumerator, 4))(enumerator, dataFlow, eConsole, &result);
        device = hr >= 0 ? result : 0;
        return hr;
    }

    /// <summary>IMMDevice.Activate(IID_IAudioClient).</summary>
    public static nint ActivateAudioClient(nint device)
    {
        Guid iid = IID_IAudioClient;
        nint result;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, nint, nint*, int>)Slot(device, 3))(device, &iid, CLSCTX_ALL, 0, &result), "IMMDevice.Activate");
        return result;
    }

    // ---- IAudioClient ----

    public static void ClientInitialize(nint client, uint flags, long bufferDuration, WAVEFORMATEX* format) =>
        Check(((delegate* unmanaged[Stdcall]<nint, int, uint, long, long, WAVEFORMATEX*, Guid*, int>)Slot(client, 3))(
            client, AUDCLNT_SHAREMODE_SHARED, flags, bufferDuration, 0, format, null), "IAudioClient.Initialize");

    public static WAVEFORMATEX* ClientGetMixFormat(nint client)
    {
        WAVEFORMATEX* format;
        Check(((delegate* unmanaged[Stdcall]<nint, WAVEFORMATEX**, int>)Slot(client, 8))(client, &format), "IAudioClient.GetMixFormat");
        return format;
    }

    public static void ClientStart(nint client) =>
        Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(client, 10))(client), "IAudioClient.Start");

    public static void ClientStop(nint client) =>
        ((delegate* unmanaged[Stdcall]<nint, int>)Slot(client, 11))(client);

    public static nint ClientGetCaptureService(nint client)
    {
        Guid iid = IID_IAudioCaptureClient;
        nint result;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(client, 14))(client, &iid, &result), "IAudioClient.GetService");
        return result;
    }

    // ---- IAudioCaptureClient ----

    public static int CaptureGetBuffer(nint capture, byte** data, uint* frames, uint* flags, ulong* devicePosition, ulong* qpcPosition) =>
        ((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, ulong*, ulong*, int>)Slot(capture, 3))(capture, data, frames, flags, devicePosition, qpcPosition);

    public static void CaptureReleaseBuffer(nint capture, uint frames) =>
        Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(capture, 4))(capture, frames), "IAudioCaptureClient.ReleaseBuffer");

    public static uint CaptureGetNextPacketSize(nint capture)
    {
        uint frames;
        Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(capture, 5))(capture, &frames), "IAudioCaptureClient.GetNextPacketSize");
        return frames;
    }
}
