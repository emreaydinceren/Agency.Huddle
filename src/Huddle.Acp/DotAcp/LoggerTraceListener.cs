namespace Agency.Huddle.Acp.DotAcp;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

/// <summary>Forwards <see cref="TraceListener"/> writes to an <see cref="ILogger"/> at Trace level, used to surface dotacp wire tracing when <see cref="DotAcpHostOptions.TraceWire"/> is enabled.</summary>
internal sealed partial class LoggerTraceListener(ILogger logger) : TraceListener
{
    public override void Write(string? message)
    {
        LoggerTraceListener.LogTraceMessage(logger, message ?? string.Empty);
    }

    public override void WriteLine(string? message)
    {
        LoggerTraceListener.LogTraceMessage(logger, message ?? string.Empty);
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "{Message}")]
    private static partial void LogTraceMessage(ILogger logger, string message);
}