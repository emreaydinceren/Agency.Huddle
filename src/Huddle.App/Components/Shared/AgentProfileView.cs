using Agency.Huddle.App.Data;

namespace Agency.Huddle.App.Components.Shared;

/// <summary>An Agent's profile card data with what the popover needs around it.</summary>
/// <param name="Profile">What the card draws.</param>
/// <param name="Manager">Whether the Agent manages the Team it was seen in.</param>
/// <param name="Membership">Whether the Agent belongs to the Team it was seen in.</param>
/// <param name="Agent">The Agent's agent user, or <see langword="null"/> when it has never connected (so there is no Room to message it in).</param>
/// <param name="Team">The Team the avatar was seen in, or <see langword="null"/> for a root Room.</param>
internal sealed record AgentProfileView(AgentProfile Profile, bool Manager, ProfileMembership Membership, User? Agent, string? Team)
{
    /// <summary>The agent user's id, or <see langword="null"/> when it has never connected.</summary>
    public string? AgentId => this.Agent?.Id;

    /// <summary>Whether this is the Human's own profile, edited on the Settings Profile tab rather than on an Agent card.</summary>
    public bool IsHuman { get; init; }
}
