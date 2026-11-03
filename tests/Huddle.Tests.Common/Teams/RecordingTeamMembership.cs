using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// An <see cref="ITeamMembership"/> that records every call and changes nothing: the archive tests read
/// <see cref="Calls"/> to see which labels the service asked to remove, and <see cref="OnCall"/> to see
/// what the disk looked like at that moment.
/// </summary>
internal sealed class RecordingTeamMembership : ITeamMembership
{
    private readonly List<MembershipCall> calls = [];

    /// <summary>Gets the calls in the order they were made.</summary>
    public IReadOnlyList<MembershipCall> Calls => this.calls;

    /// <summary>Gets or sets a callback run at the start of every call, before it is recorded; <see langword="null"/> runs nothing.</summary>
    public Action<MembershipCall>? OnCall { get; set; }

    /// <summary>Gets or sets the outcome every call returns.</summary>
    public MembershipOutcome Outcome { get; set; } = MembershipOutcome.Removed;

    /// <inheritdoc />
    public MembershipResult Add(string team, string personaName) => this.Record(new MembershipCall("Add", team, personaName));

    /// <inheritdoc />
    public MembershipResult Remove(string team, string personaName) => this.Record(new MembershipCall("Remove", team, personaName));

    /// <summary>Records the archive-only removal, which skips the manager check (<c>ITeamMembership.RemoveForArchive</c>).</summary>
    /// <param name="team">The Team being archived.</param>
    /// <param name="personaName">The member losing the label.</param>
    /// <returns>The configured <see cref="Outcome"/>.</returns>
    public MembershipResult RemoveForArchive(string team, string personaName) => this.Record(new MembershipCall("RemoveForArchive", team, personaName));

    /// <inheritdoc />
    public MembershipResult ReplaceManager(string team, string newManager) => this.Record(new MembershipCall("ReplaceManager", team, newManager));

    private MembershipResult Record(MembershipCall call)
    {
        this.OnCall?.Invoke(call);
        this.calls.Add(call);
        return new MembershipResult(this.Outcome);
    }
}

/// <summary>One call made on a <see cref="RecordingTeamMembership"/>.</summary>
/// <param name="Method">The interface method's name.</param>
/// <param name="Team">The Team argument as passed.</param>
/// <param name="Name">The Persona name (or new manager) argument as passed.</param>
internal sealed record MembershipCall(string Method, string Team, string Name);
