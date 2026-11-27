# ScreenCapture

**[English](#english) · [Deutsch](#deutsch) · [Türkçe](#türkçe)**

---

## English

A compact screenshot and screen recording tool built with .NET 11 Windows Forms. It captures the full screen, a window or a region with a keyboard or mouse shortcut and saves images as lossless PNG and videos with sound as MP4 in the `Captures` folder.

### Build and run

```powershell
dotnet build ScreenCapture.slnx -c Release
dotnet run --project src/ScreenCapture
```

Requirements: .NET 11 SDK and Windows 10 version 2004 or later. Windows 11 is recommended.

To publish as a single file:

```powershell
dotnet publish src/ScreenCapture -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Usage

| Element | Description |
|---------|-------------|
| **Output** | **Image** (PNG screenshot) or **Video** (MP4 recording with sound) |
| **Capture mode** | Full Screen, Window or Region |
| **Capture area** | Shows the saved area of the selected mode. Save it with **Set Area…**, remove it with **Clear** |
| **Shortcut** | Shows the active shortcut. Change it with **Change…** |
| **Capture** | Captures immediately with the selected mode and area |
| **Folder** | Opens the `Captures` folder |
| **⚙** | More settings, language, appearance (light / dark) and **About…** |
| Status line | Shows the last saved file. Click it to show the file selected in Explorer |

#### Capture area

Each mode keeps its own saved area. Once an area is saved, the **Capture** button, the shortcut and the tray menu capture it directly, without a selection screen. **Clear** returns to choosing at every capture.

| Mode | Without a saved area | With **Set Area…** |
|------|----------------------|--------------------|
| **Full Screen** | The monitor under the mouse pointer | Pick a specific monitor or "All monitors" from the menu |
| **Window** | The screen freezes and the clicked window is captured. If "capture the active window" is enabled in the settings, no selection screen appears | Click a window; later captures always take that window |
| **Region** | The screen freezes and you drag a rectangle | Drag a rectangle; later captures always take the same rectangle |

- Press **Esc** or **right-click** to leave a selection screen.
- While the app is running, a **red frame** marks the area the selected mode will capture: the saved region, the saved window (it follows the window and disappears while the window is minimized) or the monitor. The frame never appears in images or videos and can be turned off under **⚙ > Capture**. Areas picked anew each time get the frame for a moment after the capture.
- A **saved window** is found again by process name and window class, even after the application was closed and reopened. It is captured even when other windows cover it. If it is closed or minimized, a warning appears in the status line (as a notification when the main window is hidden).
- The same warning appears if a **saved monitor** is not connected or a **saved region** lies outside the screens.

When minimized, the application goes to the taskbar. With "hide in the notification area" enabled it only shows a tray icon. Every mode can be started from the tray icon's context menu. Only one instance runs at a time; starting it again brings the existing window to the front.

### Language

The UI is available in English, German and Turkish. On first start the Windows display language is used: Turkish or German if Windows uses it, English otherwise. Change the language under **⚙ > Language**. The main window, tray menu, selection screens and messages switch immediately, without a restart. Shortcut names follow the language too (for example `Strg+Umschalt+S` in German).

To add a text, add a property with all three translations to `src/ScreenCapture/Localization/Strings.cs`.

### Video recording

With **Video** selected under **Output**, **● Start Recording** or the shortcut starts recording the selected mode and area as MP4 (H.264 video + AAC audio); the same button (**■ Stop**, with the elapsed time) or the shortcut finishes it. Videos are saved as `yyyyMMdd_HHmmss_fff.mp4` in the `Captures` folder.

- Saved areas apply to videos too; without one you pick the area before recording starts. In window mode the recording follows the window when it moves.
- A red frame marks the recorded area; the frame does not appear in the video.
- **⚙ > Video** sets **system audio** (what the computer plays), **microphone** (both together are mixed), the mouse pointer and the frame rate (15/24/30/60 fps). Defaults: system audio on, microphone off, 30 fps.
- Areas larger than 4096x2304 pixels (e.g. several 4K monitors) are scaled down proportionally because of the H.264 limit.
- If an audio device is missing or the Windows Audio service does not respond, recording continues without that sound and a warning is shown.

### Appearance and About

Under **⚙ > Appearance** choose **Light** (default), **Dark** or **Use Windows setting**. The dark theme is applied immediately to the main window, dialogs, menus and title bar. With **Use Windows setting** the application follows changes to the Windows app theme right away.

**⚙ > About…** (or **About…** in the tray menu) shows the version, the developer and links to the website and the source code. The application icon and the photo in the About dialog come from `src/ScreenCapture/Assets` and are embedded in the application.

### Setting a shortcut

Click **Change…**, then press the shortcut you want.

- **Keyboard:** a key combined with a modifier, for example `Ctrl+Shift+S`. PrintScreen, F1–F24, Pause and ScrollLock also work on their own.
- **Mouse:** press a mouse button inside the blue area of the dialog.
  - The middle, back (X1) and forward (X2) buttons can be used, alone or with Ctrl, Alt, Shift or Win.
  - Left and right click are only accepted together with a modifier.
- By default the shortcut click is not passed to other applications, so for example the browser does not go "Back". This can be turned off in the settings.

Technically, keyboard shortcuts use `RegisterHotKey`. PrintScreen and mouse shortcuts use low-level hooks (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`).

### Saving

- **Location:** `<application folder>\Captures\yyyyMMdd_HHmmss_fff.png`, for example `20261125_165512_034.png`. If a file with the same name exists, `_1`, `_2` … is appended.
- If the application folder is not writable (for example under `Program Files`), files go to `%LocalAppData%\ScreenCapture\Captures` and the user is told so.
- Settings are stored in `settings.json`, errors in `error.log`, using the same folder rule.

#### Why PNG?

- **PNG:** lossless. Compresses the flat colors, text and UI elements typical of screenshots very well. Opens everywhere and needs no extra library.
- **JPEG:** lossy; creates artifacts around text.
- **BMP:** uncompressed; files become very large.
- **WebP lossless / JPEG XL:** need extra libraries and are less widely supported.

Images are saved as 24-bit RGB. The alpha channel of a screenshot carries no information, so no quality is lost.

### Known limitations

- **Elevated windows (UIPI):** if the application does not run as administrator, its low-level hooks cannot see input going to an elevated window in the foreground. Mouse and PrintScreen shortcuts then do not work. Run the application as administrator if needed.
- **Screen sharing:** the main window is excluded from screenshots with `WDA_EXCLUDEFROMCAPTURE`, so it is also invisible in screen sharing and recording tools such as Teams, Zoom and OBS.
- **Windows 11 and PrintScreen:** if "Use the Print screen key to open screen capture" is enabled, the two can conflict. Turning it off under Settings > Accessibility > Keyboard is recommended.
- **Covered windows:** windows hidden behind other windows are captured with `PrintWindow`. Some hardware-accelerated applications appear black this way; in that case the visible part on screen is captured.
- **Secure desktop:** the lock screen and UAC prompts cannot be captured.

---

## Deutsch

Ein kompaktes Werkzeug für Screenshots und Bildschirmaufnahmen auf Basis von .NET 11 Windows Forms. Es nimmt per Tastatur- oder Maus-Kürzel den ganzen Bildschirm, ein Fenster oder einen Bereich auf und speichert Bilder verlustfrei als PNG und Videos mit Ton als MP4 im Ordner `Captures`.

### Erstellen und ausführen

```powershell
dotnet build ScreenCapture.slnx -c Release
dotnet run --project src/ScreenCapture
```

Voraussetzungen: .NET 11 SDK und Windows 10 Version 2004 oder neuer. Windows 11 wird empfohlen.

Als einzelne Datei veröffentlichen:

```powershell
dotnet publish src/ScreenCapture -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Bedienung

| Element | Beschreibung |
|---------|--------------|
| **Ausgabe** | **Bild** (PNG-Screenshot) oder **Video** (MP4-Aufnahme mit Ton) |
| **Aufnahmemodus** | Vollbild, Fenster oder Bereich |
| **Aufnahmebereich** | Zeigt den gespeicherten Bereich des gewählten Modus. Speichern mit **Bereich festlegen…**, entfernen mit **Löschen** |
| **Tastenkürzel** | Zeigt das aktive Kürzel. Ändern mit **Ändern…** |
| **Aufnehmen** | Nimmt sofort mit dem gewählten Modus und Bereich auf |
| **Ordner** | Öffnet den Ordner `Captures` |
| **⚙** | Weitere Einstellungen, Sprache, Darstellung (hell / dunkel) und **Über…** |
| Statuszeile | Zeigt die zuletzt gespeicherte Datei. Ein Klick zeigt sie markiert im Explorer |

#### Aufnahmebereich

Jeder Modus speichert seinen eigenen Bereich. Ist ein Bereich gespeichert, nehmen die Schaltfläche **Aufnehmen**, das Tastenkürzel und das Infobereich-Menü ihn direkt auf, ohne Auswahlbildschirm. **Löschen** kehrt zur Auswahl bei jeder Aufnahme zurück.

| Modus | Ohne gespeicherten Bereich | Mit **Bereich festlegen…** |
|-------|----------------------------|----------------------------|
| **Vollbild** | Der Monitor unter dem Mauszeiger | Einen bestimmten Monitor oder „Alle Monitore“ im Menü wählen |
| **Fenster** | Der Bildschirm wird eingefroren, das angeklickte Fenster wird aufgenommen. Ist „aktives Fenster aufnehmen“ in den Einstellungen aktiv, erscheint keine Auswahl | Ein Fenster anklicken; spätere Aufnahmen nehmen immer dieses Fenster |
| **Bereich** | Der Bildschirm wird eingefroren, ein Rechteck wird aufgezogen | Ein Rechteck aufziehen; spätere Aufnahmen nehmen immer dasselbe Rechteck |

- Mit **Esc** oder **Rechtsklick** wird ein Auswahlbildschirm verlassen.
- Solange das Programm läuft, markiert ein **roter Rahmen** den Bereich, den der gewählte Modus aufnimmt: den gespeicherten Bereich, das gespeicherte Fenster (der Rahmen folgt dem Fenster und verschwindet, solange es minimiert ist) oder den Monitor. Der Rahmen erscheint nie in Bildern oder Videos und lässt sich unter **⚙ > Aufnahme** abschalten. Jedes Mal neu gewählte Bereiche werden nach der Aufnahme kurz umrahmt.
- Ein **gespeichertes Fenster** wird über Prozessname und Fensterklasse wiedergefunden, auch nachdem das Programm geschlossen und neu geöffnet wurde. Es wird auch dann aufgenommen, wenn andere Fenster es verdecken. Ist es geschlossen oder minimiert, erscheint ein Hinweis in der Statuszeile (bei ausgeblendetem Hauptfenster als Benachrichtigung).
- Derselbe Hinweis erscheint, wenn ein **gespeicherter Monitor** nicht verbunden ist oder ein **gespeicherter Bereich** außerhalb der Bildschirme liegt.

Beim Minimieren wandert das Programm in die Taskleiste. Mit „in den Infobereich ausblenden“ erscheint nur noch ein Symbol im Infobereich. Über dessen Kontextmenü lässt sich jeder Modus direkt starten. Es läuft immer nur eine Instanz; ein erneuter Start holt das vorhandene Fenster nach vorne.

### Sprache

Die Oberfläche gibt es auf Deutsch, Englisch und Türkisch. Beim ersten Start wird die Windows-Anzeigesprache verwendet: Deutsch oder Türkisch, wenn Windows sie nutzt, sonst Englisch. Die Sprache wird unter **⚙ > Sprache / Language** geändert. Hauptfenster, Infobereich-Menü, Auswahlbildschirme und Meldungen wechseln sofort, ohne Neustart. Auch die Namen der Tastenkürzel passen sich an (z. B. `Strg+Umschalt+S`).

Neue Texte werden in `src/ScreenCapture/Localization/Strings.cs` als Eigenschaft mit allen drei Übersetzungen ergänzt.

### Videoaufnahme

Ist unter **Ausgabe** **Video** gewählt, startet **● Aufnahme starten** oder das Kürzel die Aufnahme des gewählten Modus und Bereichs als MP4 (H.264-Video + AAC-Ton); dieselbe Schaltfläche (**■ Stoppen**, mit Laufzeit) oder das Kürzel beendet sie. Videos werden als `yyyyMMdd_HHmmss_fff.mp4` im Ordner `Captures` gespeichert.

- Gespeicherte Bereiche gelten auch für Videos; ohne gespeicherten Bereich wird vor der Aufnahme ausgewählt. Im Fenstermodus folgt die Aufnahme dem Fenster, wenn es verschoben wird.
- Ein roter Rahmen markiert den aufgenommenen Bereich; der Rahmen erscheint nicht im Video.
- Unter **⚙ > Video** werden **Systemton** (was der Computer abspielt), **Mikrofon** (beide zusammen werden gemischt), Mauszeiger und Bildrate (15/24/30/60 fps) eingestellt. Standard: Systemton an, Mikrofon aus, 30 fps.
- Bereiche größer als 4096x2304 Pixel (z. B. mehrere 4K-Monitore) werden wegen der H.264-Grenze proportional verkleinert.
- Wird kein Audiogerät gefunden oder reagiert der Windows-Audiodienst nicht, läuft die Aufnahme ohne Ton weiter und ein Hinweis erscheint.

### Darstellung und Info

Unter **⚙ > Darstellung** stehen **Hell** (Standard), **Dunkel** und **Windows-Einstellung verwenden** zur Auswahl. Das dunkle Design wird sofort auf Hauptfenster, Dialoge, Menüs und Titelleiste angewendet. Mit **Windows-Einstellung verwenden** folgt das Programm Änderungen am Windows-App-Design sofort.

**⚙ > Über…** (oder **Über…** im Infobereich-Menü) zeigt Version, Entwickler sowie Links zur Webseite und zum Quellcode. Programmsymbol und Foto im Info-Dialog stammen aus `src/ScreenCapture/Assets` und sind in das Programm eingebettet.

### Tastenkürzel festlegen

Auf **Ändern…** klicken und dann das gewünschte Kürzel drücken.

- **Tastatur:** eine Taste mit Zusatztaste, z. B. `Strg+Umschalt+S`. Druck, F1–F24, Pause und Rollen funktionieren auch allein.
- **Maus:** im blauen Feld des Dialogs eine Maustaste drücken.
  - Mittlere Taste, Zurück (X1) und Vorwärts (X2) sind möglich, allein oder mit Strg, Alt, Umschalt oder Win.
  - Links- und Rechtsklick werden nur zusammen mit einer Zusatztaste akzeptiert.
- Standardmäßig wird der Kürzel-Klick nicht an andere Programme weitergegeben, damit z. B. der Browser nicht „Zurück“ ausführt. Das lässt sich in den Einstellungen abschalten.

Technisch nutzen Tastenkürzel `RegisterHotKey`. Druck- und Maus-Kürzel nutzen Low-Level-Hooks (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`).

### Speichern

- **Ort:** `<Programmordner>\Captures\yyyyMMdd_HHmmss_fff.png`, z. B. `20261125_165512_034.png`. Existiert der Name bereits, wird `_1`, `_2` … angehängt.
- Ist der Programmordner schreibgeschützt (z. B. unter `Program Files`), wird nach `%LocalAppData%\ScreenCapture\Captures` gespeichert und der Benutzer darüber informiert.
- Einstellungen liegen in `settings.json`, Fehler in `error.log`, nach derselben Ordnerregel.

#### Warum PNG?

- **PNG:** verlustfrei. Komprimiert die für Screenshots typischen einfarbigen Flächen, Texte und Bedienelemente sehr gut. Lässt sich überall öffnen und braucht keine Zusatzbibliothek.
- **JPEG:** verlustbehaftet; erzeugt Artefakte an Texten.
- **BMP:** unkomprimiert; die Dateien werden sehr groß.
- **WebP verlustfrei / JPEG XL:** benötigen Zusatzbibliotheken und werden seltener unterstützt.

Bilder werden als 24-Bit-RGB gespeichert. Der Alphakanal eines Screenshots enthält keine Information, daher geht keine Qualität verloren.

### Bekannte Einschränkungen

- **Fenster mit Administratorrechten (UIPI):** Läuft das Programm nicht als Administrator, sehen seine Low-Level-Hooks keine Eingaben an ein solches Fenster im Vordergrund. Maus- und Druck-Kürzel funktionieren dann nicht. Bei Bedarf das Programm als Administrator starten.
- **Bildschirmfreigabe:** Das Hauptfenster wird per `WDA_EXCLUDEFROMCAPTURE` von Screenshots ausgeschlossen und ist daher auch in Freigabe- und Aufnahmeprogrammen wie Teams, Zoom und OBS unsichtbar.
- **Windows 11 und die Druck-Taste:** Ist „Bildschirmausschnitt mit der Taste ‚Druck‘ öffnen“ aktiv, kann es zu Konflikten kommen. Es wird empfohlen, die Option unter Einstellungen > Barrierefreiheit > Tastatur zu deaktivieren.
- **Verdeckte Fenster:** Fenster hinter anderen Fenstern werden mit `PrintWindow` aufgenommen. Manche hardwarebeschleunigte Programme erscheinen dabei schwarz; dann wird der sichtbare Teil auf dem Bildschirm aufgenommen.
- **Sicherer Desktop:** Sperrbildschirm und UAC-Abfragen können nicht aufgenommen werden.

---

## Türkçe

.NET 11 Windows Forms ile yazılmış, küçük arayüzlü bir ekran görüntüsü ve ekran kaydı aracı. Bir klavye veya fare kısayolu ile tam ekran, pencere ya da serbest alanı yakalar; görüntüleri kayıpsız PNG, videoları sesli MP4 olarak `Captures` klasörüne kaydeder.

### Derleme ve çalıştırma

```powershell
dotnet build ScreenCapture.slnx -c Release
dotnet run --project src/ScreenCapture
```

Gereksinimler: .NET 11 SDK ve Windows 10 2004 veya üzeri. Windows 11 önerilir.

Tek dosya halinde yayınlamak için:

```powershell
dotnet publish src/ScreenCapture -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Kullanım

| Öğe | Açıklama |
|-----|----------|
| **Çıktı** | **Resim** (PNG ekran görüntüsü) veya **Video** (sesli MP4 kaydı) |
| **Yakalama modu** | Tam Ekran, Pencere veya Serbest Alan |
| **Yakalama alanı** | Seçili modun kayıtlı alanını gösterir. **Alan Belirle…** ile kaydedilir, **Temizle** ile silinir |
| **Kısayol** | Etkin kısayolu gösterir. **Değiştir…** ile değiştirilir |
| **Yakala** | Seçili mod ve alanla hemen yakalar |
| **Klasör** | `Captures` klasörünü açar |
| **⚙** | Ek ayarlar, dil, görünüm (açık / koyu) ve **Hakkında…** |
| Durum satırı | Son kaydedilen dosyayı gösterir. Tıklayınca dosyayı Explorer'da seçili olarak açar |

#### Yakalama alanı

Her mod kendi kayıtlı alanını ayrı tutar. Alan kaydedildiğinde **Yakala** düğmesi, kısayol ve tepsi menüsü seçim ekranı açmadan doğrudan bu alanı yakalar. **Temizle** ile her seferinde seçim yapılan davranışa dönülür.

| Mod | Alan belirlenmemişse | **Alan Belirle…** ile |
|-----|----------------------|-----------------------|
| **Tam Ekran** | Farenin bulunduğu monitör | Menüden belirli bir monitör veya "Tüm monitörler" seçilir |
| **Pencere** | Ekran dondurulur, tıklanan pencere yakalanır. Ayarlardan "etkin pencereyi yakala" seçilirse seçim ekranı açılmaz | Bir pencereye tıklanır; sonraki yakalamalarda yalnızca o pencere yakalanır |
| **Serbest Alan** | Ekran dondurulur, fare ile bir alan sürüklenir | Bir alan sürüklenir; sonraki yakalamalarda hep aynı dikdörtgen yakalanır |

- Seçim ekranlarından **Esc** veya **sağ tık** ile çıkılır.
- Uygulama açık olduğu sürece seçili modun yakalayacağı alan **kırmızı bir çerçeveyle** gösterilir: kayıtlı alan, kayıtlı pencere (çerçeve pencereyi izler, pencere küçültülmüşken gizlenir) ya da monitör. Çerçeve resimlere ve videolara hiçbir zaman girmez; **⚙ > Yakalama** bölümünden kapatılabilir. Her seferinde yeniden seçilen alanlar yakalamadan sonra kısa bir süre çerçeveyle gösterilir.
- **Kayıtlı pencere** uygulama kapatılıp yeniden açılsa bile işlem adı ve pencere sınıfına göre yeniden bulunur. Pencere başka pencerelerin altında kalsa da yakalanır. Pencere kapatılmış ya da simge durumuna küçültülmüşse durum satırında (ana pencere gizliyse tepsi bildiriminde) bir uyarı gösterilir.
- **Kayıtlı monitör** bağlı değilse veya **kayıtlı alan** ekranların dışında kalıyorsa da aynı şekilde uyarı gösterilir.

Uygulama küçültüldüğünde görev çubuğuna iner. Ayarlarda "sistem tepsisine gizle" seçiliyse yalnızca bildirim alanında görünür. Tepsi simgesinin sağ tık menüsünden her mod doğrudan çalıştırılabilir. Uygulamanın aynı anda tek kopyası çalışır; ikinci kez açılırsa var olan pencere öne gelir.

### Dil

Arayüz Türkçe, İngilizce ve Almanca kullanılabilir. İlk açılışta Windows görüntüleme dili Türkçe ise Türkçe, Almanca ise Almanca, diğer durumlarda İngilizce seçilir. Dil **⚙ > Dil / Language** bölümünden değiştirilir; uygulamayı yeniden başlatmaya gerek kalmadan ana pencere, tepsi menüsü, seçim ekranları ve mesajlar hemen yeni dile geçer. Kısayol adları da dile uyar (örneğin Almancada `Strg+Umschalt+S`).

Yeni bir metin eklemek için `src/ScreenCapture/Localization/Strings.cs` dosyasına üç dildeki karşılığıyla bir özellik eklenir.

### Video kaydı

**Çıktı** bölümünde **Video** seçildiğinde **● Kaydı Başlat** düğmesi ve kısayol, seçili mod ve alanı MP4 (H.264 video + AAC ses) olarak kaydetmeye başlar; aynı düğme (**■ Durdur**, geçen süreyle) veya kısayol kaydı bitirir. Video da `Captures` klasörüne `yyyyMMdd_HHmmss_fff.mp4` adıyla kaydedilir.

- Kayıtlı alanlar videoda da geçerlidir; alan yoksa kayıttan önce seçim yapılır. Pencere modunda kayıt, pencere taşınırsa onu izler.
- Kaydedilen alanın çevresinde kırmızı bir çerçeve görünür; çerçeve videoya girmez.
- **⚙ > Video** bölümünden **sistem sesi** (bilgisayarda çalan ses), **mikrofon** (ikisi birlikte seçilirse karıştırılır), fare imleci ve kare hızı (15/24/30/60 fps) ayarlanır. Varsayılan: sistem sesi açık, mikrofon kapalı, 30 fps.
- 4096x2304 pikselden büyük alanlar (ör. birden çok 4K monitör) H.264 sınırı nedeniyle orantılı küçültülür.
- Ses cihazı bulunamazsa veya Windows Ses hizmeti yanıt vermezse kayıt sessiz devam eder ve uyarı gösterilir.

### Görünüm ve Hakkında

**⚙ > Görünüm** bölümünden **Açık** (varsayılan), **Koyu** veya **Windows ayarını kullan** seçilebilir. Koyu tema; ana pencereye, diyaloglara, menülere ve başlık çubuğuna anında uygulanır. **Windows ayarını kullan** seçiliyken uygulama, Windows uygulama temasındaki değişikliği de hemen izler.

**⚙ > Hakkında…** (veya tepsi menüsündeki **Hakkında…**) sürüm bilgisini, geliştiriciyi ve web sitesi ile kaynak kod bağlantılarını gösterir. Uygulama ikonu ve Hakkında penceresindeki fotoğraf `src/ScreenCapture/Assets` klasöründen gelir ve uygulamaya gömülüdür.

### Kısayol atama

**Değiştir…** düğmesine basın, sonra kullanmak istediğiniz kısayola basın.

- **Klavye:** Değiştirici ile bir tuş kullanılabilir, örneğin `Ctrl+Shift+S`. PrintScreen, F1–F24, Pause ve ScrollLock tek başına da kullanılabilir.
- **Fare:** Diyalogdaki mavi alanda bir fare tuşuna basın.
  - Orta tuş, Geri (X1) ve İleri (X2) tuşları tek başına ya da Ctrl, Alt, Shift veya Win ile birlikte kullanılabilir.
  - Sol ve sağ tık yalnızca bir değiştirici ile birlikte kabul edilir.
- Varsayılan ayarda kısayol tıklaması diğer uygulamalara iletilmez. Bu yüzden örneğin tarayıcıda "Geri" komutu çalışmaz. Bu davranış ayarlardan kapatılabilir.

Teknik olarak klavye kısayolları `RegisterHotKey` ile alınır. PrintScreen ile fare kısayolları ise düşük seviye kancalar (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`) ile alınır.

### Kayıt

- **Konum:** `<uygulama klasörü>\Captures\yyyyMMdd_HHmmss_fff.png`, örneğin `20261125_165512_034.png`. Aynı isimde bir dosya varsa sonuna `_1`, `_2` eklenir.
- Uygulama klasörüne yazılamıyorsa (örneğin `Program Files` altında) dosyalar `%LocalAppData%\ScreenCapture\Captures` klasörüne kaydedilir ve kullanıcıya bildirilir.
- Ayarlar `settings.json` dosyasında, hatalar `error.log` dosyasında tutulur. İkisi de aynı klasör mantığıyla yerleşir.

#### Neden PNG?

- **PNG:** Kayıpsızdır. Ekran görüntülerinde sık görülen düz renk alanları, metin ve arayüz öğelerinde çok iyi sıkıştırır. Her yerde açılır ve harici kütüphane gerektirmez.
- **JPEG:** Kayıplıdır; metinlerde bozulma (artefakt) oluşturur.
- **BMP:** Sıkıştırmasızdır; dosyalar çok büyük olur.
- **WebP-lossless / JPEG XL:** Ek kütüphane gerektirir ve uyumlulukları daha düşüktür.

Görüntüler 24 bit RGB olarak kaydedilir. Ekran görüntüsünde alfa kanalı bir bilgi taşımadığı için kalite kaybı olmaz.

### Bilinen kısıtlamalar

- **Yönetici pencereleri (UIPI):** Uygulama yönetici olarak çalışmıyorsa, yönetici yetkisiyle çalışan bir pencere öndeyken düşük seviye kancalar o pencereye gelen girdiyi göremez. Bu durumda fare ve PrintScreen kısayolları çalışmaz. Gerekirse uygulamayı yönetici olarak çalıştırın.
- **Ekran paylaşımı:** Ana pencere `WDA_EXCLUDEFROMCAPTURE` ile ekran görüntülerinden dışlanır. Bu yüzden Teams, Zoom ve OBS gibi ekran paylaşım ve kayıt araçlarında da görünmez.
- **Windows 11 ve PrintScreen:** "Ekran alıntısını açmak için Print Screen tuşunu kullan" ayarı açıksa çakışma olabilir. Ayarlar > Erişilebilirlik > Klavye yolundan kapatılması önerilir.
- **Arkada kalan pencereler:** Başka pencerelerin altında kalan pencereler `PrintWindow` ile yakalanır. Bazı donanım hızlandırmalı uygulamalar bu yöntemle siyah görünebilir. Bu durumda ekranda görünen kısım yakalanır.
- **Güvenli masaüstü:** Kilit ekranı ve UAC istemi gibi güvenli masaüstü ekranları yakalanamaz.
