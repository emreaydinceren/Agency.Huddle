using System.Globalization;
using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The identifier of a Sprint: <c>S2</c>. A value type with value equality, so it is a dictionary key
/// without a comparer, and it cannot be mistaken for a <see cref="TaskId"/>, whose grammar requires a dash
/// (Sprints spec §7.1).
/// </summary>
public readonly partial record struct SprintId
{
    /// <summary>Creates the id of the Sprint with this number.</summary>
    /// <param name="number">The Sprint's number, at least 1.</param>
    public SprintId(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        this.Number = number;
    }

    /// <summary>The Sprint's number: 2 for <c>S2</c>. Zero only for <see langword="default"/>.</summary>
    public int Number { get; }

    /// <summary>Returns the id as it is written in files: an upper-case S and the number, with no padding.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"S{this.Number}");

    /// <summary>Parses an id (case-insensitive, trims whitespace). The number must be 1 to 999999999.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="id">The parsed id, or <see langword="default"/> when this returns <see langword="false"/>.</param>
    public static bool TryParse(string? text, out SprintId id)
    {
        id = default;
        if (text is null)
        {
            return false;
        }

        Match match = SprintIdRegex().Match(text.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            || number < 1)
        {
            return false;
        }

        id = new(number);
        return true;
    }

    /// <summary>The grammar of an id: an S and one to nine digits.</summary>
    [GeneratedRegex(@"\A[Ss]([0-9]{1,9})\z", RegexOptions.CultureInvariant)]
    private static partial Regex SprintIdRegex();
}
