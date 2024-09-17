using System.Text;

namespace CustomAssetsBackend.Misc;

public static class Logger
{
    public enum LogLevel
    {
        Generic,
        Debug,
        Exception
    }
    
    public static Action<string>? LogAction { get; set; }
    
    public static Action<string, Exception>? ExceptionAction { get; set; }

    public static string ExceptionLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAIExceptionLog.txt");

    public static void Log(string message, LogLevel logLevel = LogLevel.Generic, Exception? err = null)
    {
        switch (logLevel)
        {
            case LogLevel.Generic:
                if (LogAction != null)
                    LogAction.Invoke(message);
                else
                    Console.WriteLine(message);
                
                break;
            case LogLevel.Debug:
#if DEBUG
                Console.WriteLine(message);
#endif
                break;
            case LogLevel.Exception:
                ExceptionAction?.Invoke(message, err ?? new Exception("No exception was passed."));
                
                var sb = new StringBuilder();
                sb.AppendLine("Time: " + DateTime.Now);
                sb.AppendLine("Log message: " + message);
                sb.AppendLine("Exception:");
                sb.AppendLine(err?.ToString() ?? "No exception was passed.");
                sb.AppendLine();
                File.AppendAllText(ExceptionLogFilePath, sb.ToString());
                
                Console.WriteLine(message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }
}