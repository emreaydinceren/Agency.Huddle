namespace Agency.Huddle.Console.Terminal;

/// <summary>A seam over console output so tests can record writes instead of touching a real console.</summary>
internal interface IConsoleOutput
{
    void Write(string text, ConsoleStyle style);

    void WriteLine(string text, ConsoleStyle style);
}
