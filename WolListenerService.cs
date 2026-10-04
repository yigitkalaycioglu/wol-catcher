using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WolCatcher;

/// <summary>
/// Magic packet'leri UDP üzerinden dinleyen arka plan servisi. Bu bilgisayara
/// ait bir MAC adresini hedefleyen bir paket yakalandığında, yapılandırılmış
/// güç işlemini (kapat/uyku/hazırda beklet) tetikler.
/// </summary>
public sealed class WolListenerService : BackgroundService
{
    private readonly ILogger<WolListenerService> _logger;
    private readonly WolCatcherOptions _options;

    private List<byte[]> _targetMacs = new();
    private DateTime _startUtc;
    private DateTime _lastActionUtc = DateTime.MinValue;
    private readonly object _triggerLock = new();
    private bool _triggered;

    public WolListenerService(ILogger<WolListenerService> logger, IOptions<WolCatcherOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _startUtc = DateTime.UtcNow;
        _targetMacs = ResolveTargetMacs();

        if (_targetMacs.Count == 0)
        {
            _logger.LogError("Hedef MAC adresi bulunamadı. appsettings.json içindeki 'TargetMacs' alanını doldurun. Servis paket yakalamayacak.");
        }
        else
        {
            _logger.LogInformation("WOL Catcher başladı. İşlem={Action}, BaşlangıçToleransı={Grace}sn. Dinlenen MAC'ler: {Macs}",
                _options.Action, _options.StartupGraceSeconds,
                string.Join(", ", _targetMacs.Select(MagicPacket.Format)));
        }

        var ports = (_options.Ports is { Length: > 0 }) ? _options.Ports : new[] { 9, 7 };
        var tasks = new List<Task>();
        foreach (var port in ports.Distinct())
            tasks.Add(ListenOnPortAsync(port, stoppingToken));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Servis durduruluyor; normal.
        }
    }

    private async Task ListenOnPortAsync(int port, CancellationToken token)
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _logger.LogInformation("UDP {Port} portu dinleniyor.", port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UDP {Port} portu dinlenemedi (port kullanımda olabilir). Bu port atlanıyor.", port);
            udp?.Dispose();
            return;
        }

        using (udp)
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await udp.ReceiveAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "UDP {Port} alımında hata.", port);
                    continue;
                }

                HandleDatagram(result.Buffer, result.RemoteEndPoint, port);
            }
        }
    }

    private void HandleDatagram(byte[] payload, IPEndPoint remote, int port)
    {
        var mac = MagicPacket.TryExtractTargetMac(payload);
        if (mac is null)
            return; // magic packet değil

        bool isOurs = _targetMacs.Any(t => t.AsSpan().SequenceEqual(mac));
        _logger.LogDebug("Magic packet alındı (port {Port}, gönderen {Remote}), hedef MAC {Mac}, bize ait mi: {Ours}",
            port, remote, MagicPacket.Format(mac), isOurs);

        if (!isOurs)
            return;

        var now = DateTime.UtcNow;

        // Başlangıç toleransı: WoL ile açılırken gelen tekrarlı paketlerin
        // makineyi anında kapatmasını önler.
        var sinceStart = now - _startUtc;
        if (sinceStart.TotalSeconds < _options.StartupGraceSeconds)
        {
            _logger.LogInformation("Magic packet yakalandı ({Mac}) ancak başlangıç toleransı içinde ({Elapsed:F0}/{Grace}sn) — yok sayıldı.",
                MagicPacket.Format(mac), sinceStart.TotalSeconds, _options.StartupGraceSeconds);
            return;
        }

        lock (_triggerLock)
        {
            if (_triggered)
                return;

            if ((now - _lastActionUtc).TotalSeconds < _options.CooldownSeconds)
                return;

            _lastActionUtc = now;
            _triggered = true;
        }

        _logger.LogWarning("Hedef magic packet yakalandı (gönderen {Remote}, MAC {Mac}). '{Action}' işlemi {Delay}sn sonra uygulanacak.",
            remote, MagicPacket.Format(mac), _options.Action, _options.ActionDelaySeconds);

        // Alıcı döngüyü bloklamamak için işlemi ayrı bir görevde çalıştır.
        _ = Task.Run(async () =>
        {
            try
            {
                if (_options.ActionDelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(_options.ActionDelaySeconds));
                PowerActions.Execute(_options.Action, _options.Force, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Güç işlemi uygulanırken hata oluştu.");
                _triggered = false; // hata olduysa tekrar denenebilsin
            }
        });
    }

    private List<byte[]> ResolveTargetMacs()
    {
        if (_options.TargetMacs is { Length: > 0 })
        {
            var list = new List<byte[]>();
            foreach (var s in _options.TargetMacs)
            {
                var parsed = MagicPacket.ParseMac(s);
                if (parsed is not null)
                    list.Add(parsed);
                else
                    _logger.LogWarning("Geçersiz MAC adresi yok sayıldı: {Mac}", s);
            }
            if (list.Count > 0)
                return list;
            _logger.LogWarning("Yapılandırılmış MAC'lerin hiçbiri geçerli değil; yerel adaptörlere geçiliyor.");
        }

        return MagicPacket.GetLocalMacs();
    }
}
