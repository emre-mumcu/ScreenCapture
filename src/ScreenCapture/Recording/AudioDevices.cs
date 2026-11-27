using System.Text.Json.Serialization;
using ScreenCapture.Interop;
using ScreenCapture.Localization;
using static ScreenCapture.Interop.CoreAudio;

namespace ScreenCapture.Recording;

/// <summary>Playback = what an output device plays (WASAPI loopback); Input = a microphone, line-in, etc.; Application = what one program plays (process loopback; DeviceId = process name).</summary>
internal enum AudioSourceKind
{
    Playback,
    Input,
    Application,
}

/// <summary>
/// One audio source chosen for videos. <see cref="DeviceId"/> null = the Windows default device of that kind
/// (follows the default when it changes). <see cref="Name"/> is kept for messages when the device is unplugged.
/// </summary>
internal sealed record AudioSourceSetting(AudioSourceKind Kind, string? DeviceId, string? Name)
{
    [JsonIgnore]
    public bool IsDefault => DeviceId is null;

    /// <summary>Short name for the status line and messages.</summary>
    [JsonIgnore]
    public string DisplayName => IsDefault
        ? Kind == AudioSourceKind.Playback ? Strings.VideoSystemAudio : Strings.VideoMicrophone
        : Name ?? DeviceId!;

    public bool SameSource(AudioSourceSetting other) =>
        Kind == other.Kind && string.Equals(DeviceId, other.DeviceId, StringComparison.OrdinalIgnoreCase);
}

/// <summary>An active audio endpoint of the system, or (Kind = Application) a program with an audio session; IsDefault then means "playing right now".</summary>
internal sealed record AudioDeviceInfo(AudioSourceKind Kind, string Id, string Name, bool IsDefault);

internal static class AudioDevices
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Lists the active playback and input devices. Runs on an MTA thread with a timeout, because a hung
    /// Windows Audio service can block Core Audio calls. Returns null if the service does not answer in time.
    /// </summary>
    public static List<AudioDeviceInfo>? TryGetDevices()
    {
        Task<List<AudioDeviceInfo>> task = Task.Run(GetDevices);
        try
        {
            return task.Wait(Timeout) ? task.Result : null;
        }
        catch (AggregateException ex)
        {
            Storage.ErrorLog.Write(ex.InnerException ?? ex, "Audio devices could not be listed");
            return [];
        }
    }

    private static List<AudioDeviceInfo> GetDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        nint enumerator = CreateDeviceEnumerator();
        try
        {
            AddDevices(enumerator, AudioSourceKind.Playback, devices);
            AddDevices(enumerator, AudioSourceKind.Input, devices);
            if (IsProcessLoopbackSupported)
            {
                AddApplications(enumerator, devices);
            }
        }
        finally
        {
            ComCall.Release(ref enumerator);
        }

        return devices;
    }

    // ---- Applications ------------------------------------------------------------------

    /// <summary>Programs that have an audio session on any output device, one entry per process name.</summary>
    private static void AddApplications(nint enumerator, List<AudioDeviceInfo> devices)
    {
        var apps = new Dictionary<string, (string Name, bool Playing)>(StringComparer.OrdinalIgnoreCase);
        foreach ((uint pid, int state) in GetSessionsOfAllOutputs(enumerator))
        {
            string? processName = GetProcessName(pid);
            if (processName is null)
            {
                continue;
            }

            bool playing = state == AudioSessionStateActive;
            if (apps.TryGetValue(processName, out var existing))
            {
                apps[processName] = (existing.Name, existing.Playing || playing);
            }
            else
            {
                apps[processName] = (GetProgramName(pid) ?? processName, playing);
            }
        }

        devices.AddRange(apps
            .Select(a => new AudioDeviceInfo(AudioSourceKind.Application, a.Key, a.Value.Name, a.Value.Playing))
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase));
    }

    private static List<(uint ProcessId, int State)> GetSessionsOfAllOutputs(nint enumerator)
    {
        var sessions = new List<(uint, int)>();
        uint ownProcess = (uint)Environment.ProcessId;
        nint collection = EnumEndpoints(enumerator, eRender, DEVICE_STATE_ACTIVE);
        try
        {
            uint count = CollectionCount(collection);
            for (uint i = 0; i < count; i++)
            {
                nint device = CollectionItem(collection, i);
                try
                {
                    sessions.AddRange(GetSessions(device).Where(s =>
                        s.ProcessId != 0 && s.ProcessId != ownProcess && s.State != AudioSessionStateExpired));
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    // a device without session information: skip it
                }
                finally
                {
                    ComCall.Release(ref device);
                }
            }
        }
        finally
        {
            ComCall.Release(ref collection);
        }

        return sessions;
    }

    /// <summary>
    /// Process ids to capture for a saved application (by process name): the processes that own its audio
    /// sessions (e.g. a browser's audio helper), or else its main process. Child processes are included anyway.
    /// </summary>
    public static List<uint> FindApplicationProcesses(string processName)
    {
        var result = new List<uint>();
        nint enumerator = CreateDeviceEnumerator();
        try
        {
            foreach ((uint pid, _) in GetSessionsOfAllOutputs(enumerator))
            {
                if (!result.Contains(pid) && string.Equals(GetProcessName(pid), processName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(pid);
                }
            }
        }
        finally
        {
            ComCall.Release(ref enumerator);
        }

        if (result.Count == 0)
        {
            // Not playing yet: capture the main process (its whole process tree).
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(processName);
            try
            {
                System.Diagnostics.Process? main = processes.FirstOrDefault(p => p.MainWindowHandle != 0) ?? processes.FirstOrDefault();
                if (main is not null)
                {
                    result.Add((uint)main.Id);
                }
            }
            finally
            {
                foreach (System.Diagnostics.Process p in processes) p.Dispose();
            }
        }

        return result;
    }

    private static string? GetProcessName(uint pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null; // exited
        }
    }

    /// <summary>File description of the program ("VLC media player"), or null if it cannot be read.</summary>
    private static string? GetProgramName(uint pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            string? description = process.MainModule?.FileVersionInfo.FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null; // e.g. an elevated process
        }
    }

    private static void AddDevices(nint enumerator, AudioSourceKind kind, List<AudioDeviceInfo> devices)
    {
        int flow = kind == AudioSourceKind.Playback ? eRender : eCapture;
        string? defaultId = null;
        if (GetDefaultEndpoint(enumerator, flow, out nint defaultDevice) >= 0)
        {
            try { defaultId = DeviceGetId(defaultDevice); }
            finally { ComCall.Release(ref defaultDevice); }
        }

        nint collection = EnumEndpoints(enumerator, flow, DEVICE_STATE_ACTIVE);
        try
        {
            uint count = CollectionCount(collection);
            var found = new List<AudioDeviceInfo>();
            for (uint i = 0; i < count; i++)
            {
                nint device = CollectionItem(collection, i);
                try
                {
                    string id = DeviceGetId(device);
                    string name = DeviceGetFriendlyName(device) ?? id;
                    found.Add(new AudioDeviceInfo(kind, id, name, string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase)));
                }
                finally
                {
                    ComCall.Release(ref device);
                }
            }

            devices.AddRange(found.OrderByDescending(d => d.IsDefault).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase));
        }
        finally
        {
            ComCall.Release(ref collection);
        }
    }

    /// <summary>Summary of the chosen sources for the status line and settings ("No audio" when empty).</summary>
    public static string Describe(IReadOnlyList<AudioSourceSetting> sources) =>
        sources.Count == 0 ? Strings.VideoNoAudio : string.Join(" + ", sources.Select(s => s.DisplayName));
}
