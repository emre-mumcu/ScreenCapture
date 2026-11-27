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

    public const uint DEVICE_STATE_ACTIVE = 0x1;
    private const ushort VT_LPWSTR = 31;

    // PKEY_Device_FriendlyName {a45c254e-df1c-4efd-8020-67d146a850e0}, 14
    private static readonly Guid FMTID_DeviceFriendlyName = new("a45c254e-df1c-4efd-8020-67d146a850e0");

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public nint pointer;
    }

    [LibraryImport("ole32.dll")]
    private static partial int PropVariantClear(PROPVARIANT* pvar);

    /// <summary>IMMDeviceEnumerator.EnumAudioEndpoints → IMMDeviceCollection.</summary>
    public static nint EnumEndpoints(nint enumerator, int dataFlow, uint stateMask)
    {
        nint result;
        Check(((delegate* unmanaged[Stdcall]<nint, int, uint, nint*, int>)Slot(enumerator, 3))(enumerator, dataFlow, stateMask, &result), "IMMDeviceEnumerator.EnumAudioEndpoints");
        return result;
    }

    /// <summary>IMMDeviceEnumerator.GetDevice. Returns the HRESULT (the device may have been removed).</summary>
    public static int GetDevice(nint enumerator, string id, out nint device)
    {
        nint result;
        int hr;
        fixed (char* p = id)
        {
            hr = ((delegate* unmanaged[Stdcall]<nint, char*, nint*, int>)Slot(enumerator, 5))(enumerator, p, &result);
        }

        device = hr >= 0 ? result : 0;
        return hr;
    }

    public static uint CollectionCount(nint collection)
    {
        uint count;
        Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(collection, 3))(collection, &count), "IMMDeviceCollection.GetCount");
        return count;
    }

    public static nint CollectionItem(nint collection, uint index)
    {
        nint result;
        Check(((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(collection, 4))(collection, index, &result), "IMMDeviceCollection.Item");
        return result;
    }

    /// <summary>IMMDevice.GetId: the persistent endpoint id string.</summary>
    public static string DeviceGetId(nint device)
    {
        char* id;
        Check(((delegate* unmanaged[Stdcall]<nint, char**, int>)Slot(device, 5))(device, &id), "IMMDevice.GetId");
        try
        {
            return new string(id);
        }
        finally
        {
            CoTaskMemFree((nint)id);
        }
    }

    public static uint DeviceGetState(nint device)
    {
        uint state;
        Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(device, 6))(device, &state), "IMMDevice.GetState");
        return state;
    }

    /// <summary>Display name of the endpoint (e.g. "Speakers (Realtek(R) Audio)"), or null.</summary>
    public static string? DeviceGetFriendlyName(nint device)
    {
        nint store;
        if (((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)Slot(device, 4))(device, 0 /* STGM_READ */, &store) < 0)
        {
            return null;
        }

        try
        {
            var key = new PROPERTYKEY { fmtid = FMTID_DeviceFriendlyName, pid = 14 };
            PROPVARIANT value = default;
            if (((delegate* unmanaged[Stdcall]<nint, PROPERTYKEY*, PROPVARIANT*, int>)Slot(store, 5))(store, &key, &value) < 0)
            {
                return null;
            }

            try
            {
                return value.vt == VT_LPWSTR && value.pointer != 0 ? new string((char*)value.pointer) : null;
            }
            finally
            {
                PropVariantClear(&value);
            }
        }
        finally
        {
            ComCall.Release(ref store);
        }
    }

    // ---- Audio sessions (which processes play audio) -----------------------------------

    private static readonly Guid IID_IAudioSessionManager2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private static readonly Guid IID_IAudioSessionControl2 = new("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d");

    /// <summary>AudioSessionState: 0 inactive, 1 active (playing), 2 expired.</summary>
    public const int AudioSessionStateActive = 1;
    public const int AudioSessionStateExpired = 2;

    /// <summary>Audio sessions of a render device as (process id, state); the system sounds session is left out.</summary>
    public static List<(uint ProcessId, int State)> GetSessions(nint device)
    {
        var sessions = new List<(uint, int)>();
        nint manager = 0, enumerator = 0;
        try
        {
            Guid iid = IID_IAudioSessionManager2;
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, nint, nint*, int>)Slot(device, 3))(device, &iid, CLSCTX_ALL, 0, &manager), "IMMDevice.Activate(IAudioSessionManager2)");
            nint e;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(manager, 5))(manager, &e), "IAudioSessionManager2.GetSessionEnumerator");
            enumerator = e;
            int count;
            Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(enumerator, 3))(enumerator, &count), "IAudioSessionEnumerator.GetCount");
            for (int i = 0; i < count; i++)
            {
                nint control = 0, control2 = 0;
                try
                {
                    if (((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)Slot(enumerator, 4))(enumerator, i, &control) < 0)
                    {
                        continue;
                    }

                    control2 = ComCall.QueryInterface(control, IID_IAudioSessionControl2);
                    int state;
                    uint pid;
                    if (((delegate* unmanaged[Stdcall]<nint, int*, int>)Slot(control2, 3))(control2, &state) < 0 ||
                        ((delegate* unmanaged[Stdcall]<nint, uint*, int>)Slot(control2, 14))(control2, &pid) < 0 ||
                        ((delegate* unmanaged[Stdcall]<nint, int>)Slot(control2, 15))(control2) == 0 /* S_OK = system sounds */)
                    {
                        continue;
                    }

                    sessions.Add((pid, state));
                }
                catch (COMException)
                {
                    // skip this session
                }
                finally
                {
                    ComCall.Release(ref control2);
                    ComCall.Release(ref control);
                }
            }
        }
        finally
        {
            ComCall.Release(ref enumerator);
            ComCall.Release(ref manager);
        }

        return sessions;
    }

    // ---- Process loopback (audio of one application) ------------------------------------

    private static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid IID_IActivateAudioInterfaceCompletionHandler = new("41D949AB-9862-444A-80F6-C261334DA5EB");
    private static readonly Guid IID_IAgileObject = new("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");
    private const string VirtualAudioDeviceProcessLoopback = "VAD\\Process_Loopback";
    public const uint AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const uint AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = 0x80000000;
    public const uint AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000;
    public const uint AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR = 0x4;

    /// <summary>Process loopback needs Windows 10 build 20348 or later (Windows 11, Server 2022).</summary>
    public static bool IsProcessLoopbackSupported => Environment.OSVersion.Version.Build >= 20348;

    [LibraryImport("mmdevapi.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int ActivateAudioInterfaceAsync(string deviceInterfacePath, Guid* riid, void* activationParams, nint completionHandler, nint* operation);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateEventW")]
    public static partial nint CreateEvent(nint attributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, nint name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    /// <summary>Minimal COM object implementing IActivateAudioInterfaceCompletionHandler (agile, static lifetime).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct CompletionHandler
    {
        public nint Vtable;
        public nint Event;
    }

    private static readonly nint* s_handlerVtable = CreateHandlerVtable();

    private static nint* CreateHandlerVtable()
    {
        var vtable = (nint*)NativeMemory.Alloc((nuint)(4 * sizeof(nint)));
        vtable[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&HandlerQueryInterface;
        vtable[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&HandlerAddRef;
        vtable[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&HandlerRelease;
        vtable[3] = (nint)(delegate* unmanaged[Stdcall]<nint, nint, int>)&HandlerActivateCompleted;
        return vtable;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static int HandlerQueryInterface(nint self, Guid* iid, nint* result)
    {
        if (*iid == IID_IUnknown || *iid == IID_IActivateAudioInterfaceCompletionHandler || *iid == IID_IAgileObject)
        {
            *result = self;
            return 0;
        }

        *result = 0;
        return unchecked((int)0x80004002); // E_NOINTERFACE
    }

    // The object's memory is owned by ActivateProcessLoopbackClient, not by COM reference counting.
    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static uint HandlerAddRef(nint self) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static uint HandlerRelease(nint self) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static int HandlerActivateCompleted(nint self, nint operation)
    {
        SetEvent(((CompletionHandler*)self)->Event); // last access to self
        return 0;
    }

    /// <summary>
    /// Activates an IAudioClient that captures what process <paramref name="processId"/> and its child processes play.
    /// Throws <see cref="TimeoutException"/> if Windows does not complete the activation in time.
    /// </summary>
    public static nint ActivateProcessLoopbackClient(uint processId, TimeSpan timeout)
    {
        // AUDIOCLIENT_ACTIVATION_PARAMS { PROCESS_LOOPBACK = 1, { TargetProcessId, INCLUDE_TARGET_PROCESS_TREE = 0 } }
        var parameters = (uint*)NativeMemory.AllocZeroed(12);
        parameters[0] = 1;
        parameters[1] = processId;
        parameters[2] = 0;

        // PROPVARIANT { vt = VT_BLOB (65), blob = { cbSize, pBlobData } }
        var variant = (byte*)NativeMemory.AllocZeroed(24);
        *(ushort*)variant = 65;
        *(uint*)(variant + 8) = 12;
        *(nint*)(variant + 16) = (nint)parameters;

        var handler = (CompletionHandler*)NativeMemory.AllocZeroed((nuint)sizeof(CompletionHandler));
        handler->Vtable = (nint)s_handlerVtable;
        handler->Event = CreateEvent(0, true, false, 0);

        nint operation = 0;
        bool pending = false, completed = false;
        try
        {
            Guid iid = IID_IAudioClient;
            Check(ActivateAudioInterfaceAsync(VirtualAudioDeviceProcessLoopback, &iid, variant, (nint)handler, &operation), "ActivateAudioInterfaceAsync");
            pending = true;
            if (WaitForSingleObject(handler->Event, (uint)timeout.TotalMilliseconds) != 0)
            {
                throw new TimeoutException("Process loopback activation did not complete.");
            }

            completed = true;
            int activateResult;
            nint unknown;
            Check(((delegate* unmanaged[Stdcall]<nint, int*, nint*, int>)Slot(operation, 3))(operation, &activateResult, &unknown), "IActivateAudioInterfaceAsyncOperation.GetActivateResult");
            Check(activateResult, "Process loopback activation");
            try
            {
                return ComCall.QueryInterface(unknown, IID_IAudioClient);
            }
            finally
            {
                ComCall.Release(ref unknown);
            }
        }
        finally
        {
            ComCall.Release(ref operation);
            if (!pending || completed)
            {
                // Otherwise Windows may still call the handler later: leave its memory (a few bytes) alive.
                CloseHandle(handler->Event);
                NativeMemory.Free(handler);
                NativeMemory.Free(variant);
                NativeMemory.Free(parameters);
            }
        }
    }

    /// <summary>IAudioClient.SetEventHandle.</summary>
    public static void ClientSetEventHandle(nint client, nint handle) =>
        Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)Slot(client, 13))(client, handle), "IAudioClient.SetEventHandle");

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
