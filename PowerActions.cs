using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WolCatcher;

/// <summary>
/// Bilgisayarı kapatma / uyku / hazırda bekletme işlemlerini gerçekleştirir.
/// </summary>
public static class PowerActions
{
    // powrprof.dll içindeki SetSuspendState: uyku/hazırda bekletme için.
    // hibernate=true -> hazırda beklet, false -> uyku.
    [DllImport("powrprof.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();

    /// <summary>İşlemi uygular, başarılı olursa true döner.</summary>
    public static bool Execute(string action, bool force, ILogger logger)
    {
        switch (action?.Trim().ToLowerInvariant())
        {
            case "sleep":
                logger.LogInformation("İşlem: Uyku moduna geçiliyor.");
                // disableWakeEvent=false => WoL/uyandırma olayları aktif kalır.
                if (SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false))
                    return true;
                logger.LogError("SetSuspendState başarısız (Win32 hata kodu {Code}).", Marshal.GetLastWin32Error());
                return false;

            case "hibernate":
                logger.LogInformation("İşlem: Hazırda bekletiliyor.");
                if (SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false))
                    return true;
                logger.LogWarning("SetSuspendState(hibernate) başarısız, shutdown.exe /h deneniyor.");
                return RunShutdown("/h", logger);

            case "shutdown":
            default:
                logger.LogInformation("İşlem: Bilgisayar kapatılıyor.");
                // /s = kapat, /f = uygulamaları zorla kapat, /t 0 = beklemeden.
                return RunShutdown(force ? "/s /f /t 0" : "/s /t 0", logger);
        }
    }

    private static bool RunShutdown(string args, ILogger logger)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                StandardErrorEncoding = OemEncoding()
            };
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                logger.LogError("shutdown.exe başlatılamadı.");
                return false;
            }
            var stderr = proc.StandardError.ReadToEndAsync();
            if (!proc.WaitForExit(10_000))
            {
                logger.LogError("shutdown.exe 10 sn içinde bitmedi.");
                return false;
            }
            if (proc.ExitCode == 0)
                return true;
            logger.LogError("shutdown.exe çıkış kodu {Code}: {Err}", proc.ExitCode, stderr.Result.Trim());
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "shutdown.exe çalıştırılırken hata oluştu.");
            return false;
        }
    }

    // shutdown.exe mesajlarını sistemin OEM kod sayfasıyla yazıyor (Türkçe Windows'ta 857).
    // UTF-8 olarak okununca Türkçe karakterler logda bozuk görünüyordu.
    private static Encoding OemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
