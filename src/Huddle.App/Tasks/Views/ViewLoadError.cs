using System.Text.Json;

namespace Agency.Huddle.App.Tasks.Views;

/// <summary>A parse failure in <c>views.json</c>, with a 1-based line and column.</summary>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based line the failure was found on, or <see langword="null"/> when <see cref="JsonException"/> did not report one.</param>
/// <param name="Column">The 1-based column the failure was found on, or <see langword="null"/> when <see cref="JsonException"/> did not report one.</param>
public sealed record ViewLoadError(string Message, long? Line, long? Column);
