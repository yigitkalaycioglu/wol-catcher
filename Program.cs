using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WolCatcher;

// --- Komut satırı (servis yönetimi) ---
if (args.Length > 0)
{
    try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { /* konsol yoksa yoksay */ }

    var cmd = args[0].ToLowerInvariant();
    switch (cmd)
    {
        case "install":
            if (!ServiceControl.EnsureAdmin(args)) return 0;
            { var rc = ServiceControl.Install(args); ServiceControl.KeepOpenIfNeeded(args); return rc; }

        case "uninstall":
        case "remove":
            if (!ServiceControl.EnsureAdmin(args)) return 0;
            { var rc = ServiceControl.Uninstall(); ServiceControl.KeepOpenIfNeeded(args); return rc; }

        case "start":
            if (!ServiceControl.EnsureAdmin(args)) return 0;
            { var rc = ServiceControl.Start(); ServiceControl.KeepOpenIfNeeded(args); return rc; }

        case "stop":
            if (!ServiceControl.EnsureAdmin(args)) return 0;
            { var rc = ServiceControl.Stop(); ServiceControl.KeepOpenIfNeeded(args); return rc; }

        case "status":
        case "query":
            { var rc = ServiceControl.Status(); ServiceControl.KeepOpenIfNeeded(args); return rc; }

        case "help":
        case "-h":
        case "--help":
        case "/?":
            ServiceControl.PrintHelp();
            return 0;

        case "run":
        case "console":
            break; // host'u çalıştırmaya devam et

        default:
            Console.WriteLine($"Bilinmeyen komut: {cmd}");
            ServiceControl.PrintHelp();
            return 1;
    }
}

// --- Host'u oluştur ve çalıştır (servis veya konsol) ---
var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory // servis modunda appsettings.json'u exe yanından oku
};
var builder = Host.CreateApplicationBuilder(settings);

builder.Services.AddWindowsService(o => o.ServiceName = ServiceControl.ServiceName);
builder.Services.Configure<WolCatcherOptions>(
    builder.Configuration.GetSection(WolCatcherOptions.SectionName));
builder.Services.AddHostedService<WolListenerService>();

// Dosya günlüğü ekle: %ProgramData%\WolCatcher\wolcatcher.log
var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "WolCatcher", "wolcatcher.log");
builder.Logging.AddProvider(new FileLoggerProvider(logPath));

var host = builder.Build();
host.Run();
return 0;
