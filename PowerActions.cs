using System.Diagnostics;
using System.Runtime.InteropServices;
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

    public static void Execute(string action, bool force, ILogger logger)
    {
        switch (action?.Trim().ToLowerInvariant())
        {
            case "sleep":
                logger.LogInformation("İşlem: Uyku moduna geçiliyor.");
                // disableWakeEvent=false => WoL/uyandırma olayları aktif kalır.
                if (!SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false))
                    logger.LogError("SetSuspendState başarısız (Win32 hata kodu {Code}).", Marshal.GetLastWin32Error());
                break;

            case "hibernate":
                logger.LogInformation("İşlem: Hazırda bekletiliyor.");
                if (!SetSuspendState(hibernate: true, forceCritical: false, disableWakeEvent: false))
                {
                    logger.LogWarning("SetSuspendState(hibernate) başarısız, shutdown.exe /h deneniyor.");
                    RunShutdown("/h", logger);
                }
                break;

            case "shutdown":
            default:
                logger.LogInformation("İşlem: Bilgisayar kapatılıyor.");
                // /s = kapat, /f = uygulamaları zorla kapat, /t 0 = beklemeden.
                RunShutdown(force ? "/s /f /t 0" : "/s /t 0", logger);
                break;
        }
    }

    private static void RunShutdown(string args, ILogger logger)
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
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                logger.LogError("shutdown.exe başlatılamadı.");
                return;
            }
            proc.WaitForExit(10_000);
            if (proc.ExitCode != 0)
            {
                var err = proc.StandardError.ReadToEnd();
                logger.LogError("shutdown.exe çıkış kodu {Code}: {Err}", proc.ExitCode, err.Trim());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "shutdown.exe çalıştırılırken hata oluştu.");
        }
    }
}
