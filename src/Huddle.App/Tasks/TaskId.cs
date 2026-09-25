using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Tasks;

/// <summary>A unique identifier for a Task: "PLAT-0042".</summary>
public readonly partial record struct TaskId(string Prefix, int Number)
{
    /// <summary>Prefix is always upper case.</summary>
    public string Prefix { get; } = Prefix.ToUpperInvariant();

    /// <summary>Returns the string representation: "PLAT-0042", padding the number to at least 4 digits.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{this.Prefix}-{this.Number:D4}");

    /// <summary>Parses a TaskId from a string (case-insensitive, trims whitespace).</summary>
    public static bool TryParse(string? text, out TaskId id)
    {
        if (text is null)
        {
            id = default;
            return false;
        }

        text = text.Trim();
        if (text.Length == 0)
        {
            id = default;
            return false;
        }

        Match match = TaskIdRegex().Match(text);
        if (!match.Success)
        {
            id = default;
            return false;
        }

        string prefix = match.Groups[1].Value.ToUpperInvariant();
        if (!int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out int number))
        {
            id = default;
            return false;
        }

        id = new(prefix, number);
        return true;
    }

    /// <summary>Generated regex for TaskId format: letter followed by 0-7 alphanumeric characters, dash, 1-9 digits.</summary>
    [GeneratedRegex(@"\A([A-Za-z][A-Za-z0-9]{0,7})-([0-9]{1,9})\z", RegexOptions.CultureInvariant)]
    private static partial Regex TaskIdRegex();
}
