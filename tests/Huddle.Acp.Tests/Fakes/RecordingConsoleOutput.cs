namespace Agency.Huddle.Acp.Tests.Fakes;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Agency.Huddle.Console.Terminal;

/// <summary>Records every <see cref="IConsoleOutput"/> write, in order, for assertion by tests.</summary>
internal sealed class RecordingConsoleOutput : IConsoleOutput
{
    private readonly Lock gate = new Lock();

    private readonly List<(string Text, ConsoleStyle Style, bool IsLine)> entries = new List<(string Text, ConsoleStyle Style, bool IsLine)>();

    /// <summary>Every write, in order, exactly as it was passed to <see cref="Write"/> or <see cref="WriteLine"/>.</summary>
    internal IReadOnlyList<(string Text, ConsoleStyle Style)> Segments
    {
        get
        {
            lock (this.gate)
            {
                return this.entries.Select(entry => (entry.Text, entry.Style)).ToArray();
            }
        }
    }

    /// <summary>The concatenation of every write, with a newline appended after each <see cref="WriteLine"/>.</summary>
    internal string Text
    {
        get
        {
            lock (this.gate)
            {
                StringBuilder builder = new StringBuilder();
                foreach ((string text, ConsoleStyle _, bool isLine) in this.entries)
                {
                    builder.Append(text);
                    if (isLine)
                    {
                        builder.Append('\n');
                    }
                }

                return builder.ToString();
            }
        }
    }

    /// <summary>A line-by-line view of <see cref="Text"/>.</summary>
    internal IReadOnlyList<string> Lines
    {
        get
        {
            string[] parts = this.Text.Split('\n');
            if (parts.Length > 0 && parts[^1].Length == 0)
            {
                return parts[..^1];
            }

            return parts;
        }
    }

    public void Write(string text, ConsoleStyle style)
    {
        lock (this.gate)
        {
            this.entries.Add((text, style, false));
        }
    }

    public void WriteLine(string text, ConsoleStyle style)
    {
        lock (this.gate)
        {
            this.entries.Add((text, style, true));
        }
    }
}
