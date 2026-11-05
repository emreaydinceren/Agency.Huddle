using Agency.Huddle.App;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// A real <see cref="AgentsMdLedger"/> over a temp data folder, with a stand-in registered Helper and a dispatcher that has no
/// invoker, for tests that need a ledger as a collaborator and never run a judgement through it.
/// </summary>
public sealed class AgentsMdLedgerFixture : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly FiringTimeProvider clock = new();
    private readonly HelperDispatcher dispatcher;

    /// <summary>Wires the ledger.</summary>
    public AgentsMdLedgerFixture()
    {
        this.Options = this.dir.Options(team => team.Acp.Enabled = true);
        HelperSettingsResolver settings = new(new HelperSettingsStore(this.Options), this.Options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(this.Options), this.clock);
        StandInHelper helper = new();
        this.dispatcher = new HelperDispatcher([], settings, new HelperStatusStore(this.Options), this.Options, NullLogger<HelperDispatcher>.Instance, this.clock);
        this.Ledger = new AgentsMdLedger(() => this.dispatcher, () => helper, settings, budget, this.Options, NullLogger<AgentsMdLedger>.Instance, this.clock);
    }

    /// <summary>The options the ledger reads.</summary>
    internal IOptions<TeamOptions> Options { get; }

    /// <summary>The ledger.</summary>
    internal AgentsMdLedger Ledger { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        this.dispatcher.Dispose();
        this.dir.Dispose();
    }

    /// <summary>A registered Helper whose only job is to have an id and defaults.</summary>
    private sealed class StandInHelper : IHelperAgent
    {
        /// <inheritdoc />
        public HelperId Id { get; } = new("AgentsMdChange");

        /// <inheritdoc />
        public string DisplayName => "AGENTS.md Changes";

        /// <inheritdoc />
        public string Description => "A stand-in.";

        /// <inheritdoc />
        public IReadOnlyList<string> PromptKeys { get; } = [];

        /// <inheritdoc />
        public HelperDefaults Defaults { get; } = new(true, null, null, null, DailyCap: 2, WaitSeconds: 1);
    }
}
