using System.Text;
using Microsoft.Extensions.Logging;

namespace WolCatcher;

/// <summary>
/// Günlükleri basit bir metin dosyasına yazan minimal ILoggerProvider.
/// Dosya 5 MB'ı aştığında bir önceki kopyaya (.1) döndürülür.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private readonly string _path;
    private readonly object _lock = new();

    // Dosya yeni ya da boşken başına BOM yazılır (Not Defteri Türkçe karakterleri doğru
    // göstersin diye); dolu bir dosyaya eklerken StreamWriter BOM yazmaz.
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public FileLoggerProvider(string path)
    {
        _path = path;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); } catch { /* yoksay */ }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, this);

    public void Dispose() { }

    internal void Write(string line)
    {
        lock (_lock)
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var backup = _path + ".1";
                    File.Delete(backup);
                    File.Move(_path, backup);
                }
                File.AppendAllText(_path, line + Environment.NewLine, Utf8WithBom);
            }
            catch { /* günlükleme hataları sessizce yutulur */ }
        }
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Hangi seviyelerin yazılacağına appsettings.json içindeki Logging bölümü karar verir.
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var shortCat = category.Contains('.') ? category[(category.LastIndexOf('.') + 1)..] : category;
            var level = logLevel.ToString().ToUpperInvariant();
            var msg = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level,-11}] {shortCat}: {formatter(state, exception)}";
            if (exception is not null)
                msg += Environment.NewLine + exception;
            provider.Write(msg);
        }
    }
}
