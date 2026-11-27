using System.Globalization;

namespace ScreenCapture.Localization;

public enum AppLanguage
{
    Turkish,
    English,
    German,
}

/// <summary>
/// All user-visible texts in Turkish, English and German. Each property returns the text in
/// <see cref="Current"/>; changing the language raises <see cref="LanguageChanged"/> so open UI can refresh.
/// </summary>
internal static class Strings
{
    private static AppLanguage s_current = AppLanguage.English;

    public static event EventHandler? LanguageChanged;

    public static AppLanguage Current
    {
        get => s_current;
        set
        {
            if (s_current == value)
            {
                return;
            }

            s_current = value;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>Turkish or German if Windows uses it, English otherwise.</summary>
    public static AppLanguage DetectSystemLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "tr" => AppLanguage.Turkish,
        "de" => AppLanguage.German,
        _ => AppLanguage.English,
    };

    /// <summary>Language names are always shown in their own language.</summary>
    public static string NativeName(AppLanguage language) => language switch
    {
        AppLanguage.Turkish => "Türkçe",
        AppLanguage.German => "Deutsch",
        _ => "English",
    };

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Current switch
    {
        AppLanguage.Turkish => "tr-TR",
        AppLanguage.German => "de-DE",
        _ => "en-US",
    });

    private static string T(string tr, string en, string de) => s_current switch
    {
        AppLanguage.Turkish => tr,
        AppLanguage.German => de,
        _ => en,
    };

    // ---- Common ---------------------------------------------------------------

    public static string Ok => T("Tamam", "OK", "OK");
    public static string Cancel => T("İptal", "Cancel", "Abbrechen");
    public static string Save => T("Kaydet", "Save", "Speichern");

    // ---- Capture modes ----------------------------------------------------------

    public static string ModeFullScreen => T("Tam Ekran", "Full Screen", "Vollbild");
    public static string ModeWindow => T("Pencere", "Window", "Fenster");
    public static string ModeRegion => T("Serbest Alan", "Region", "Bereich");
    public static string ModeFullScreenTip => T("Bir monitörün veya tüm monitörlerin tamamını yakalar", "Captures a whole monitor or all monitors", "Nimmt einen ganzen Monitor oder alle Monitore auf");
    public static string ModeWindowTip => T("Bir pencereyi yakalar", "Captures a single window", "Nimmt ein einzelnes Fenster auf");
    public static string ModeRegionTip => T("Ekranın seçilen bir bölümünü yakalar", "Captures a selected part of the screen", "Nimmt einen ausgewählten Bildschirmbereich auf");

    // ---- Main window ------------------------------------------------------------

    public static string SectionMode => T("Yakalama modu", "Capture mode", "Aufnahmemodus");
    public static string SectionArea => T("Yakalama alanı", "Capture area", "Aufnahmebereich");
    public static string SectionHotkey => T("Kısayol", "Shortcut", "Tastenkürzel");
    public static string Ready => T("Hazır", "Ready", "Bereit");
    public static string DefineArea => T("Alan Belirle…", "Set Area…", "Bereich festlegen…");
    public static string DefineAreaTip => T("Yakalama alanını kaydeder. Sonraki tüm yakalamalarda bu alan seçim yapılmadan kullanılır.",
        "Saves the capture area. All following captures use it without asking.",
        "Speichert den Aufnahmebereich. Alle folgenden Aufnahmen verwenden ihn ohne Nachfrage.");
    public static string ClearArea => T("Temizle", "Clear", "Löschen");
    public static string ClearAreaTip => T("Kayıtlı alanı siler; her yakalamada yeniden seçim yapılır.",
        "Removes the saved area; you choose again at every capture.",
        "Entfernt den gespeicherten Bereich; bei jeder Aufnahme wird neu ausgewählt.");
    public static string ChangeHotkey => T("Değiştir…", "Change…", "Ändern…");
    public static string ChangeHotkeyTip => T("Yakalama kısayolunu değiştir (klavye veya fare)", "Change the capture shortcut (keyboard or mouse)", "Aufnahme-Tastenkürzel ändern (Tastatur oder Maus)");
    public static string CaptureButton => T("Yakala", "Capture", "Aufnehmen");
    public static string CaptureButtonTip => T("Seçili mod ve alanla şimdi yakala", "Capture now with the selected mode and area", "Jetzt mit gewähltem Modus und Bereich aufnehmen");
    public static string FolderButton => T("Klasör", "Folder", "Ordner");
    public static string FolderButtonTip => T("Captures klasörünü aç", "Open the Captures folder", "Ordner „Captures“ öffnen");
    public static string SettingsTip => T("Ayarlar", "Settings", "Einstellungen");

    public static string TargetSaved => T("Kayıtlı – seçim yapılmadan kullanılır", "Saved – used without asking", "Gespeichert – wird direkt verwendet");
    public static string TargetDefault => T("Varsayılan davranış", "Default behavior", "Standardverhalten");
    public static string TargetEachTime => T("Her seferinde seçim yapılır", "You choose at every capture", "Auswahl bei jeder Aufnahme");
    public static string TargetSavedTip => T("Yakala düğmesi ve kısayol bu alanı doğrudan yakalar.", "The Capture button and the shortcut capture this area directly.", "Die Schaltfläche „Aufnehmen“ und das Tastenkürzel nehmen diesen Bereich direkt auf.");
    public static string TargetNoneTip => T("Sabit bir alan kaydetmek için 'Alan Belirle…' düğmesine basın.", "Click 'Set Area…' to save a fixed area.", "Klicken Sie auf „Bereich festlegen…“, um einen festen Bereich zu speichern.");

    public static string HotkeyNone => T("(atanmadı)", "(not set)", "(nicht festgelegt)");
    public static string HotkeyInactive => T("Kısayol etkin değil", "Shortcut is not active", "Tastenkürzel ist nicht aktiv");
    public static string HotkeyKindKeyboard => T("Klavye kısayolu – her uygulamada çalışır", "Keyboard shortcut – works in every app", "Tastenkürzel – funktioniert in jeder App");
    public static string HotkeyKindMouse => T("Fare kısayolu – her uygulamada çalışır", "Mouse shortcut – works in every app", "Maustaste – funktioniert in jeder App");

    // Status line
    public static string StatusCancelled => T("İptal edildi", "Cancelled", "Abgebrochen");
    public static string StatusAreaCancelled => T("Alan belirleme iptal edildi", "Setting the area was cancelled", "Bereichsauswahl abgebrochen");
    public static string StatusAreaSaved => T("Yakalama alanı kaydedildi", "Capture area saved", "Aufnahmebereich gespeichert");
    public static string StatusAreaCleared => T("Kayıtlı alan temizlendi", "Saved area cleared", "Gespeicherter Bereich gelöscht");
    public static string StatusSettingsSaved => T("Ayarlar kaydedildi", "Settings saved", "Einstellungen gespeichert");
    public static string StatusFolderFailed => T("Klasör açılamadı", "Could not open the folder", "Ordner konnte nicht geöffnet werden");
    public static string StatusSaved(string file) => T($"✓ Kaydedildi: {file}", $"✓ Saved: {file}", $"✓ Gespeichert: {file}");
    public static string StatusSavedTip(string path) => T($"{path}\nKlasörde göstermek için tıklayın", $"{path}\nClick to show in folder", $"{path}\nKlicken, um im Ordner anzuzeigen");
    public static string StatusNewHotkey(string hotkey) => T($"Yeni kısayol: {hotkey}", $"New shortcut: {hotkey}", $"Neues Tastenkürzel: {hotkey}");
    public static string StatusAreaFailed(string message) => T($"Alan belirlenemedi: {message}", $"Could not set the area: {message}", $"Bereich konnte nicht festgelegt werden: {message}");
    public static string StatusCaptureFailed(string message) => T($"Yakalama başarısız: {message}", $"Capture failed: {message}", $"Aufnahme fehlgeschlagen: {message}");
    public static string StatusSaveFailed(string message) => T($"Kaydedilemedi: {message}", $"Could not save: {message}", $"Speichern fehlgeschlagen: {message}");

    // Notifications
    public static string NotifySavedTitle => T("Ekran görüntüsü kaydedildi", "Screenshot saved", "Screenshot gespeichert");
    public static string NotifyCaptureFailedTitle => T("Yakalanamadı", "Capture failed", "Aufnahme fehlgeschlagen");
    public static string NotifySaveFailedTitle => T("Kaydedilemedi", "Could not save", "Speichern fehlgeschlagen");

    // Tray menu
    public static string TrayShow => T("Göster", "Show", "Anzeigen");
    public static string TrayCaptureSelected => T("Yakala (seçili mod)", "Capture (selected mode)", "Aufnehmen (gewählter Modus)");
    public static string TrayCaptureFullScreen => T("Tam Ekran Yakala", "Capture Full Screen", "Vollbild aufnehmen");
    public static string TrayCaptureWindow => T("Pencere Yakala", "Capture Window", "Fenster aufnehmen");
    public static string TrayCaptureRegion => T("Alan Yakala", "Capture Region", "Bereich aufnehmen");
    public static string TrayOpenFolder => T("Klasörü Aç", "Open Folder", "Ordner öffnen");
    public static string TraySettings => T("Ayarlar…", "Settings…", "Einstellungen…");
    public static string TrayAbout => T("Hakkında…", "About…", "Über…");
    public static string TrayExit => T("Çıkış", "Exit", "Beenden");

    // Messages
    public static string HotkeyActivationFailed(string hotkey, string? error) => T(
        $"Kayıtlı kısayol ({hotkey}) etkinleştirilemedi:\n{error}",
        $"The saved shortcut ({hotkey}) could not be activated:\n{error}",
        $"Das gespeicherte Tastenkürzel ({hotkey}) konnte nicht aktiviert werden:\n{error}");
    public static string UsingDefaultHotkey(string hotkey) => T(
        $"\n\nVarsayılan kısayol ({hotkey}) kullanılıyor.",
        $"\n\nUsing the default shortcut ({hotkey}).",
        $"\n\nDas Standard-Tastenkürzel ({hotkey}) wird verwendet.");
    public static string ChooseNewHotkey => T(
        "\n\nLütfen 'Değiştir…' düğmesiyle yeni bir kısayol seçin.",
        "\n\nPlease choose a new shortcut with 'Change…'.",
        "\n\nBitte wählen Sie mit „Ändern…“ ein neues Tastenkürzel.");
    public static string CapturesFallback(string directory) => T(
        $"Uygulama klasörüne yazılamıyor. Ekran görüntüleri şu klasöre kaydedilecek:\n{directory}",
        $"The application folder is not writable. Screenshots will be saved to:\n{directory}",
        $"Der Programmordner ist schreibgeschützt. Screenshots werden hier gespeichert:\n{directory}");
    public static string HotkeyAssignFailedTitle => T("Kısayol atanamadı", "Shortcut not assigned", "Tastenkürzel nicht zugewiesen");
    public static string PrintScreenConflict => T(
        "Windows'ta \"PrtScn tuşu Ekran Alıntısı Aracını açsın\" ayarı açık.\n\n" +
        "ScreenCapture tuşu kendisi yakalar, ancak çakışma yaşamamak için bu ayarı kapatmanız önerilir:\n" +
        "Ayarlar > Erişilebilirlik > Klavye > \"Ekran alıntısını açmak için Print Screen tuşunu kullan\"",
        "Windows is set to open the Snipping Tool with the Print Screen key.\n\n" +
        "ScreenCapture handles the key itself, but turning that option off avoids conflicts:\n" +
        "Settings > Accessibility > Keyboard > \"Use the Print screen key to open screen capture\"",
        "Windows öffnet mit der Druck-Taste das Snipping Tool.\n\n" +
        "ScreenCapture verarbeitet die Taste selbst, zur Vermeidung von Konflikten sollte die Option aber deaktiviert werden:\n" +
        "Einstellungen > Barrierefreiheit > Tastatur > „Bildschirmausschnitt mit der Taste ‚Druck‘ öffnen“");
    public static string StartupWriteFailed => T(
        "\"Windows ile başlat\" ayarı kayıt defterine yazılamadı.",
        "The \"Start with Windows\" setting could not be written to the registry.",
        "Die Einstellung „Mit Windows starten“ konnte nicht in die Registrierung geschrieben werden.");
    public static string UnexpectedError(string message, string logPath) => T(
        $"Beklenmeyen bir hata oluştu ve kayıt altına alındı:\n\n{message}\n\nAyrıntılar: {logPath}",
        $"An unexpected error occurred and was logged:\n\n{message}\n\nDetails: {logPath}",
        $"Ein unerwarteter Fehler ist aufgetreten und wurde protokolliert:\n\n{message}\n\nDetails: {logPath}");

    // ---- Capture targets --------------------------------------------------------

    public static string MonitorName(int number, int width, int height, bool primary) => T(
        $"Monitör {number} – {width}x{height}" + (primary ? " (birincil)" : ""),
        $"Monitor {number} – {width}x{height}" + (primary ? " (primary)" : ""),
        $"Monitor {number} – {width}x{height}" + (primary ? " (Hauptmonitor)" : ""));
    public static string MonitorUnderCursor => T("Fare imlecinin bulunduğu monitör", "Monitor under the mouse pointer", "Monitor unter dem Mauszeiger");
    public static string DefaultSuffix => T(" (varsayılan)", " (default)", " (Standard)");
    public static string AllMonitors(int width, int height) => T($"Tüm monitörler – {width}x{height}", $"All monitors – {width}x{height}", $"Alle Monitore – {width}x{height}");
    public static string NotConnected(string? device) => T($"{device} (bağlı değil)", $"{device} (not connected)", $"{device} (nicht verbunden)");
    public static string ActiveWindowEachTime => T("Etkin pencere (her seferinde)", "Active window (every time)", "Aktives Fenster (jedes Mal)");
    public static string PickWindowEachTime => T("Her yakalamada pencere seçilir", "Pick a window at every capture", "Fensterauswahl bei jeder Aufnahme");
    public static string PickRegionEachTime => T("Her yakalamada alan seçilir", "Draw a region at every capture", "Bereichsauswahl bei jeder Aufnahme");
    public static string RegionDescription(int w, int h, int x, int y) => T($"{w}x{h} px  •  konum: {x}, {y}", $"{w}x{h} px  •  position: {x}, {y}", $"{w}x{h} px  •  Position: {x}, {y}");

    public static string MonitorMissing(string? device) => T(
        $"Kayıtlı monitör ({device}) bağlı değil. Alanı yeniden belirleyin.",
        $"The saved monitor ({device}) is not connected. Please set the area again.",
        $"Der gespeicherte Monitor ({device}) ist nicht verbunden. Bitte Bereich neu festlegen.");
    public static string RegionOffScreen => T(
        "Kayıtlı alan şu anki ekranların dışında kalıyor. Alanı yeniden belirleyin.",
        "The saved area is outside the current screens. Please set the area again.",
        "Der gespeicherte Bereich liegt außerhalb der Bildschirme. Bitte Bereich neu festlegen.");
    public static string WindowOffScreen => T(
        "Pencere ekranın dışında ve içeriği alınamadı.",
        "The window is off-screen and its content could not be read.",
        "Das Fenster liegt außerhalb des Bildschirms; der Inhalt konnte nicht gelesen werden.");
    public static string WindowMinimized(string process) => T(
        $"Kayıtlı pencere ({process}) simge durumuna küçültülmüş; yakalanamıyor.",
        $"The saved window ({process}) is minimized and cannot be captured.",
        $"Das gespeicherte Fenster ({process}) ist minimiert und kann nicht aufgenommen werden.");
    public static string WindowMissing(string process) => T(
        $"Kayıtlı pencere ({process}) bulunamadı. Pencere kapatılmış olabilir; alanı yeniden belirleyin.",
        $"The saved window ({process}) was not found. It may have been closed; please set the area again.",
        $"Das gespeicherte Fenster ({process}) wurde nicht gefunden. Es wurde evtl. geschlossen; bitte Bereich neu festlegen.");
    public static string EmptyCaptureArea => T("Yakalama alanı boş.", "The capture area is empty.", "Der Aufnahmebereich ist leer.");
    public static string SelectionOffScreen => T("Seçilen alan ekranın dışında.", "The selected area is off-screen.", "Der gewählte Bereich liegt außerhalb des Bildschirms.");

    // ---- Overlays -----------------------------------------------------------------

    public static string RegionOverlayHint => T("Alanı seçmek için sürükleyin  •  Esc / sağ tık: iptal", "Drag to select an area  •  Esc / right-click: cancel", "Zum Auswählen ziehen  •  Esc / Rechtsklick: Abbrechen");
    public static string WindowOverlayHint => T("Yakalanacak pencereye tıklayın  •  Esc / sağ tık: iptal", "Click the window to capture  •  Esc / right-click: cancel", "Auf das Fenster klicken  •  Esc / Rechtsklick: Abbrechen");

    // ---- Hotkeys --------------------------------------------------------------------

    public static string HotkeyErrorNoKey => T(
        "Geçerli bir tuş seçilmedi. Değiştirici tuşlar (Ctrl, Alt, Shift, Win) tek başına kullanılamaz.",
        "No valid key selected. Modifier keys (Ctrl, Alt, Shift, Win) cannot be used alone.",
        "Keine gültige Taste gewählt. Zusatztasten (Strg, Alt, Umschalt, Win) sind allein nicht erlaubt.");
    public static string HotkeyErrorNeedsModifier => T(
        "Bu tuş tek başına kullanılamaz. Ctrl, Alt, Shift veya Win ile birlikte kullanın (tek başına yalnızca PrintScreen, F1–F24, Pause ve ScrollLock kabul edilir).",
        "This key cannot be used alone. Combine it with Ctrl, Alt, Shift or Win (only PrintScreen, F1–F24, Pause and ScrollLock work alone).",
        "Diese Taste ist allein nicht erlaubt. Kombinieren Sie sie mit Strg, Alt, Umschalt oder Win (allein nur Druck, F1–F24, Pause und Rollen).");
    public static string HotkeyErrorShiftTyping => T(
        "Shift + yazı tuşu kombinasyonu normal yazmayı engeller. Ctrl, Alt veya Win ekleyin.",
        "Shift + a typing key would block normal typing. Add Ctrl, Alt or Win.",
        "Umschalt + Schreibtaste würde das normale Tippen blockieren. Fügen Sie Strg, Alt oder Win hinzu.");
    public static string HotkeyErrorNoMouseButton => T("Geçerli bir fare tuşu seçilmedi.", "No valid mouse button selected.", "Keine gültige Maustaste gewählt.");
    public static string HotkeyErrorPlainClick => T(
        "Değiştiricisiz sol veya sağ tık kısayol olarak atanamaz; bilgisayar kullanılamaz hale gelir.",
        "A plain left or right click cannot be a shortcut; the computer would become unusable.",
        "Ein einfacher Links- oder Rechtsklick ist nicht erlaubt; der Computer wäre sonst unbenutzbar.");
    public static string HotkeyWarningLeftClick => T(
        "Uyarı: Bu kombinasyon bazı uygulamalardaki Ctrl/Shift+tık seçim işlevlerini engelleyebilir.",
        "Warning: this combination may block Ctrl/Shift+click selection in some applications.",
        "Hinweis: Diese Kombination kann Strg/Umschalt+Klick-Auswahl in manchen Programmen blockieren.");
    public static string HotkeyWarningRightClick => T(
        "Uyarı: Bu kombinasyon bazı uygulamalardaki sağ tık menüsü işlevlerini engelleyebilir.",
        "Warning: this combination may block context menus in some applications.",
        "Hinweis: Diese Kombination kann Kontextmenüs in manchen Programmen blockieren.");
    public static string HotkeyInUse(string hotkey) => T(
        $"'{hotkey}' kısayolu başka bir uygulama veya Windows tarafından kullanılıyor. Farklı bir kısayol seçin.",
        $"The shortcut '{hotkey}' is already used by another application or Windows. Please choose a different one.",
        $"Das Tastenkürzel „{hotkey}“ wird bereits von einem anderen Programm oder Windows verwendet. Bitte ein anderes wählen.");
    public static string HotkeyRegisterFailed(string hotkey, int code) => T(
        $"'{hotkey}' kısayolu kaydedilemedi (Win32 hata {code}).",
        $"The shortcut '{hotkey}' could not be registered (Win32 error {code}).",
        $"Das Tastenkürzel „{hotkey}“ konnte nicht registriert werden (Win32-Fehler {code}).");
    public static string KeyboardHookFailed(int code) => T($"Klavye kancası kurulamadı (Win32 hata {code}).", $"Could not install the keyboard hook (Win32 error {code}).", $"Tastatur-Hook konnte nicht installiert werden (Win32-Fehler {code}).");
    public static string MouseHookFailed(int code) => T($"Fare kancası kurulamadı (Win32 hata {code}).", $"Could not install the mouse hook (Win32 error {code}).", $"Maus-Hook konnte nicht installiert werden (Win32-Fehler {code}).");

    // Key and button names
    public static string ModCtrl => T("Ctrl", "Ctrl", "Strg");
    public static string ModShift => T("Shift", "Shift", "Umschalt");
    public static string KeySpace => T("Boşluk", "Space", "Leertaste");
    public static string KeyMenu => T("Menü", "Menu", "Menü");
    public static string KeyDelete => T("Delete", "Delete", "Entf");
    public static string KeyInsert => T("Insert", "Insert", "Einfg");
    public static string KeyPrintScreen => T("PrintScreen", "PrintScreen", "Druck");
    public static string MouseLeft => T("Sol Tık", "Left Click", "Linksklick");
    public static string MouseRight => T("Sağ Tık", "Right Click", "Rechtsklick");
    public static string MouseMiddle => T("Orta Tuş", "Middle Button", "Mittlere Taste");
    public static string MouseX1 => T("Fare Geri (X1)", "Mouse Back (X1)", "Maus Zurück (X1)");
    public static string MouseX2 => T("Fare İleri (X2)", "Mouse Forward (X2)", "Maus Vorwärts (X2)");

    // ---- Hotkey dialog --------------------------------------------------------------

    public static string HotkeyDialogTitle => T("Kısayol Ata", "Set Shortcut", "Tastenkürzel festlegen");
    public static string HotkeyDialogInstruction => T(
        "Bir tuş kombinasyonuna basın veya aşağıdaki alanda bir fare tuşuna basın.\nEsc: iptal",
        "Press a key combination, or press a mouse button in the area below.\nEsc: cancel",
        "Drücken Sie eine Tastenkombination oder eine Maustaste im Feld unten.\nEsc: Abbrechen");
    public static string HotkeyDialogMouseArea => T(
        "Fare kısayolu için bu alanda tıklayın\n(Orta tuş, Geri/İleri tuşları; sol/sağ tık için Ctrl/Alt/Shift ile)",
        "Click in this area for a mouse shortcut\n(middle, back/forward buttons; left/right click with Ctrl/Alt/Shift)",
        "Für ein Maus-Kürzel hier klicken\n(mittlere Taste, Zurück/Vorwärts; Links-/Rechtsklick mit Strg/Alt/Umschalt)");
    public static string HotkeyDialogConfirm => T("Kaydetmek için 'Kaydet' düğmesine tıklayın.", "Click 'Save' to apply.", "Zum Übernehmen auf „Speichern“ klicken.");

    // ---- Settings dialog ----------------------------------------------------------------

    public static string SettingsTitle => T("Ayarlar", "Settings", "Einstellungen");
    public static string SettingsLanguage => T("Dil", "Language", "Sprache");
    public static string SettingsGeneral => T("Genel", "General", "Allgemein");
    public static string SettingsMinimizeToTray => T("Küçültüldüğünde sistem tepsisine gizle", "Hide in the notification area when minimized", "Beim Minimieren in den Infobereich ausblenden");
    public static string SettingsAlwaysOnTop => T("Her zaman üstte", "Always on top", "Immer im Vordergrund");
    public static string SettingsStartWithWindows => T("Windows ile başlat (tepside küçültülmüş)", "Start with Windows (minimized to tray)", "Mit Windows starten (im Infobereich)");
    public static string SettingsCapture => T("Yakalama", "Capture", "Aufnahme");
    public static string SettingsActiveWindow => T("Pencere (alan belirlenmemişse): seçim yapmadan etkin pencereyi yakala", "Window (no saved area): capture the active window without picking", "Fenster (ohne gespeicherten Bereich): aktives Fenster ohne Auswahl aufnehmen");
    public static string SettingsIncludeCursor => T("Fare imlecini görüntüye dahil et", "Include the mouse pointer", "Mauszeiger mit aufnehmen");
    public static string SettingsCopyToClipboard => T("Görüntüyü ayrıca panoya kopyala", "Also copy the image to the clipboard", "Bild zusätzlich in die Zwischenablage kopieren");
    public static string SettingsFeedback => T("Bildirim ve kısayol", "Feedback and shortcut", "Rückmeldung und Tastenkürzel");
    public static string SettingsPlaySound => T("Kayıtta ses çal", "Play a sound when saved", "Beim Speichern Ton abspielen");
    public static string SettingsShowNotification => T("Kayıtta tepsi bildirimi göster", "Show a notification when saved", "Beim Speichern Benachrichtigung anzeigen");
    public static string SettingsSwallowMouse => T("Fare kısayol tıklamasını diğer uygulamalara iletme", "Do not pass the mouse shortcut click to other apps", "Maus-Kürzel nicht an andere Programme weitergeben");
    public static string SettingsCapturesFolder => T("Kayıt klasörü", "Save folder", "Speicherordner");
    public static string SettingsTheme => T("Görünüm", "Appearance", "Darstellung");
    public static string ThemeLight => T("Açık", "Light", "Hell");
    public static string ThemeDark => T("Koyu", "Dark", "Dunkel");
    public static string ThemeSystem => T("Windows ayarını kullan", "Use Windows setting", "Windows-Einstellung verwenden");

    // ---- Video recording -----------------------------------------------------------------

    public static string SectionOutput => T("Çıktı", "Output", "Ausgabe");
    public static string OutputImage => T("Resim", "Image", "Bild");
    public static string OutputVideo => T("Video", "Video", "Video");
    public static string OutputImageTip => T("Ekran görüntüsünü PNG olarak kaydeder", "Saves a screenshot as PNG", "Speichert einen Screenshot als PNG");
    public static string OutputVideoTip => T("Seçilen alanı sesli olarak MP4 videoya kaydeder", "Records the selected area as MP4 video with sound", "Nimmt den gewählten Bereich als MP4-Video mit Ton auf");
    public static string StartRecording => T("● Kaydı Başlat", "● Start Recording", "● Aufnahme starten");
    public static string StopRecording(string elapsed) => T($"■ Durdur   {elapsed}", $"■ Stop   {elapsed}", $"■ Stoppen   {elapsed}");
    public static string StartRecordingTip => T("Seçili mod ve alanla video kaydını başlat (kısayol da başlatır/durdurur)", "Start recording the selected mode and area (the shortcut starts/stops too)", "Aufnahme des gewählten Modus und Bereichs starten (das Kürzel startet/stoppt ebenfalls)");
    public static string StopRecordingTip => T("Kaydı durdur ve videoyu kaydet", "Stop and save the video", "Aufnahme beenden und Video speichern");
    public static string StatusRecordingStarting => T("Kayıt başlatılıyor…", "Starting recording…", "Aufnahme wird gestartet…");
    public static string StatusRecording => T("● Kaydediliyor – durdurmak için Durdur'a veya kısayola basın", "● Recording – press Stop or the shortcut to finish", "● Aufnahme läuft – zum Beenden „Stoppen“ oder das Kürzel drücken");
    public static string StatusFinalizing => T("Video kaydediliyor…", "Saving video…", "Video wird gespeichert…");
    public static string StatusRecordingFailed(string message) => T($"Kayıt başarısız: {message}", $"Recording failed: {message}", $"Aufnahme fehlgeschlagen: {message}");
    public static string NotifyVideoSavedTitle => T("Video kaydedildi", "Video saved", "Video gespeichert");
    public static string NotifyRecordingStartedTitle => T("Video kaydı başladı", "Recording started", "Aufnahme gestartet");
    public static string VideoSystemAudio => T("Sistem sesi", "System audio", "Systemton");
    public static string VideoMicrophone => T("Mikrofon", "Microphone", "Mikrofon");
    public static string VideoNoAudio => T("Ses yok", "No audio", "Kein Ton");
    public static string VideoSummary(string audio, int fps) => T($"Ses: {audio}  •  {fps} fps", $"Audio: {audio}  •  {fps} fps", $"Ton: {audio}  •  {fps} fps");
    public static string VideoAudioDeviceMissing(string name) => T(
        $"{name} için cihaz bulunamadı; video bu ses olmadan kaydediliyor.",
        $"No device found for {name}; recording without it.",
        $"Kein Gerät für {name} gefunden; Aufnahme ohne diesen Ton.");
    public static string VideoAudioDeviceFailed(string name) => T(
        $"{name} açılamadı; video bu ses olmadan kaydediliyor.",
        $"{name} could not be opened; recording without it.",
        $"{name} konnte nicht geöffnet werden; Aufnahme ohne diesen Ton.");
    public static string VideoAudioDeviceTimeout(string name) => T(
        $"{name} yanıt vermiyor (Windows Ses hizmeti); video bu ses olmadan kaydediliyor.",
        $"{name} is not responding (Windows Audio service); recording without it.",
        $"{name} reagiert nicht (Windows-Audiodienst); Aufnahme ohne diesen Ton.");
    public static string VideoAreaTooSmall(int min) => T(
        $"Video için alan en az {min}x{min} piksel olmalı.",
        $"The video area must be at least {min}x{min} pixels.",
        $"Der Videobereich muss mindestens {min}x{min} Pixel groß sein.");
    public static string TrayStartRecording => T("Video Kaydını Başlat", "Start Video Recording", "Videoaufnahme starten");
    public static string TrayStopRecording => T("Video Kaydını Durdur", "Stop Video Recording", "Videoaufnahme beenden");
    public static string SettingsVideo => T("Video", "Video", "Video");
    public static string SettingsFrameRate => T("Kare hızı:", "Frame rate:", "Bildrate:");
    public static string SettingsSystemAudio => T("Sistem sesini kaydet (bilgisayarda çalan ses)", "Record system audio (what the computer plays)", "Systemton aufnehmen (was der Computer abspielt)");
    public static string SettingsMicrophone => T("Mikrofonu kaydet", "Record the microphone", "Mikrofon aufnehmen");
    public static string SettingsVideoCursor => T("Fare imlecini videoda göster", "Show the mouse pointer in videos", "Mauszeiger im Video anzeigen");

    // ---- About ------------------------------------------------------------------------

    public static string AboutButton => T("Hakkında…", "About…", "Über…");
    public static string AboutTitle => T("ScreenCapture Hakkında", "About ScreenCapture", "Über ScreenCapture");
    public static string AboutVersion(string version) => T($"Sürüm {version}", $"Version {version}", $"Version {version}");
    public static string AboutDescription => T(
        "Tam ekran, pencere ve serbest alan için kompakt ekran görüntüsü aracı. Klavye veya fare kısayoluyla çalışır, görüntüleri kayıpsız PNG olarak kaydeder.",
        "A compact screen capture tool for full screen, window and region captures. Works with a keyboard or mouse shortcut and saves lossless PNG images.",
        "Ein kompaktes Werkzeug für Vollbild-, Fenster- und Bereichsaufnahmen. Arbeitet mit Tastatur- oder Maus-Kürzel und speichert verlustfreie PNG-Bilder.");
    public static string AboutDeveloper => T("Geliştirici", "Developer", "Entwickler");
    public static string AboutTagline => T("tasarım & geliştirme", "design & development", "Design & Entwicklung");
    public static string AboutBio => T(
        "Yirmi yılı aşkın sektör deneyimine sahip, serbest çalışan full-stack yazılım tasarımcısı ve geliştiricisi. Odak noktası en yeni teknolojiler ve kişiye özel çözümler.",
        "Freelance full-stack software designer and developer with over two decades of industry experience, focused on cutting-edge technologies and bespoke solutions.",
        "Freiberuflicher Full-Stack-Softwaredesigner und -entwickler mit über zwei Jahrzehnten Branchenerfahrung, mit Fokus auf modernste Technologien und maßgeschneiderte Lösungen.");
    public static string AboutExpertise => T(
        "BT danışmanlığı  •  Yazılım geliştirme (.NET, Java)  •  Duyarlı web ve mobil uygulamalar  •  Veri analitiği  •  WordPress bakımı",
        "IT consulting  •  Software development (.NET, Java)  •  Responsive web & mobile apps  •  Data analytics  •  WordPress care",
        "IT-Beratung  •  Softwareentwicklung (.NET, Java)  •  Responsive Web- & Mobile-Apps  •  Datenanalyse  •  WordPress-Betreuung");
    public static string AboutWebsite => T("Web sitesi", "Website", "Webseite");
    public static string AboutSourceCode => T("Kaynak kod", "Source code", "Quellcode");
    public static string Close => T("Kapat", "Close", "Schließen");
}
