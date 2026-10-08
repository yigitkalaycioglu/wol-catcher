using Microsoft.Extensions.Logging;
using Xunit;

namespace WolCatcher.Tests;

public class FileLoggerTests
{
    private static string WriteLog(LogLevel minimum, Action<ILogger> write)
    {
        var path = Path.Combine(Path.GetTempPath(), $"wolcatcher-test-{Guid.NewGuid():N}.log");
        using (var factory = LoggerFactory.Create(b =>
               {
                   b.SetMinimumLevel(minimum);
                   b.AddProvider(new FileLoggerProvider(path));
               }))
        {
            write(factory.CreateLogger("Test"));
        }
        return path;
    }

    [Fact]
    public void Yapilandirmadaki_seviyenin_altindaki_satirlar_yazilmaz()
    {
        var path = WriteLog(LogLevel.Information, log =>
        {
            log.LogDebug("görünmemeli");
            log.LogInformation("ilk satır");
            log.LogWarning("ikinci satır");
        });
        try
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("görünmemeli", text);
            Assert.Contains("ilk satır", text);
            Assert.Contains("ikinci satır", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Debug_seviyesi_acilinca_debug_satirlari_da_yazilir()
    {
        var path = WriteLog(LogLevel.Debug, log => log.LogDebug("ayrıntı"));
        try
        {
            Assert.Contains("ayrıntı", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Bom_sadece_dosyanin_basinda_olur()
    {
        var path = WriteLog(LogLevel.Information, log =>
        {
            log.LogInformation("bir");
            log.LogInformation("iki");
        });
        try
        {
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            Assert.DoesNotContain('﻿', File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
