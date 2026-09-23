namespace Agency.Huddle.App.Acp;

/// <summary>
/// Starts one Persona's <see cref="IPersonaHost"/>: its Work Dir, bearer token, App Tools bound to
/// the given Agent id, App Tool server and Adapter process (RS §6.3). Opens no
/// session — that is <see cref="IPersonaHost.OpenAsync"/>'s job, called once per Room. This
/// interface exists purely as a test seam: it is what lets the entire pipe path be exercised
/// against a fake agent for zero tokens. The real implementation, against <c>DotAcpAgentHost</c>, is
/// built by a separate task.
/// </summary>
internal interface IAgentHostFactory
{
    /// <summary>Starts the host for <paramref name="persona"/>, with its tools bound to <paramref name="agentId"/>.</summary>
    /// <param name="persona">The Persona whose Adapter, Model, Effort and text this host launches.</param>
    /// <param name="agentId">The Agent id the App Tools this host builds are bound to.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken);
}