using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// A hand-driven <see cref="ITeamMembership"/> for UI tests: it records every call and answers with the
/// <see cref="MembershipResult"/> the test sets (a plain <see cref="MembershipOutcome.Added"/> by default).
/// It touches no Persona file.
/// </summary>
internal sealed class FakeTeamMembership : ITeamMembership
{
    private readonly Lock gate = new();
    private readonly List<string> calls = [];

    /// <summary>What <see cref="Add"/> and <see cref="Remove"/> return. Set it before the code under test calls.</summary>
    public MembershipResult MembershipResult { get; set; } = new(MembershipOutcome.Added);

    /// <summary>When set, <see cref="Remove"/> records its call and then blocks until the test releases this semaphore.</summary>
    public SemaphoreSlim? Hold { get; set; }

    /// <summary>A snapshot of the calls so far, in order: <c>Add:{team}/{persona}</c> or <c>Remove:{team}/{persona}</c>.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (this.gate)
            {
                return [.. this.calls];
            }
        }
    }

    /// <inheritdoc />
    public MembershipResult Add(string team, string personaName)
    {
        lock (this.gate)
        {
            this.calls.Add($"Add:{team}/{personaName}");
        }

        return this.MembershipResult;
    }

    /// <inheritdoc />
    public MembershipResult Remove(string team, string personaName)
    {
        lock (this.gate)
        {
            this.calls.Add($"Remove:{team}/{personaName}");
        }

        this.Hold?.Wait();
        return this.MembershipResult;
    }
}
