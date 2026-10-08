namespace WolCatcher;

/// <summary>
/// Yakalanan bir magic packet'in güç işlemini tetikleyip tetiklemeyeceğine karar verir.
/// Saat dışarıdan verildiği için birim testlerle denenebilir.
/// </summary>
public sealed class TriggerGate
{
    /// <summary>
    /// Saat gözlemleri arasında bundan uzun süre geçtiyse bilgisayar uykudan ya da
    /// hazırda bekletmeden uyanmış sayılır. Servis saati birkaç saniyede bir gözlemlediği
    /// için normal çalışmada bu kadar uzun bir boşluk oluşmaz.
    /// </summary>
    public static readonly TimeSpan DefaultWakeGap = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _grace;
    private readonly TimeSpan _cooldown;
    private readonly TimeSpan _wakeGap;
    private readonly object _lock = new();

    private DateTime _graceStartUtc;
    private DateTime _lastSeenUtc;
    private DateTime _lastActionUtc = DateTime.MinValue;
    private bool _running;

    public TriggerGate(TimeSpan grace, TimeSpan cooldown, DateTime startUtc, TimeSpan? wakeGap = null)
    {
        _grace = grace;
        _cooldown = cooldown;
        _wakeGap = wakeGap ?? DefaultWakeGap;
        _graceStartUtc = startUtc;
        _lastSeenUtc = startUtc;
    }

    /// <summary>
    /// Saati gözlemler. Uykudan uyanma algılanırsa başlangıç toleransını o andan
    /// itibaren yeniden başlatır ve true döner. Birkaç saniyede bir çağrılmalı.
    /// </summary>
    public bool ObserveClock(DateTime nowUtc)
    {
        lock (_lock)
            return ObserveClockLocked(nowUtc);
    }

    public GateResult Evaluate(DateTime nowUtc)
    {
        lock (_lock)
        {
            // Uyanınca gelen ilk paket, bir sonraki saat gözleminden önce gelebilir.
            bool resumed = ObserveClockLocked(nowUtc);

            var sinceGraceStart = nowUtc - _graceStartUtc;
            if (sinceGraceStart < _grace)
                return new GateResult(GateDecision.GracePeriod, sinceGraceStart, resumed);
            if (_running)
                return new GateResult(GateDecision.AlreadyRunning, sinceGraceStart, resumed);
            if (nowUtc - _lastActionUtc < _cooldown)
                return new GateResult(GateDecision.Cooldown, sinceGraceStart, resumed);

            _running = true;
            _lastActionUtc = nowUtc;
            return new GateResult(GateDecision.Trigger, sinceGraceStart, resumed);
        }
    }

    /// <summary>
    /// Güç işlemi bittiğinde çağrılır. İşlem başarısız olduysa (ya da uyku/hazırda
    /// bekletmeden dönüldüyse) sonraki paketler yeniden değerlendirilir.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
            _running = false;
    }

    private bool ObserveClockLocked(DateTime nowUtc)
    {
        bool resumed = nowUtc - _lastSeenUtc > _wakeGap;
        if (resumed)
            _graceStartUtc = nowUtc;
        if (nowUtc > _lastSeenUtc)
            _lastSeenUtc = nowUtc;
        return resumed;
    }
}

public enum GateDecision
{
    Trigger,
    GracePeriod,
    AlreadyRunning,
    Cooldown
}

public readonly record struct GateResult(GateDecision Decision, TimeSpan SinceGraceStart, bool ResumeDetected);
