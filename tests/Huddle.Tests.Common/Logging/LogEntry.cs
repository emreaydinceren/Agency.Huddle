using Microsoft.Extensions.Logging;

namespace Agency.Huddle.Tests.Elicitation;

/// <summary>One entry a recording logger captured.</summary>
/// <param name="Level">The severity the entry was logged at.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception passed to the log call, or <see langword="null"/>.</param>
/// <param name="Category">The logger category, or <see langword="null"/> when the logger has none.</param>
internal readonly record struct LogEntry(LogLevel Level, string Message, Exception? Exception, string? Category);
