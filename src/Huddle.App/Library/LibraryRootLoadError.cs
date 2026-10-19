namespace Agency.Huddle.App.Library;

/// <summary>A parse failure in <c>library-roots.json</c>, with a 1-based line and column when known.</summary>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Line">The 1-based line the failure was found on, or <see langword="null"/> when unknown.</param>
/// <param name="Column">The 1-based column the failure was found on, or <see langword="null"/> when unknown.</param>
public sealed record LibraryRootLoadError(string Message, long? Line, long? Column);
