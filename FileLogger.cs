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
    private bool _fileCreated = false;

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
                
                // If file doesn't exist, create it with UTF-8 BOM
                if (!_fileCreated)
                {
                    if (!info.Exists)
                    {
                        File.AppendAllText(_path, string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    }
                    _fileCreated = true;
                }
                
                // Handle log rotation
                if (info.Exists && info.Length > MaxBytes)
                {
                    var backup = _path + ".1";
                    File.Delete(backup);
                    File.Move(_path, backup);
                }
                
                // Append using UTF-8 without BOM to avoid corrupting the file
                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { /* günlükleme hataları sessizce yutulur */ }
        }
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

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
