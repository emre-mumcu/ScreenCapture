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

        AppSettings settings = SettingsStore.Load();
        settings.Language ??= Strings.DetectSystemLanguage();
        Strings.Current = settings.Language.Value;
        bool startMinimized = args.Any(a => string.Equals(a, StartupRegistration.MinimizedArgument, StringComparison.OrdinalIgnoreCase));

        Application.Run(new MainForm(settings, startMinimized));
        GC.KeepAlive(mutex);
    }

    private static void ReportUnhandled(Exception exception, string context)
    {
        ErrorLog.Write(exception, context);
        MessageBox.Show(
            Strings.UnexpectedError(exception.Message, AppPaths.ErrorLogFile),
            "ScreenCapture", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
