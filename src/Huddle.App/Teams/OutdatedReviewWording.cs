using System.Text.Json;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Every Review-stage wording Huddle shipped in a built-in process template (#669, spec D-5): each distinct text found in the git
/// history of the development processes and of the one other process that named a Review state, as an exact string, paired with the
/// wording those files carry now. The data is the embedded <c>OutdatedReviewWording.json</c>, kept out of the C# so the source rules
/// that keep role and Team-type names in data (<c>RoleNeutralityTests</c>, <c>BuiltinHouseholdTypeTests</c>) stay true.
/// <see cref="ReviewStageMigration"/> offers a change only when its find occurs exactly once in a Team's <c>AGENTS.md</c>; nothing here is
/// a fuzzy search. The texts use line feeds; the migration and the retro service fit them to the file's line endings.
/// </summary>
internal static class OutdatedReviewWording
{
    private const string ResourceName = "Teams/OutdatedReviewWording.json";

    private const string AgentsMd = "AGENTS.md";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<RetroChange, string[]> Contexts = [];

    /// <summary>Every change, in the order a Team is offered them: stages, roles, missing roles, gates, Definition of Done, then the last process line.</summary>
    public static IReadOnlyList<RetroChange> All { get; } = Load();

    /// <summary>The title lines a file must carry, any one of them, for <paramref name="change"/> to be offered: its find also occurs in a process Huddle did not rewrite, or in two that it rewrote differently.</summary>
    /// <param name="change">One of <see cref="All"/>.</param>
    /// <returns>The frontmatter title lines, or <see langword="null"/> when the find alone is enough.</returns>
    public static IReadOnlyList<string>? ContextFor(RetroChange change) => Contexts.GetValueOrDefault(change);

    private static List<RetroChange> Load()
    {
        using Stream stream = typeof(OutdatedReviewWording).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        List<Entry> entries = JsonSerializer.Deserialize<List<Entry>>(stream, JsonOptions) ?? [];
        List<RetroChange> changes = [];
        foreach (Entry entry in entries)
        {
            foreach (string find in entry.Finds)
            {
                RetroChange change = new("0", entry.Title, AgentsMd, find, entry.Replace);
                changes.Add(change);
                if (entry.OnlyIn is { Count: > 0 } titleLines)
                {
                    Contexts[change] = [.. titleLines];
                }
            }
        }

        return changes;
    }

    /// <summary>One row of the data file: the finds that share a replacement, and the process titles the row is limited to.</summary>
    /// <param name="Title">A short name for the change.</param>
    /// <param name="Finds">Each shipped wording that the replacement stands in for.</param>
    /// <param name="Replace">The current wording.</param>
    /// <param name="OnlyIn">Frontmatter title lines, any one of which the file must carry; none means the find alone is enough.</param>
    private sealed record Entry(string Title, List<string> Finds, string Replace, List<string>? OnlyIn = null);
}
