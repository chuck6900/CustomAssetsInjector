using System;

namespace LibCpp2IL.Logging;

public class CallbackWriter : LogWriter
{
    public enum LogLevel
    {
        Verbose,
        Info,
        Warn,
        Error
    }
    
    public Action<string, LogLevel>? LogCallback;
    
    public override void Info(string message)
    {
        LogCallback?.Invoke("Cpp2IL: " + message, LogLevel.Info);
    }

    public override void Warn(string message)
    {
        LogCallback?.Invoke("Cpp2IL - Warning: " + message, LogLevel.Warn);
    }

    public override void Error(string message)
    {
        LogCallback?.Invoke("Cpp2IL - Error: " + message, LogLevel.Error);
    }

    public override void Verbose(string message)
    {
        LogCallback?.Invoke("Cpp2IL - Verbose: " + message, LogLevel.Verbose);
    }
}
