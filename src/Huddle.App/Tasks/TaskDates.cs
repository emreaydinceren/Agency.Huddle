namespace Agency.Huddle.App.Tasks;

using System.Globalization;

/// <summary>
/// The single date format every Tasks UI surface uses when it shows a date to a Human: the
/// invariant <c>yyyy-MM-dd</c> (Settled decision J53). The Change log is the one exception and
/// keeps its own local <c>yyyy-MM-dd HH:mm</c> format.
/// </summary>
internal static class TaskDates
{
    /// <summary>The invariant format string every Task date renders with.</summary>
    public const string Format = "yyyy-MM-dd";

    /// <summary>Formats <paramref name="date"/> as <c>yyyy-MM-dd</c>, or an empty string when <see langword="null"/>.</summary>
    /// <param name="date">The date to format.</param>
    public static string Display(DateOnly? date) => date?.ToString(Format, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Formats <paramref name="date"/> as <c>yyyy-MM-dd</c>, or an empty string when <see langword="null"/>.</summary>
    /// <param name="date">The date to format.</param>
    public static string Display(DateTimeOffset? date) => date?.ToString(Format, CultureInfo.InvariantCulture) ?? string.Empty;
}
