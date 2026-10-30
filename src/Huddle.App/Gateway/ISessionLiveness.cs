namespace Agency.Huddle.App.Gateway;

/// <summary>
/// What the connection guard asks on an open socket (C-5). The real resolver implements it over <see cref="SessionRegistry"/>; a resolver that
/// does not (the hosted test seam, whose fixed session is in no registry) leaves the socket alone.
/// </summary>
internal interface ISessionLiveness
{
    /// <summary>Whether the session is still live: not expired, and its device not revoked, removed or rotated.</summary>
    /// <param name="sessionHash">The <see cref="GatewayClaims.Session"/> claim.</param>
    bool IsLive(string sessionHash);
}
