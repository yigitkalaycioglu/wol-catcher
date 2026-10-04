# WolCatcher

Telefondaki Wake-on-LAN uygulaması bilgisayarı açabiliyor ama kapatamıyor. WolCatcher bu eksiği kapatan küçük bir Windows servisi: bilgisayar açıkken gelen magic packet'i yakalıyor ve bilgisayarı kapatıyor. Böylece telefondaki aynı WoL butonu hem açmak hem kapatmak için kullanılabiliyor.

C# ve .NET 10 ile yazıldı, .NET Generic Host üzerinde Windows servisi olarak çalışıyor. Haziran'dan beri kendi bilgisayarımda servis olarak kurulu.

## Nasıl çalışıyor

- UDP 9 ve 7 portlarını broadcast dahil dinliyor (portlar ayarlanabilir).
- Gelen paketin içinde magic packet arıyor: 6 bayt `FF` ve ardından 16 kez tekrarlanan MAC adresi. Arama paketin her yerinde yapılıyor, başta fazladan bayt ya da sonda SecureOn şifresi olsa da paket tanınıyor.
- Paketteki MAC bu bilgisayara aitse ayarlanan işlemi yapıyor: kapatma (`shutdown.exe`), uyku ya da hazırda bekletme (`powrprof.dll` içindeki `SetSuspendState`, P/Invoke ile).
- MAC adresleri ağ adaptörlerinden otomatik bulunuyor, istenirse `appsettings.json` içinde elle de verilebiliyor.
- Bilgisayar WoL ile açıldığında telefon birkaç paket daha gönderiyor. Bu yüzden servis başladıktan sonraki 45 saniye boyunca gelen paketler yok sayılıyor. Kısa sürede birden fazla tetiklenmeyi de ayrı bir bekleme süresi engelliyor.
- Aynı exe servisi kurup yönetebiliyor. `sc.exe` ile servis otomatik başlayacak şekilde kuruluyor ve çökerse yeniden başlatılması ayarlanıyor. Yönetici izni gerekirse UAC ile kendini yeniden başlatıyor.
- Loglar `%ProgramData%\WolCatcher\wolcatcher.log` dosyasına yazılıyor, dosya 5 MB'ı geçince `.1` olarak yedekleniyor. Servisin başlama, durma ve hata kayıtları Olay Görüntüleyici'de de görünüyor.

## Derleme ve kurulum

Gereksinimler: Windows 10/11 x64 ve .NET 10 SDK. Servis komutları yönetici izni istiyor.

```powershell
dotnet build -c Release
```

Kurulum için tek dosyalık bir exe almak daha pratik:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

`publish\WolCatcher.exe` ve `publish\appsettings.json` dosyalarını kalıcı bir klasöre (örneğin `C:\Tools\WolCatcher`) kopyalayıp kurulumu yapın:

```powershell
.\WolCatcher.exe install
```

Servis exe'nin bulunduğu yolu kullandığı için kurulumdan sonra klasörü taşımayın.

| Komut | Ne yapar |
|---|---|
| `install` | Servisi kurar ve başlatır |
| `uninstall` | Servisi durdurup kaldırır |
| `start`, `stop` | Servisi başlatır, durdurur |
| `status` | Servisin durumunu gösterir |
| `console` | Servis olarak değil konsolda çalıştırır |
| `help` | Kullanımı gösterir |

`appsettings.json` değiştirildikten sonra servisi yeniden başlatmak gerekiyor (`stop`, ardından `start`).

Konsolda bilgisayarı kapatmadan denemek için tolerans süresini uzatmak yeterli:

```powershell
$env:WolCatcher__StartupGraceSeconds = "99999"
dotnet run -- console
```

Açma tarafının çalışması için BIOS/UEFI ve ağ kartı ayarlarında Wake-on-LAN açık olmalı. Telefon uygulamasında hedef olarak ağın broadcast adresi (örneğin `192.168.1.255`) ve port 9 kullanılabilir. Windows Güvenlik Duvarı'nda UDP 9 ve 7 için gelen bağlantıya izin vermek gerekebilir.

## Ayarlar

```json
{
  "WolCatcher": {
    "Ports": [ 9, 7 ],
    "Action": "Shutdown",
    "StartupGraceSeconds": 45,
    "TargetMacs": [],
    "ActionDelaySeconds": 0,
    "Force": true,
    "CooldownSeconds": 10
  }
}
```

- `Action`: `Shutdown`, `Sleep` veya `Hibernate`.
- `TargetMacs`: boşsa bu bilgisayardaki adaptörlerin MAC adresleri kullanılıyor. `AA:BB:CC:DD:EE:FF`, `aa-bb-cc-dd-ee-ff` veya ayraçsız yazılabilir.
- `Force`: kapatırken açık uygulamaları zorla kapatır, kaydedilmemiş işler kaybolabilir.
- `ActionDelaySeconds`: işlemden önce beklenecek süre.

Ayarlar ortam değişkenleriyle de değiştirilebiliyor, örneğin `WolCatcher__Action=Sleep`.

## Bilinen sorunlar

- Bilgisayar WoL ile uykudan uyandırıldığında tolerans süresi işlemiyor, çünkü süre sadece servis başladığında sayılıyor. Telefonun gönderdiği tekrar paketleri bilgisayarı hemen tekrar kapatmaya çalışabiliyor.
- İşlem başarısız olursa tetikleme kilidi sıfırlanmıyor ve servis yeniden başlayana kadar yeni paketler yok sayılıyor. Aynı sebeple `Sleep` ve `Hibernate` servis çalıştığı sürece yalnızca bir kez çalışıyor.
- Kimlik doğrulama yok. Aynı ağda bu bilgisayarın MAC adresine magic packet gönderen herhangi bir cihaz onu kapatabilir.
- Dosyaya sadece `Information` ve üstü seviyedeki loglar yazılıyor.
- Sadece Windows'ta çalışıyor, otomatik test yok.

## Lisans

MIT
