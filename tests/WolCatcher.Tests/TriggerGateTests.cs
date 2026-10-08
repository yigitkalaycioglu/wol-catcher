using Xunit;

namespace WolCatcher.Tests;

public class TriggerGateTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    private static TriggerGate NewGate() => new(Grace, Cooldown, T0);

    // Servisin yaptığı gibi saati 5 saniyede bir gözlemleyerek zamanı ilerletir.
    private static DateTime Advance(TriggerGate gate, DateTime from, TimeSpan by)
    {
        var t = from;
        var end = from + by;
        while (t < end)
        {
            t = t.AddSeconds(5) > end ? end : t.AddSeconds(5);
            gate.ObserveClock(t);
        }
        return t;
    }

    [Fact]
    public void Baslangic_toleransinda_paketler_yok_sayilir()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromSeconds(20));
        Assert.Equal(GateDecision.GracePeriod, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Tolerans_bitince_islem_tetiklenir()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromSeconds(50));
        Assert.Equal(GateDecision.Trigger, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Islem_surerken_ikinci_kez_tetiklenmez()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromSeconds(50));
        gate.Evaluate(t);
        t = Advance(gate, t, TimeSpan.FromSeconds(20));
        Assert.Equal(GateDecision.AlreadyRunning, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Basarisiz_islemden_sonra_yeniden_tetiklenebilir()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromSeconds(50));
        gate.Evaluate(t);
        gate.Complete(); // örneğin shutdown.exe hata verdi
        t = Advance(gate, t, TimeSpan.FromSeconds(15));
        Assert.Equal(GateDecision.Trigger, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Bekleme_suresi_dolmadan_tekrar_tetiklenmez()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromSeconds(50));
        gate.Evaluate(t);
        gate.Complete();
        t = Advance(gate, t, TimeSpan.FromSeconds(3));
        Assert.Equal(GateDecision.Cooldown, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Uykudan_uyaninca_tolerans_yeniden_baslar()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromMinutes(10));

        // Bilgisayar iki saat uyudu, bu sürede saat gözlemi yapılmadı.
        t = t.AddHours(2);
        Assert.True(gate.ObserveClock(t));

        t = Advance(gate, t, TimeSpan.FromSeconds(5));
        Assert.Equal(GateDecision.GracePeriod, gate.Evaluate(t).Decision);

        t = Advance(gate, t, TimeSpan.FromSeconds(45));
        Assert.Equal(GateDecision.Trigger, gate.Evaluate(t).Decision);
    }

    [Fact]
    public void Uyanir_uyanmaz_gelen_paket_saat_gozleminden_once_de_yakalanir()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromMinutes(10));

        var result = gate.Evaluate(t.AddHours(2).AddSeconds(1));

        Assert.True(result.ResumeDetected);
        Assert.Equal(GateDecision.GracePeriod, result.Decision);
    }

    [Fact]
    public void Saatin_geri_alinmasi_uyanma_sayilmaz()
    {
        var gate = NewGate();
        var t = Advance(gate, T0, TimeSpan.FromMinutes(1));

        Assert.False(gate.ObserveClock(t.AddMinutes(-5)));
        Assert.Equal(GateDecision.Trigger, gate.Evaluate(t.AddSeconds(5)).Decision);
    }
}
