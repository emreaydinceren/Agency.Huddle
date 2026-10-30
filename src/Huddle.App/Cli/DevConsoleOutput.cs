namespace Agency.Huddle.App.Cli;

/// <summary>The writer the development announcer uses (default <c>Console.Out</c>); tests replace it to see that nothing is written.</summary>
/// <param name="Writer">The writer.</param>
internal sealed record DevConsoleOutput(TextWriter Writer);
