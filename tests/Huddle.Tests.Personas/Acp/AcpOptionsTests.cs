using Microsoft.Extensions.Configuration;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Pins <see cref="AcpOptions"/>' Room Sessions keys (RS §6.14) and their defaults.</summary>
public sealed class AcpOptionsTests
{
    /// <summary><see cref="AcpOptions.SessionIdleMinutes"/> defaults to 30.</summary>
    [Fact]
    public void SessionIdleMinutes_Default_Is30()
    {
        var options = new AcpOptions();

        Assert.Equal(30, options.SessionIdleMinutes);
    }

    /// <summary><see cref="AcpOptions.MaxLiveSessions"/> defaults to 3.</summary>
    [Fact]
    public void MaxLiveSessions_Default_Is3()
    {
        var options = new AcpOptions();

        Assert.Equal(3, options.MaxLiveSessions);
    }

    /// <summary><see cref="AcpOptions.MaxConcurrentTurns"/> defaults to 1 — today's serial behaviour.</summary>
    [Fact]
    public void MaxConcurrentTurns_Default_Is1()
    {
        var options = new AcpOptions();

        Assert.Equal(1, options.MaxConcurrentTurns);
    }

    /// <summary><see cref="AcpOptions.TranscriptCatchUpMessages"/> defaults to 20.</summary>
    [Fact]
    public void TranscriptCatchUpMessages_Default_Is20()
    {
        var options = new AcpOptions();

        Assert.Equal(20, options.TranscriptCatchUpMessages);
    }

    /// <summary>
    /// <see cref="AcpOptions.TranscriptCatchUpTokens"/> defaults to 100000 (LR-R7): the catch-up budget is
    /// measured in tokens, not messages, so a resumed Room session reads a bounded transcript tail.
    /// </summary>
    [Fact]
    public void TranscriptCatchUpTokens_Default_Is100000()
    {
        var options = new AcpOptions();

        Assert.Equal(100_000, options.TranscriptCatchUpTokens);
    }

    /// <summary>
    /// <see cref="AcpOptions.CatchUpKeepOwnMessages"/> defaults to 5 (LR-R11): the Persona's own most recent
    /// messages are kept in the catch-up even when older turns are trimmed to fit the token budget.
    /// </summary>
    [Fact]
    public void CatchUpKeepOwnMessages_Default_Is5()
    {
        var options = new AcpOptions();

        Assert.Equal(5, options.CatchUpKeepOwnMessages);
    }

    /// <summary>
    /// <see cref="AcpOptions.AdvertiseElicitation"/> ships on: Claude's built-in <c>AskUserQuestion</c>, the
    /// refusal-fallback dialog and MCP forms reach the Human unless an operator turns it off (elicitation
    /// bridge E-1b, the last step of its delivery).
    /// </summary>
    [Fact]
    public void AcpOptions_AdvertiseElicitation_DefaultsToTrue()
    {
        var options = new AcpOptions();

        Assert.True(options.AdvertiseElicitation);
    }

    /// <summary>
    /// With no <c>Team:Acp</c> hire-limit keys configured, <see cref="AcpOptions.MaxAgentsPerTeam"/>
    /// is 8 and <see cref="AcpOptions.MaxLiveProcesses"/> is 16 (HR-10, decision D1).
    /// </summary>
    [Fact]
    public void HireLimits_WhenAbsent_DefaultTo8And16()
    {
        TeamOptions teamOptions = BindTeamOptions(new Dictionary<string, string?>());

        Assert.Equal(8, teamOptions.Acp.MaxAgentsPerTeam);
        Assert.Equal(16, teamOptions.Acp.MaxLiveProcesses);
    }

    /// <summary>
    /// <c>Team:Acp:MaxAgentsPerTeam</c> and <c>Team:Acp:MaxLiveProcesses</c> bind through
    /// <c>ConfigurationBinder</c> onto <see cref="AcpOptions"/> as written (HR-10, decision D1).
    /// </summary>
    [Fact]
    public void HireLimits_BoundFromConfiguration_ReadsConfiguredValues()
    {
        TeamOptions teamOptions = BindTeamOptions(new Dictionary<string, string?>
        {
            ["Team:Acp:MaxAgentsPerTeam"] = "3",
            ["Team:Acp:MaxLiveProcesses"] = "5",
        });

        Assert.Equal(3, teamOptions.Acp.MaxAgentsPerTeam);
        Assert.Equal(5, teamOptions.Acp.MaxLiveProcesses);
    }

    /// <summary>
    /// Binds the given in-memory values onto a fresh <see cref="TeamOptions"/> the way the host does, under
    /// the <c>Team</c> section.
    /// </summary>
    /// <param name="values">The configuration keys and values to bind.</param>
    /// <returns>The bound options.</returns>
    private static TeamOptions BindTeamOptions(IReadOnlyDictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        TeamOptions teamOptions = new();
        configuration.GetSection(TeamOptions.SectionName).Bind(teamOptions);
        return teamOptions;
    }
}
