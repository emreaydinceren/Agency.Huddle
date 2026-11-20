using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Seeder.Model;

/// <summary>One extra entry on a Task's timeline, beyond the ones derived from its status: an event by default, or a comment or an Evidence line.</summary>
/// <param name="Actor">Who did it.</param>
/// <param name="Summary">The entry text: an app-style event summary such as <c>priority: Medium → High</c>, a comment's words, or an Evidence line's fields after the author, such as <c>dotnet test | exit 0</c>.</param>
internal sealed record SeedTimelineEvent(string Actor, string Summary)
{
    /// <summary>What the entry is; an <see cref="TimelineEntryKind.Event"/> unless the scenario says otherwise.</summary>
    public TimelineEntryKind Kind { get; init; } = TimelineEntryKind.Event;

    /// <summary>
    /// Whether the entry is written before the Task's last status change instead of after all of them, so Evidence on a finished Task
    /// comes before the move to Done.
    /// </summary>
    public bool BeforeLastStatus { get; init; }
}
