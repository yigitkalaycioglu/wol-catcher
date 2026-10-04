using System.Diagnostics;
using System.Security.Principal;

namespace WolCatcher;

/// <summary>
/// Windows servisini sc.exe aracılığıyla kuran/kaldıran/başlatan yardımcılar.
/// Yönetici hakkı gerektiren komutlarda otomatik olarak UAC ile yükseltme yapar.
/// </summary>
public static class ServiceControl
{
    public const string ServiceName = "WolCatcher";
    public const string DisplayName = "Wake-on-LAN Catcher";
    private const string Description =
        "Bu bilgisayar açıkken alınan Wake-on-LAN magic packet'lerini yakalar ve bilgisayarı kapatır/uyutur.";

    private static string ExePath => Environment.ProcessPath
        ?? throw new InvalidOperationException("Çalıştırılabilir dosya yolu belirlenemedi.");

    public static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>
    /// Yönetici değilse kendini UAC ile yeniden başlatır. Yükseltme yapıldıysa
    /// false döner (çağıran çıkmalıdır); zaten yöneticiyse true döner.
    /// </summary>
    public static bool EnsureAdmin(string[] args)
    {
        if (IsAdmin())
            return true;

        Console.WriteLine("Bu komut yönetici hakları gerektiriyor. Yükseltme isteniyor (UAC)...");

        var relaunchArgs = args.Append("--keep-open").ToArray();
        var psi = new ProcessStartInfo
        {
            FileName = ExePath,
            UseShellExecute = true,
            Verb = "runas",
            Arguments = string.Join(' ', relaunchArgs.Select(QuoteArg))
        };

        try
        {
            using var p = Process.Start(psi);
            p?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Yönetici izni alınamadı: " + ex.Message);
            Console.WriteLine("Lütfen PowerShell'i 'Yönetici olarak çalıştır' ile açıp komutu tekrar deneyin.");
        }
        return false;
    }

    public static void KeepOpenIfNeeded(string[] args)
    {
        if (args.Contains("--keep-open"))
        {
            Console.WriteLine();
            Console.WriteLine("Bu pencereyi kapatmak için bir tuşa basın...");
            try { Console.ReadKey(true); } catch { }
        }
    }

    public static int Install(string[] args)
    {
        Console.WriteLine($"'{ServiceName}' servisi kuruluyor...");
        Console.WriteLine($"Çalıştırılabilir: {ExePath}");

        // Servisi oluştur. binPath değeri, yolda boşluk olduğu için tırnak içine alınır.
        var rc = RunSc("create", ServiceName,
            "binPath=", $"\"{ExePath}\"",
            "start=", "auto",
            "DisplayName=", DisplayName);

        if (rc != 0)
        {
            Console.WriteLine("Servis oluşturulamadı. Zaten kurulu olabilir; önce 'uninstall' deneyin.");
            return rc;
        }

        RunSc("description", ServiceName, Description);
        // Çökme/durma durumunda otomatik yeniden başlat (5 sn sonra, sayaç 1 günde sıfırlanır).
        RunSc("failure", ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/5000/restart/5000");

        Console.WriteLine("Servis başlatılıyor...");
        RunSc("start", ServiceName);

        Console.WriteLine();
        Console.WriteLine($"Tamamlandı. '{ServiceName}' kuruldu ve otomatik başlatmaya ayarlandı.");
        return 0;
    }

    public static int Uninstall()
    {
        Console.WriteLine($"'{ServiceName}' servisi kaldırılıyor...");
        RunSc("stop", ServiceName);        // çalışmıyorsa hata önemsiz
        var rc = RunSc("delete", ServiceName);
        if (rc == 0)
            Console.WriteLine("Servis kaldırıldı.");
        return rc;
    }

    public static int Start() => RunSc("start", ServiceName);
    public static int Stop() => RunSc("stop", ServiceName);
    public static int Status() => RunSc("query", ServiceName);

    public static void PrintHelp()
    {
        Console.WriteLine($"""
            Wake-on-LAN Catcher ({ServiceName})

            Kullanım:
              WolCatcher install     Servisi kur ve başlat (yönetici gerekir)
              WolCatcher uninstall   Servisi durdur ve kaldır (yönetici gerekir)
              WolCatcher start       Servisi başlat
              WolCatcher stop        Servisi durdur
              WolCatcher status      Servis durumunu göster
              WolCatcher console     Servis olarak değil, bu pencerede çalıştır (test için)
              WolCatcher help        Bu yardımı göster

            Ayarlar appsettings.json (çalıştırılabilir dosyanın yanında) üzerinden yapılır.
            Günlük: %ProgramData%\WolCatcher\wolcatcher.log
            """);
    }

    private static int RunSc(params string[] scArgs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var a in scArgs)
            psi.ArgumentList.Add(a);

        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(15_000);

            if (!string.IsNullOrWhiteSpace(stdout))
                Console.WriteLine(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr))
                Console.WriteLine(stderr.Trim());
            return p.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("sc.exe çalıştırılamadı: " + ex.Message);
            return 1;
        }
    }

    private static string QuoteArg(string arg) =>
        arg.Contains(' ') || arg.Contains('"') ? "\"" + arg.Replace("\"", "\\\"") + "\"" : arg;
}
