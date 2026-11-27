using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenCapture.Storage;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON. Corrupt files fall back to defaults.</summary>
internal static class SettingsStore
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        string path = AppPaths.SettingsFile;
        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), s_options) ?? new AppSettings();
            if (settings.Hotkey is null || !settings.Hotkey.Validate().IsValid)
            {
                settings.Hotkey = Hotkeys.HotkeyDefinition.Default;
            }

            if (!Enum.IsDefined(settings.Mode))
            {
                settings.Mode = Capture.CaptureMode.Region;
            }

            settings.Normalize();

            return settings;
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Settings file could not be read, using defaults");
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        string path = AppPaths.SettingsFile;
        string temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, s_options));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Settings file could not be saved");
            try { File.Delete(temp); } catch { /* ignore */ }
        }
    }
}
