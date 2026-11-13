using System.Globalization;

namespace Agency.Huddle.App.Teams;

/// <summary>The one sentence a tool answers when it is given the old name of a Team that was renamed (Team Rename Spec section 6.3).</summary>
internal static class RenamedTeamRefusal
{
    /// <summary>Words the refusal for a Team name the catalog does not know.</summary>
    /// <param name="renames">The rename journal, or <see langword="null"/> when none is registered.</param>
    /// <param name="asked">The Team name the tool was given.</param>
    /// <returns><c>Team 'Old' was renamed to 'New'. Use 'New'.</c>, or <see langword="null"/> when the name was never renamed.</returns>
    public static string? For(ITeamRenames? renames, string? asked)
    {
        if (renames is null || string.IsNullOrWhiteSpace(asked))
        {
            return null;
        }

        string name = asked.Trim();
        string today = renames.Resolve(name);
        return string.Equals(today, name, StringComparison.OrdinalIgnoreCase)
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"Team '{name}' was renamed to '{today}'. Use '{today}'.");
    }
}
