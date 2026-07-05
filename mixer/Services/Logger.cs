using System;
using System.IO;
using System.Text;

namespace mixer.Services
{
    /// <summary>
    /// Very simple, thread-safe file logger.
    /// Writes to %LocalAppData%\mixer\app_log.txt.
    /// Never throws -- logging failures must never crash the app.
    /// </summary>
    public static class Logger
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mixer");

        private static readonly string _logFilePath = Path.Combine(_logDirectory, "app_log.txt");

        static Logger()
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);
            }
            catch
            {
                // If we can't even create the directory, logging is best-effort only.
            }
        }

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message) => Write("ERROR", message);

        public static void Error(string message, Exception ex) =>
            Write("ERROR", $"{message} :: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        public static void Debug(string message) => Write("DEBUG", message);

        private static void Write(string level, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(_logFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Swallow -- logging must never be the cause of a crash.
            }
        }
    }
}
