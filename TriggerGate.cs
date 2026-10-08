namespace WolCatcher;

/// <summary>
/// Yakalanan bir magic packet'in güç işlemini tetikleyip tetiklemeyeceğine karar verir.
/// Saat dışarıdan verildiği için birim testlerle denenebilir.
/// </summary>
public sealed class TriggerGate
{
    private readonly TimeSpan _grace;
    private readonly TimeSpan _cooldown;
    private readonly object _lock = new();

    private readonly DateTime _graceStartUtc;
    private DateTime _lastActionUtc = DateTime.MinValue;
    private bool _running;

    public TriggerGate(TimeSpan grace, TimeSpan cooldown, DateTime startUtc)
    {
        _grace = grace;
        _cooldown = cooldown;
        _graceStartUtc = startUtc;
    }

    public GateResult Evaluate(DateTime nowUtc)
    {
        lock (_lock)
        {
            var sinceGraceStart = nowUtc - _graceStartUtc;
            if (sinceGraceStart < _grace)
                return new GateResult(GateDecision.GracePeriod, sinceGraceStart);
            if (_running)
                return new GateResult(GateDecision.AlreadyRunning, sinceGraceStart);
            if (nowUtc - _lastActionUtc < _cooldown)
                return new GateResult(GateDecision.Cooldown, sinceGraceStart);

            _running = true;
            _lastActionUtc = nowUtc;
            return new GateResult(GateDecision.Trigger, sinceGraceStart);
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
}

public enum GateDecision
{
    Trigger,
    GracePeriod,
    AlreadyRunning,
    Cooldown
}

public readonly record struct GateResult(GateDecision Decision, TimeSpan SinceGraceStart);
