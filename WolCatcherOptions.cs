namespace WolCatcher;

/// <summary>
/// Uygulamanın davranışını belirleyen ayarlar. appsettings.json içindeki
/// "WolCatcher" bölümünden okunur.
/// </summary>
public sealed class WolCatcherOptions
{
    /// <summary>Yapılandırmadaki bölüm adı.</summary>
    public const string SectionName = "WolCatcher";

    /// <summary>Magic packet'in dinleneceği UDP portları. WoL için yaygın olanlar 9 ve 7'dir.</summary>
    public int[] Ports { get; set; } = { 9, 7 };

    /// <summary>
    /// Paket yakalandığında yapılacak işlem: "Shutdown", "Sleep" veya "Hibernate".
    /// </summary>
    public string Action { get; set; } = "Shutdown";

    /// <summary>
    /// Servis başladıktan sonra paketleri yok sayacağı süre (saniye).
    /// Bilgisayar WoL ile açıldığında, telefon uygulamasının gönderdiği
    /// tekrarlı paketlerin makineyi anında tekrar kapatmasını önler.
    /// </summary>
    public int StartupGraceSeconds { get; set; } = 45;

    /// <summary>
    /// Hedeflenecek MAC adresleri (örn. "AA:BB:CC:DD:EE:FF"). Boş bırakılırsa
    /// bu bilgisayardaki tüm fiziksel ağ adaptörlerinin MAC'leri otomatik kullanılır.
    /// </summary>
    public string[] TargetMacs { get; set; } = Array.Empty<string>();

    /// <summary>İşlemden önce beklenecek süre (saniye). 0 = anında.</summary>
    public int ActionDelaySeconds { get; set; } = 0;

    /// <summary>Kapatırken açık uygulamaları zorla kapat (kaydedilmemiş veriler kaybolabilir).</summary>
    public bool Force { get; set; } = true;

    /// <summary>
    /// Aynı paketin/tekrarların kısa sürede birden fazla işlem tetiklemesini önleyen
    /// bekleme süresi (saniye).
    /// </summary>
    public int CooldownSeconds { get; set; } = 10;
}
