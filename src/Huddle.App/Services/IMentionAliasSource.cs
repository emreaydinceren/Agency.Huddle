namespace Agency.Huddle.App.Services;

/// <summary>
/// Supplies the Alias-to-Name pairs currently in force, so <see cref="MentionParser"/> and
/// <see cref="ChatService"/> can resolve a Persona's Alias - as a Mention, or as an <c>/invite</c>
/// target - without either one taking a dependency on <see cref="Agency.Huddle.App.Acp.PersonaStore"/>
/// directly. <see cref="MentionParser"/> is a pure static class with no store of its own by design, and
/// <see cref="ChatService"/> lives one layer below <c>Huddle.App.Acp</c>; this interface is the seam
/// that lets Aliases reach both without either becoming coupled to how a Persona is discovered or
/// stored.
/// </summary>
/// <remarks>
/// <see cref="Agency.Huddle.App.Acp.PersonaStore"/> is this interface's one real implementation, and it
/// must be registered against the SAME singleton instance already registered for
/// <see cref="Agency.Huddle.App.Acp.PersonaStore"/> itself (see
/// <see cref="Agency.Huddle.App.ServiceCollectionExtensions.AddTeamServices"/>, following the
/// <c>AgentGateway</c>/<c>IAgentGateway</c> pattern already there) - never as a second, independently
/// constructed instance. A second <see cref="Agency.Huddle.App.Acp.PersonaStore"/> means a second
/// <see cref="System.IO.FileSystemWatcher"/> watching the same Teams directory, which is exactly the
/// double-registration shape <c>PipeHostFixture.RemovePersonaSupervisorHostedService</c>'s remarks
/// document as the cause of a real, intermittent test flake, just for a different singleton.
/// </remarks>
public interface IMentionAliasSource
{
    /// <summary>Every Persona's Alias, paired with its owning Name, currently known - unfiltered by any Room's membership.</summary>
    IReadOnlyList<MentionAlias> Aliases { get; }
}
