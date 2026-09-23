using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Acp;

/// <summary>
/// One Persona's running Adapter process, which opens and resumes that Persona's sessions
/// (RS §6.3). Everything per Persona — the Work Dir, the bearer token, the App Tools bound to the
/// Agent id, the App Tool server and the Adapter process itself — lives behind this interface and
/// is started once by <see cref="IAgentHostFactory.StartAsync"/>. Only opening or resuming one
/// Room's session is per call.
/// </summary>
internal interface IPersonaHost : IAsyncDisposable
{
    /// <summary>The resolved Adapter Profile this host was started against (finding P-8).</summary>
    AdapterProfile Profile { get; }

    /// <summary>Whether the Adapter advertised <c>sessionCapabilities.resume</c>.</summary>
    bool CanResume { get; }

    /// <summary>Opens a fresh session, composing its system prompt now so the memory index is current.</summary>
    Task<IAgentSession> OpenAsync(CancellationToken cancellationToken);

    /// <summary>Resumes a session by id, or returns <see langword="null"/> when the Adapter no longer has it.</summary>
    Task<IAgentSession?> ResumeAsync(string sessionId, CancellationToken cancellationToken);
}
