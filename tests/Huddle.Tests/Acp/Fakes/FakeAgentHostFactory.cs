using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// A test double for <see cref="IAgentHostFactory"/>. Every <see cref="StartAsync"/> call returns a
/// fresh <see cref="FakePersonaHost"/> (recorded as <see cref="Host"/>), whose first
/// <see cref="FakePersonaHost.OpenAsync"/> returns the one fixed <see cref="Session"/> for the whole
/// test - so a test can reach into <see cref="Session"/> to queue turn plans and inspect recorded
/// prompts without launching a real agent process, exactly as before RS §6.3 split the factory.
/// </summary>
internal sealed class FakeAgentHostFactory : IAgentHostFactory
{
    private Exception? failure;

    /// <summary>Initializes a new instance of the <see cref="FakeAgentHostFactory"/> class.</summary>
    /// <param name="time">The clock the shared <see cref="Session"/>'s planned delays wait on; defaults to <see cref="TimeProvider.System"/>.</param>
    public FakeAgentHostFactory(TimeProvider? time = null)
    {
        this.Session = new FakeAgentSession(completeEventsOnDispose: false, time);
    }

    /// <summary>
    /// The session every host's first <see cref="IPersonaHost.OpenAsync"/> call returns (RS §6.3
    /// "first-open rule"). Built with <c>completeEventsOnDispose: false</c> (D19 correction 14): a
    /// supervisor restart disposes one host and starts another, and the new host's first open must
    /// still hand back a session whose <see cref="FakeAgentSession.Events"/> reader has not already
    /// completed.
    /// </summary>
    public FakeAgentSession Session { get; }

    /// <summary>The last <see cref="FakePersonaHost"/> <see cref="StartAsync"/> returned.</summary>
    public FakePersonaHost? Host { get; private set; }

    /// <summary>
    /// The <see cref="AdapterProfile.SessionPerRoom"/> value <see cref="StartAsync"/> gives the host
    /// it creates. Set before a test's runner is started - <see cref="Host"/> does not exist until
    /// then, so there is nothing to set the profile on beforehand except through this factory-level
    /// switch. Defaults false, matching the profile's own default (finding P-9).
    /// </summary>
    public bool SessionPerRoom { get; set; }

    /// <summary>
    /// Runs, if set, right after <see cref="StartAsync"/> creates its <see cref="FakePersonaHost"/>
    /// and before returning it - the only point at which a test can script that host (for example
    /// <see cref="FakePersonaHost.FailNextOpenWith"/>) before the runner's own first open call
    /// reaches it, since <see cref="Host"/> does not exist beforehand.
    /// </summary>
    public Action<FakePersonaHost>? OnHostStarted { get; set; }

    public List<(Persona Persona, string AgentId)> Calls { get; } = [];

    /// <summary>
    /// Configures every future <see cref="StartAsync"/> call to fail with <paramref name="exception"/>
    /// instead of returning a host - for a test proving what happens when the host itself fails to
    /// start, before any Turn could ever be queued against it.
    /// </summary>
    /// <param name="exception">The exception <see cref="StartAsync"/> throws.</param>
    public void FailNextCreateWith(Exception exception)
    {
        this.failure = exception;
    }

    public Task<IPersonaHost> StartAsync(Persona persona, string agentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        this.Calls.Add((persona, agentId));

        if (this.failure is not null)
        {
            throw this.failure;
        }

        // SessionPerRoom comes from this.SessionPerRoom, false by default (finding P-9's "absent
        // means false" for the fakes, never the record's own default), so the many
        // FakePersonaServer-backed runner tests that never touch this switch, several of which run
        // two Rooms over this one fake session, are never moved by D28's later default flip. A D23
        // test sets it true before starting its runner to exercise per-Room mode.
        FakePersonaHost host = new(
            this.Session,
            new AdapterProfile(
                Id: "claude",
                DisplayName: "Claude",
                Description: null,
                Command: "claude-agent-acp",
                Args: null,
                AdapterPath: null,
                UsesToolNamePrefix: true,
                EnvironmentOverrides: null,
                ReadsFiles: true,
                IsolateUserSettings: true,
                SessionPerRoom: this.SessionPerRoom));

        this.Host = host;
        this.OnHostStarted?.Invoke(host);
        return Task.FromResult<IPersonaHost>(host);
    }
}
