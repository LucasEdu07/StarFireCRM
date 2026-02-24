using System;
using System.IO;
using System.Text;
using ExtintorCrm.App.Infrastructure;

namespace ExtintorCrm.App.Infrastructure.Logging;

public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message, ex);
    }

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            AppDataPaths.EnsureInitialized();
            var logDir = Path.Combine(AppDataPaths.DataDirectory, "logs");
            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
            sb.Append(level).Append(" | ").Append(message);
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append(ex);
            }

            lock (SyncRoot)
            {
                File.AppendAllText(logFile, sb.ToString() + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging nunca deve quebrar o fluxo principal.
        }
    }
}
