using ScreenCapture.Interop;
using ScreenCapture.Localization;
using ScreenCapture.Storage;

namespace ScreenCapture;

internal static class Program
{
    private const string MutexName = @"Local\ScreenCapture_SingleInstance_7F3C2A91";

    /// <summary>Broadcast by a second instance to ask the running one to show itself.</summary>
    public const string ShowInstanceMessageName = "ScreenCapture_ShowInstance_7F3C2A91";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Let the running instance take the foreground, then ask it to show its window.
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            uint message = NativeMethods.RegisterWindowMessage(ShowInstanceMessageName);
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, message, 0, 0);
            return;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportUnhandled(e.Exception, "Unexpected error on the UI thread");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ErrorLog.Write(ex, "Unhandled exception (process terminating)");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLog.Write(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

#if DEBUG
        // Diagnostics only: headless recording of a fixed area, with a trace file.
        if (Environment.GetEnvironmentVariable("SCREENCAPTURE_TEST_RECORD") is { Length: > 0 } testSpec)
        {
            RunRecordingSelfTest(testSpec);
            return;
        }
#endif

        AppSettings settings = SettingsStore.Load();
        settings.Language ??= Strings.DetectSystemLanguage();
        Strings.Current = settings.Language.Value;
        Forms.UiTheme.Mode = Enum.IsDefined(settings.Theme) ? settings.Theme : Forms.ThemeMode.Light;
        bool startMinimized = args.Any(a => string.Equals(a, StartupRegistration.MinimizedArgument, StringComparison.OrdinalIgnoreCase));

        Application.Run(new MainForm(settings, startMinimized));
        GC.KeepAlive(mutex);
    }

#if DEBUG
    /// <summary>
    /// spec = "seconds;systemAudio(0/1);microphone(0/1);x,y,w,h;outputPath[;P:deviceId|I:deviceId|A:processName…]".
    /// The trace file also lists the audio devices found.
    /// </summary>
    private static void RunRecordingSelfTest(string spec)
    {
        string[] parts = spec.Split(';');
        int seconds = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        int[] r = parts[3].Split(',').Select(v => int.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        string output = parts[4];
        Recording.RecorderTrace.FilePath = Path.ChangeExtension(output, ".trace.txt");
        try
        {
            List<Recording.AudioDeviceInfo>? devices = Recording.AudioDevices.TryGetDevices();
            Recording.RecorderTrace.Write(devices is null
                ? "Devices: timeout"
                : "Devices:\n" + string.Join("\n", devices.Select(d => $"  {d.Kind} {(d.IsDefault ? "*" : " ")} {d.Name} | {d.Id}")));

            var sources = new List<Recording.AudioSourceSetting>();
            if (parts[1] == "1") sources.Add(new(Recording.AudioSourceKind.Playback, null, null));
            if (parts[2] == "1") sources.Add(new(Recording.AudioSourceKind.Input, null, null));
            if (parts.Length > 5)
            {
                foreach (string item in parts[5].Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kind = item[0] switch { 'P' => Recording.AudioSourceKind.Playback, 'A' => Recording.AudioSourceKind.Application, _ => Recording.AudioSourceKind.Input };
                    sources.Add(new(kind, item[2..], item[2..]));
                }
            }

            var options = new Recording.RecordingOptions(
                new Recording.RecordingArea(new Rectangle(r[0], r[1], r[2], r[3])),
                30, true, sources, output);
            Recording.RecorderTrace.Write("Starting");
            Recording.ScreenRecorder recorder = Recording.ScreenRecorder.StartAsync(options).GetAwaiter().GetResult();
            Recording.RecorderTrace.Write("Started; warnings: " + string.Join(" | ", recorder.Warnings));
            Thread.Sleep(seconds * 1000);
            Recording.RecorderTrace.Write("Stopping");
            recorder.StopAsync().GetAwaiter().GetResult();
            Recording.RecorderTrace.Write("Stopped; elapsed " + recorder.Elapsed);
            recorder.Dispose();
            Recording.RecorderTrace.Write("Done");
        }
        catch (Exception ex)
        {
            Recording.RecorderTrace.Write("FAILED: " + ex);
        }
    }
#endif

    private static void ReportUnhandled(Exception exception, string context)
    {
        ErrorLog.Write(exception, context);
        MessageBox.Show(
            Strings.UnexpectedError(exception.Message, AppPaths.ErrorLogFile),
            "ScreenCapture", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
