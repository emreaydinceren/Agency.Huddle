using System.Text.Json.Nodes;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Jobs;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Teams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>Pins who may schedule what through the <c>jobs</c> tool, and what it answers.</summary>
public sealed class JobsToolTests : IDisposable
{
    private readonly TempDataDir dir = new();
    private readonly IOptions<TeamOptions> options;
    private readonly PersonaStore personas;
    private readonly JobStore store;
    private readonly JobScheduler scheduler;
    private readonly NullRunner runner = new();
    private readonly FakeTeamCatalog catalog = new();

    /// <summary>Seeds Nova and Orion, and Cleo as the Chief of Staff.</summary>
    public JobsToolTests()
    {
        this.options = this.dir.Options();
        AgentPaths paths = new(this.options);
        TestPersonaFiles.Write(paths, "Nova", PersonaSupervisorTestSupport.PersonaText("Nova", "You are Nova."));
        TestPersonaFiles.Write(paths, "Orion", PersonaSupervisorTestSupport.PersonaText("Orion", "You are Orion."));
        TestPersonaFiles.Write(paths, "Cleo", "---\nName: Cleo\nTitle: Chief of Staff\nAlias: cos\n_builtin: chief-of-staff\n---\nYou are Cleo.");
        foreach (string name in new[] { "Pia", "Quinn", "Rex" })
        {
            TestPersonaFiles.Write(paths, name, PersonaSupervisorTestSupport.PersonaText(name, $"You are {name}."));
        }

        this.catalog.Teams =
        [
            new TeamSummary("Marketing", [], ["Pia", "Nova", "Quinn"], true) { Manager = "Pia" },
            new TeamSummary("Sales", [], ["Rex", "Orion"], true) { Manager = "Rex" },
        ];
        this.personas = this.options.CreatePersonaStore();
        this.store = new JobStore(this.personas, this.options, NullLogger<JobStore>.Instance);
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        this.scheduler = new JobScheduler(this.store, new JobStateStore(this.options), this.runner, this.options, NullLogger<JobScheduler>.Instance, new Agency.Huddle.Tests.Jobs.NoJobNotice(), clock);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.scheduler.Dispose();
        this.store.Dispose();
        this.personas.Dispose();
        this.dir.Dispose();
    }

    /// <summary>An Agent schedules a Job for itself, and the answer says when it runs next.</summary>
    [Fact]
    public async Task Create_ForSelf_SchedulesItAndSaysWhenItRuns()
    {
        string answer = await this.Tool("Nova").InvokeAsync(Create(), TestContext.Current.CancellationToken);

        Job job = Assert.Single(this.store.Jobs);
        Assert.Equal((JobOwner.Agent("Nova"), "Nova"), (job.Owner, job.CreatedBy));
        Assert.Equal($"Scheduled {job.Id} \"Digest\" for Nova: daily at 09:00. It runs next on Mon 2 Nov 2026 at 09:00 (UTC).", answer);
    }

    /// <summary>A Job created through the tool has its state row at once, so a one-off due inside the minute is not lost.</summary>
    [Fact]
    public async Task Create_SeedsTheJobStateAtOnce()
    {
        _ = await this.Tool("Nova").InvokeAsync(Create(), TestContext.Current.CancellationToken);

        Job job = Assert.Single(this.store.Jobs);
        JobState? state = new JobStateStore(this.options).Get(job.Id);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero), state?.NextRunUtc);
    }

    /// <summary>An Agent that is not the Chief of Staff may not schedule for another Agent or for the Human.</summary>
    /// <param name="target">The other target.</param>
    [Theory]
    [InlineData("Orion")]
    [InlineData("You")]
    public async Task Create_ForSomeoneElse_ByAnAgent_IsRefused(string target)
    {
        string answer = await this.Tool("Nova").InvokeAsync(Create(target), TestContext.Current.CancellationToken);

        Assert.Equal(JobsTool.Refusal, answer);
        Assert.Empty(this.store.Jobs);
    }

    /// <summary>The Chief of Staff may schedule for another Agent, by Alias too, and for the Human by Name.</summary>
    [Fact]
    public async Task Create_ByTheChiefOfStaff_ForOthers_IsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Cleo");

        _ = await tool.InvokeAsync(Create("Orion"), ct);
        _ = await tool.InvokeAsync(Create("You"), ct);

        Assert.Equal([JobOwner.Agent("Orion"), JobOwner.Human], this.store.Jobs.Select(job => job.Owner).OrderBy(owner => owner.IsHuman ? 1 : 0));
        Assert.All(this.store.Jobs, job => Assert.Equal("Cleo", job.CreatedBy));
    }

    /// <summary>An Agent lists only its own Jobs; the Chief of Staff lists every Job.</summary>
    [Fact]
    public async Task List_AnAgentSeesItsOwn_TheChiefOfStaffSeesAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.Tool("Nova").InvokeAsync(Create(), ct);
        _ = await this.Tool("Orion").InvokeAsync(Create(), ct);

        JsonArray novas = Assert.IsType<JsonArray>(JsonNode.Parse(await this.Tool("Nova").InvokeAsync(Args("list"), ct)));
        JsonArray all = Assert.IsType<JsonArray>(JsonNode.Parse(await this.Tool("Cleo").InvokeAsync(Args("list"), ct)));

        Assert.Equal("Nova", Assert.Single(novas)?["target"]?.GetValue<string>());
        Assert.Equal(2, all.Count);
    }

    /// <summary>A manager schedules for a member of its own Team, and the Job records the manager as its creator.</summary>
    [Fact]
    public async Task Create_ByAManager_ForItsMember_IsAllowed()
    {
        string answer = await this.Tool("Pia").InvokeAsync(Create("Nova"), TestContext.Current.CancellationToken);

        Job job = Assert.Single(this.store.Jobs);
        Assert.Equal((JobOwner.Agent("Nova"), "Pia"), (job.Owner, job.CreatedBy));
        Assert.Equal($"Scheduled {job.Id} \"Digest\" for Nova: daily at 09:00. It runs next on Mon 2 Nov 2026 at 09:00 (UTC).", answer);
    }

    /// <summary>A manager is refused for an Agent of another Team and for the Human; those stay with the Chief of Staff.</summary>
    /// <param name="target">The target outside the manager's Team.</param>
    [Theory]
    [InlineData("Orion")]
    [InlineData("You")]
    public async Task Create_ByAManager_ForOutsideItsTeam_IsRefused(string target)
    {
        string answer = await this.Tool("Pia").InvokeAsync(Create(target), TestContext.Current.CancellationToken);

        Assert.Equal(JobsTool.Refusal, answer);
        Assert.Empty(this.store.Jobs);
    }

    /// <summary>A member who is not the manager is refused for a colleague in the same Team.</summary>
    [Fact]
    public async Task Create_ByAMember_ForAColleague_IsRefused()
    {
        string answer = await this.Tool("Nova").InvokeAsync(Create("Quinn"), TestContext.Current.CancellationToken);

        Assert.Equal(JobsTool.Refusal, answer);
        Assert.Empty(this.store.Jobs);
    }

    /// <summary>A manager lists a member's Job beside its own and pauses it; another Team's manager sees and may do neither.</summary>
    [Fact]
    public async Task ManagerListsAndPauses_AMembersJob()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.Tool("Nova").InvokeAsync(Create(), ct);
        _ = await this.Tool("Orion").InvokeAsync(Create(), ct);
        string novasId = this.store.Jobs.Single(job => job.Owner.IsAgent("Nova")).Id;

        JsonArray seen = Assert.IsType<JsonArray>(JsonNode.Parse(await this.Tool("Pia").InvokeAsync(Args("list"), ct)));
        string paused = await this.Tool("Pia").InvokeAsync(Args("pause", ("id", novasId)), ct);
        string refused = await this.Tool("Rex").InvokeAsync(Args("pause", ("id", novasId)), ct);

        Assert.Equal("Nova", Assert.Single(seen)?["target"]?.GetValue<string>());
        Assert.Equal($"Paused {novasId} \"Digest\"; it will not run until it is resumed.", paused);
        Assert.Equal(JobsTool.Refusal, refused);
        Assert.True(this.store.Jobs.Single(job => job.Id == novasId).Paused);
    }

    /// <summary>The Chief of Staff still schedules for a member of any Team, whether or not it manages that Team.</summary>
    [Fact]
    public async Task Create_ByTheChiefOfStaff_ForATeamMember_StillWorks()
    {
        _ = await this.Tool("Cleo").InvokeAsync(Create("Quinn"), TestContext.Current.CancellationToken);

        Assert.Equal(JobOwner.Agent("Quinn"), Assert.Single(this.store.Jobs).Owner);
    }

    /// <summary>The <c>when</c> field lists the monthly form, taken from the same grammar the parser quotes.</summary>
    [Fact]
    public void InputSchema_WhenField_ListsTheMonthlyForm()
    {
        string? when = this.Tool("Nova").InputSchema["properties"]?["when"]?["description"]?.GetValue<string>();

        Assert.Equal(
            "One of 'once on 2026-11-05 at 14:30', 'daily at 09:00', 'weekdays at 09:00', 'weekly on mon,thu at 09:00', 'monthly on 15 at 09:00' or 'every 4 hours'.",
            when);
    }

    /// <summary>An Agent may not pause, run or delete another Agent's Job.</summary>
    /// <param name="action">The action.</param>
    [Theory]
    [InlineData("pause")]
    [InlineData("run_now")]
    [InlineData("delete")]
    public async Task ActOnAnothersJob_IsRefused(string action)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        _ = await this.Tool("Orion").InvokeAsync(Create(), ct);
        string id = Assert.Single(this.store.Jobs).Id;

        Assert.Equal(JobsTool.Refusal, await this.Tool("Nova").InvokeAsync(Args(action, ("id", id)), ct));
        Assert.False(Assert.Single(this.store.Jobs).Paused);
    }

    /// <summary>Pause and resume flip the Job and say so.</summary>
    [Fact]
    public async Task PauseThenResume_FlipsTheJob()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Nova");
        _ = await tool.InvokeAsync(Create(), ct);
        string id = Assert.Single(this.store.Jobs).Id;

        Assert.Equal($"Paused {id} \"Digest\"; it will not run until it is resumed.", await tool.InvokeAsync(Args("pause", ("id", id)), ct));
        Assert.True(this.store.Jobs[0].Paused);
        Assert.Equal($"Resumed {id} \"Digest\". It runs next on Mon 2 Nov 2026 at 09:00 (UTC).", await tool.InvokeAsync(Args("resume", ("id", id)), ct));
    }

    /// <summary>Update changes only the fields given.</summary>
    [Fact]
    public async Task Update_ChangesOnlyTheGivenFields()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Nova");
        _ = await tool.InvokeAsync(Create(), ct);
        string id = Assert.Single(this.store.Jobs).Id;

        string answer = await tool.InvokeAsync(Args("update", ("id", id), ("when", "weekdays at 10:30")), ct);

        Assert.Equal($"Updated {id} \"Digest\" for Nova: weekdays at 10:30. It runs next on Mon 2 Nov 2026 at 10:30 (UTC).", answer);
        Assert.Equal("Summarise yesterday.", this.store.Jobs[0].Instruction);
    }

    /// <summary>Bad input is answered with what to change, and nothing is stored.</summary>
    [Fact]
    public async Task BadInput_IsAnsweredWithTheFix()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Nova");

        Assert.Equal("'25:00' is not a time of day; write it as HH:mm, for example 09:00.", await tool.InvokeAsync(Args("create", ("title", "T"), ("instruction", "I"), ("when", "daily at 25:00")), ct));
        Assert.Equal("A new Job needs a 'title', an 'instruction' and a 'when'.", await tool.InvokeAsync(Args("create", ("when", "daily at 09:00")), ct));
        Assert.Equal("No Agent and not the Human is called 'Ghost'. Call list_agents to see the Agents.", await tool.InvokeAsync(Create("Ghost"), ct));
        Assert.Equal("No Job has the id 'job-x'. Call jobs with action 'list' to see yours.", await tool.InvokeAsync(Args("pause", ("id", "job-x")), ct));
        Assert.Equal("'snooze' is not an action; use create, list, update, pause, resume, delete, run_now.", await tool.InvokeAsync(Args("snooze"), ct));
        Assert.Empty(this.store.Jobs);
    }

    /// <summary>Run now says what the run did, in the tool's own words for each outcome.</summary>
    /// <param name="outcome">What the run did, by name.</param>
    /// <param name="expected">The answer, with <c>{id}</c> for the Job's id.</param>
    [Theory]
    [InlineData("Delivered", "Ran {id} \"Digest\": its instruction is posted in its Room.")]
    [InlineData("BudgetSpent", "{id} did not run: its Room's Budget is spent until the Human continues it.")]
    [InlineData("NoAgent", "{id} did not run: Nova is not available.")]
    [InlineData("Failed", "{id} did not run.")]
    public async Task RunNow_SaysWhatTheRunDid(string outcome, string expected)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Nova");
        _ = await tool.InvokeAsync(Create(), ct);
        string id = Assert.Single(this.store.Jobs).Id;
        ArgumentNullException.ThrowIfNull(expected);
        this.runner.Outcome = Enum.Parse<JobOutcome>(outcome);

        Assert.Equal(expected.Replace("{id}", id, StringComparison.Ordinal), await tool.InvokeAsync(Args("run_now", ("id", id)), ct));
    }

    /// <summary>A paused Job's answer says it is paused, and a one-off in the past says it will not run.</summary>
    [Fact]
    public async Task NextRunLine_SaysPausedOrPassed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        JobsTool tool = this.Tool("Nova");
        _ = await tool.InvokeAsync(Create(), ct);
        string id = Assert.Single(this.store.Jobs).Id;
        _ = await tool.InvokeAsync(Args("pause", ("id", id)), ct);

        Assert.Equal($"Updated {id} \"Digest\" for Nova: daily at 09:00. It is paused.", await tool.InvokeAsync(Args("update", ("id", id)), ct));

        _ = await tool.InvokeAsync(Args("resume", ("id", id)), ct);
        string passed = await tool.InvokeAsync(Args("update", ("id", id), ("when", "once on 2026-11-01 at 09:00")), ct);

        Assert.Equal($"Updated {id} \"Digest\" for Nova: once on 2026-11-01 at 09:00. Its time has passed, so it will not run.", passed);
    }

    /// <summary>The refusal and the unknown-zone answer, word for word.</summary>
    [Fact]
    public async Task RefusalAndUnknownZone_AreWordedExactly()
    {
        Assert.Equal("You can schedule Jobs for yourself and for members of a Team you manage. Ask the Chief of Staff to schedule one for anyone else or for the Human.", JobsTool.Refusal);
        JsonObject arguments = Create();
        arguments["timezone"] = "Mars/Olympus";

        Assert.Equal(
            "'Mars/Olympus' is not a time zone this machine knows; use an IANA name such as Europe/Berlin.",
            await this.Tool("Nova").InvokeAsync(arguments, TestContext.Current.CancellationToken));
    }

    private static JsonObject Create(string? target = null)
    {
        JsonObject arguments = Args("create", ("title", "Digest"), ("instruction", "Summarise yesterday."), ("when", "daily at 09:00"), ("timezone", "UTC"));
        if (target is not null)
        {
            arguments["target"] = target;
        }

        return arguments;
    }

    private static JsonObject Args(string action, params (string Key, string Value)[] fields)
    {
        JsonObject arguments = new() { ["action"] = action };
        foreach ((string key, string value) in fields)
        {
            arguments[key] = value;
        }

        return arguments;
    }

    /// <summary>Builds the tool the way production does, through <see cref="ActivatorUtilities"/>, so the constructor's services come from the container.</summary>
    /// <param name="persona">The calling Persona's Name.</param>
    private JobsTool Tool(string persona)
    {
        ServiceCollection services = new();
        _ = services.AddSingleton(this.store).AddSingleton(this.scheduler).AddSingleton(this.personas);
        _ = services.AddSingleton<IHumanIdentity>(new ConfiguredHumanIdentity("You")).AddSingleton<IPromptSource>(new FakePromptSource());
        _ = services.AddSingleton<ITeamCatalog>(this.catalog);
        using ServiceProvider provider = services.BuildServiceProvider();
        return ActivatorUtilities.CreateInstance<JobsTool>(provider, persona);
    }

    /// <summary>A runner that posts nothing and answers with <see cref="Outcome"/>.</summary>
    private sealed class NullRunner : IJobRunner
    {
        public JobOutcome Outcome { get; set; } = JobOutcome.Delivered;

        public Task<JobOutcome> RunAsync(Job job, DateTimeOffset ranAt, DateTimeOffset? lateDue, CancellationToken ct) => Task.FromResult(this.Outcome);
    }
}
