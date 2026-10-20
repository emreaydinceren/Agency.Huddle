namespace Agency.Huddle.App.Tasks;

/// <summary>
/// Who can confirm a finished Request (ADR-0040): a member of the requesting Team whose role covers
/// <c>verify</c>. Shared by <c>confirm_request</c> and by the Feed's "only you can confirm it" rule.
/// </summary>
internal interface IRequestConfirmers
{
    /// <summary>Whether <paramref name="member"/> belongs to <paramref name="team"/> in a role whose coverage includes <c>verify</c>.</summary>
    /// <param name="team">The requesting Team's name.</param>
    /// <param name="member">The member's name.</param>
    /// <returns><see langword="true"/> for a verifier of that Team.</returns>
    bool IsVerifierOf(string team, string member);

    /// <summary>Whether any member of <paramref name="team"/> could call <c>confirm_request</c>.</summary>
    /// <param name="team">The requesting Team's name.</param>
    /// <returns><see langword="true"/> when at least one member is a verifier.</returns>
    bool TeamHasConfirmer(string team);
}
