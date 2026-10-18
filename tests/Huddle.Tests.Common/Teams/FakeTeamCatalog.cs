using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// A hand-driven <see cref="ITeamCatalog"/> for tests that need a Team list they control without
/// any timing: the test sets <see cref="Teams"/>, calls <see cref="Raise"/> to fire
/// <see cref="Changed"/>, and reads <see cref="ChangedSubscriberCount"/> to prove a component
/// subscribed (and unsubscribed).
/// </summary>
internal sealed class FakeTeamCatalog : ITeamCatalog
{
    private Action? changed;
    private int changedSubscriberCount;

    /// <summary>The Teams the fake reports. Set it before the code under test reads it.</summary>
    public IReadOnlyList<TeamSummary> Teams { get; set; } = [];

    /// <summary>The number of handlers currently attached to <see cref="Changed"/>.</summary>
    public int ChangedSubscriberCount => Volatile.Read(ref this.changedSubscriberCount);

    /// <inheritdoc />
    public event Action? Changed
    {
        add
        {
            this.changed += value;
            Interlocked.Increment(ref this.changedSubscriberCount);
        }
        remove
        {
            this.changed -= value;
            Interlocked.Decrement(ref this.changedSubscriberCount);
        }
    }

    /// <summary>Fires <see cref="Changed"/> once, as the live catalog does after a rebuild.</summary>
    public void Raise() => this.changed?.Invoke();

    /// <inheritdoc />
    public TeamSummary? Find(string team) =>
        this.Teams.FirstOrDefault(t => string.Equals(t.Name, team, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public bool ProjectExists(string team, string project) =>
        this.Find(team)?.Projects.Contains(project, StringComparer.OrdinalIgnoreCase) ?? false;
}
