namespace Agency.Huddle.Console.Terminal;

using System;

/// <summary>Writes to <see cref="System.Console"/>, colouring each write according to its <see cref="ConsoleStyle"/>.</summary>
internal sealed class SystemConsoleOutput : IConsoleOutput
{
    public void Write(string text, ConsoleStyle style)
    {
        ConsoleColor? color = SystemConsoleOutput.ColorFor(style);
        if (color is null)
        {
            System.Console.Write(text);
            return;
        }

        ConsoleColor previous = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color.Value;
        try
        {
            System.Console.Write(text);
        }
        finally
        {
            System.Console.ForegroundColor = previous;
        }
    }

    public void WriteLine(string text, ConsoleStyle style)
    {
        ConsoleColor? color = SystemConsoleOutput.ColorFor(style);
        if (color is null)
        {
            System.Console.WriteLine(text);
            return;
        }

        ConsoleColor previous = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color.Value;
        try
        {
            System.Console.WriteLine(text);
        }
        finally
        {
            System.Console.ForegroundColor = previous;
        }
    }

    private static ConsoleColor? ColorFor(ConsoleStyle style)
    {
        return style switch
        {
            ConsoleStyle.Default => null,
            ConsoleStyle.Agent => ConsoleColor.White,
            ConsoleStyle.Thought => ConsoleColor.DarkGray,
            ConsoleStyle.Tool => ConsoleColor.Cyan,
            ConsoleStyle.Plan => ConsoleColor.Yellow,
            ConsoleStyle.Usage => ConsoleColor.DarkGray,
            ConsoleStyle.Prompt => ConsoleColor.Green,
            ConsoleStyle.Error => ConsoleColor.Red,
            ConsoleStyle.Info => ConsoleColor.DarkCyan,
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Unknown console style."),
#pragma warning restore HAA0601
        };
    }
}
