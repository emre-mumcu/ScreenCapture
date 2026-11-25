# ScreenCapture

Bu, .NET 11 Windows Forms ile yazılmış, küçük arayüzlü bir ekran görüntüsü aracıdır. Bir klavye veya fare kısayolu ile tam ekran, pencere ya da serbest alan yakalar. Görüntüleri `Captures` klasörüne kayıpsız PNG olarak kaydeder.

## Derleme ve çalıştırma

```powershell
dotnet build ScreenCapture.slnx -c Release
dotnet run --project src/ScreenCapture
```

Gereksinimler: .NET 11 SDK ve Windows 10 2004 veya üzeri. Windows 11 önerilir.

Tek dosya halinde yayınlamak için:

```powershell
dotnet publish src/ScreenCapture -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Kullanım

| Öğe | Açıklama |
|-----|----------|
| **Yakalama modu** | Tam Ekran, Pencere veya Serbest Alan |
| **Yakalama alanı** | Seçili modun kayıtlı alanını gösterir. **Alan Belirle…** ile kaydedilir, **Temizle** ile silinir |
| **Kısayol** | Etkin kısayolu gösterir. **Değiştir…** ile değiştirilir |
| **Yakala** | Seçili mod ve alanla hemen yakalar |
| **Klasör** | `Captures` klasörünü açar |
| **⚙** | Ek ayarlar ve arayüz dili |
| Durum satırı | Son kaydedilen dosyayı gösterir. Tıklayınca dosyayı Explorer'da seçili olarak açar |

### Yakalama alanı

Her mod kendi kayıtlı alanını ayrı tutar. Alan kaydedildiğinde **Yakala** düğmesi, kısayol ve tepsi menüsü seçim ekranı açmadan doğrudan bu alanı yakalar. **Temizle** ile her seferinde seçim yapılan davranışa dönülür.

| Mod | Alan belirlenmemişse | **Alan Belirle…** ile |
|-----|----------------------|-----------------------|
| **Tam Ekran** | Farenin bulunduğu monitör | Menüden belirli bir monitör veya "Tüm monitörler" seçilir |
| **Pencere** | Ekran dondurulur, tıklanan pencere yakalanır. Ayarlardan "etkin pencereyi yakala" seçilirse seçim ekranı açılmaz | Bir pencereye tıklanır; sonraki yakalamalarda yalnızca o pencere yakalanır |
| **Serbest Alan** | Ekran dondurulur, fare ile bir alan sürüklenir | Bir alan sürüklenir; sonraki yakalamalarda hep aynı dikdörtgen yakalanır |

- Seçim ekranlarından **Esc** veya **sağ tık** ile çıkılır.
- **Kayıtlı pencere** uygulama kapatılıp yeniden açılsa bile işlem adı ve pencere sınıfına göre yeniden bulunur. Pencere başka pencerelerin altında kalsa da yakalanır. Pencere kapatılmış ya da simge durumuna küçültülmüşse durum satırında (pencere gizliyse tepsi bildiriminde) bir uyarı gösterilir.
- **Kayıtlı monitör** bağlı değilse veya **kayıtlı alan** ekranların dışında kalıyorsa da aynı şekilde uyarı gösterilir.

Uygulama küçültüldüğünde görev çubuğuna iner. Ayarlarda "sistem tepsisine gizle" seçiliyse yalnızca bildirim alanında görünür. Tepsi simgesinin sağ tık menüsünden her mod doğrudan çalıştırılabilir. Uygulamanın aynı anda tek kopyası çalışır; ikinci kez açılırsa var olan pencere öne gelir.

## Dil

Arayüz Türkçe, İngilizce ve Almanca kullanılabilir. İlk açılışta Windows görüntüleme dili Türkçe ise Türkçe, Almanca ise Almanca, diğer durumlarda İngilizce seçilir. Dil **⚙ > Dil / Language** bölümünden değiştirilir; uygulamayı yeniden başlatmaya gerek kalmadan ana pencere, tepsi menüsü, seçim ekranları ve mesajlar hemen yeni dile geçer. Kısayol adları da dile uyar (örneğin Almancada `Strg+Umschalt+S`).

Yeni bir metin eklemek için `src/ScreenCapture/Localization/Strings.cs` dosyasına üç dildeki karşılığıyla bir özellik eklenir.

## Kısayol atama

**Değiştir…** düğmesine basın, sonra kullanmak istediğiniz kısayola basın.

- **Klavye:** Değiştirici ile bir tuş kullanılabilir, örneğin `Ctrl+Shift+S`. PrintScreen, F1–F24, Pause ve ScrollLock tek başına da kullanılabilir.
- **Fare:** Diyalogdaki mavi alanda bir fare tuşuna basın.
  - Orta tuş, Geri (X1) ve İleri (X2) tuşları kullanılabilir. İsterseniz Ctrl, Alt, Shift veya Win ile birlikte de kullanılabilirler.
  - Sol ve sağ tık yalnızca bir değiştirici ile birlikte kabul edilir.
- Varsayılan fare ayarında kısayol tıklaması diğer uygulamalara iletilmez. Bu yüzden örneğin tarayıcıda "Geri" komutu çalışmaz. Bu davranış ayarlardan kapatılabilir.

Teknik olarak klavye kısayolları `RegisterHotKey` ile alınır. PrintScreen ile fare kısayolları ise düşük seviye kancalar (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`) ile alınır.

## Kayıt

- **Konum:** `<uygulama klasörü>\Captures\yyyyMMdd_HHmmss_fff.png`, örneğin `20261125_165512_034.png`. Aynı isimde bir dosya varsa sonuna `_1`, `_2` eklenir.
- Uygulama klasörüne yazılamıyorsa (örneğin `Program Files` altında) dosyalar `%LocalAppData%\ScreenCapture\Captures` klasörüne kaydedilir ve kullanıcıya bildirilir.
- Ayarlar `settings.json` dosyasında, hatalar `error.log` dosyasında tutulur. İkisi de aynı klasör mantığıyla yerleşir.

### Neden PNG?

- **PNG:** Kayıpsızdır. Ekran görüntülerinde sık görülen düz renk alanları, metin ve arayüz öğelerinde çok iyi sıkıştırır. Her yerde açılır ve harici kütüphane gerektirmez.
- **JPEG:** Kayıplıdır; metinlerde bozulma (artefakt) oluşturur.
- **BMP:** Sıkıştırmasızdır; dosyalar çok büyük olur.
- **WebP-lossless / JPEG XL:** Ek kütüphane gerektirir ve uyumlulukları daha düşüktür.

Görüntüler 24 bit RGB olarak kaydedilir. Ekran görüntüsünde alfa kanalı bir bilgi taşımadığı için kalite kaybı olmaz.

## Bilinen kısıtlamalar

- **Yönetici pencereleri (UIPI):** Uygulama yönetici olarak çalışmıyorsa, yönetici yetkisiyle çalışan bir pencere öndeyken düşük seviye kancalar o pencereye gelen girdiyi göremez. Bu durumda fare ve PrintScreen kısayolları çalışmaz. Gerekirse uygulamayı yönetici olarak çalıştırın.
- **Ekran paylaşımı:** Ana pencere `WDA_EXCLUDEFROMCAPTURE` ile ekran görüntülerinden dışlanır. Bu yüzden Teams, Zoom ve OBS gibi ekran paylaşım ve kayıt araçlarında da görünmez.
- **Windows 11 ve PrintScreen:** "Ekran alıntısını açmak için Print Screen tuşunu kullan" ayarı açıksa çakışma olabilir. Ayarlar > Erişilebilirlik > Klavye yolundan kapatılması önerilir.
- **Arkada kalan pencereler:** Başka pencerelerin altında kalan pencereler `PrintWindow` ile yakalanır. Bazı donanım hızlandırmalı uygulamalar bu yöntemle siyah görünebilir. Bu durumda ekranda görünen kısım yakalanır.
- **Güvenli masaüstü:** Kilit ekranı ve UAC istemi gibi güvenli masaüstü ekranları yakalanamaz.
- **Uygulama ikonu:** İkon çalışma anında programla çizilir; `.exe` dosyasının kendi ikonu varsayılan ikondur.
