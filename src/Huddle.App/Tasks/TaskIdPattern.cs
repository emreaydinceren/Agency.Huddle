using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Tasks;

/// <summary>
/// The one pattern that finds a stand-alone Task id in text: the renderer links it and the reference scanner
/// reads it (Tasks spec §13.13.2, D-32). A candidate is checked with <see cref="TaskId.TryParse"/> before use.
/// </summary>
internal static partial class TaskIdPattern
{
    /// <summary>Matches an upper-case Task id that is not glued to a letter, digit, underscore or dash on either side.</summary>
    /// <returns>The regex; group 1 is the id.</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9_-])([A-Z][A-Z0-9]{0,7}-[0-9]{1,9})(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    internal static partial Regex Candidate();
}
