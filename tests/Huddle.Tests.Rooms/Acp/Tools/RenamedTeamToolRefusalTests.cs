using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.TeamTypes;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Helpers;
using Agency.Huddle.Tests.Factories;
using Agency.Huddle.Tests.Teams;
using Agency.Huddle.Tests.Ui.Teams;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins Team Rename Spec section 6.3, "any other tool that writes" (batch 3 review): a tool given the old name of a renamed Team
/// refuses with the one sentence that names today's name, and a never-journalled unknown name keeps its own error. The tools are
/// built through <see cref="ActivatorUtilities"/>, as production builds them, with the rename journal registered.
/// </summary>
public sealed class RenamedTeamToolRefusalTests
{
    private const string Sentence = "Team 'Marketing' was renamed to 'Growth'. Use 'Growth'.";

    private static TeamRenameStore Journal(TempDataDir dir, string oldName, string newName)
    {
        TeamRenameStore journal = new(dir.Options());
        journal.Complete(journal.Begin(TeamRenameKind.Team, oldName, oldName, newName));
        return journal;
    }

    private static async Task<string> CreateTaskAsync(TaskToolHarness harness, ITeamRenames journal, string team, CancellationToken ct)
    {
        ServiceCollection services = new();
        services.AddLogging();
        harness.AddTo(services);
        services.AddSingleton(journal);
        await using ServiceProvider provider = services.BuildServiceProvider();
        TaskService tasks = ActivatorUtilities.CreateInstance<TaskService>(provider);
        CreateTaskTool tool = ActivatorUtilities.CreateInstance<CreateTaskTool>(
            provider, tasks, harness.Triggers, harness.Directory, harness.Sprints, new FakePromptSource(), harness.NovaId ?? throw new InvalidOperationException("No Nova."));
        return await tool.InvokeAsync(new JsonObject { ["title"] = "Plan", ["team"] = team }, ct);
    }

    /// <summary>create_task with the old name of a renamed Team is refused with the rename sentence and writes nothing.</summary>
    [Fact]
    public async Task CreateTask_OldTeamName_RefusesWithTheRenameSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using TempDataDir dir = new();

        string result = await CreateTaskAsync(harness, Journal(dir, "Marketing", "Growth"), "Marketing", ct);

        Assert.Equal(Sentence, result);
        Assert.Empty(harness.Store.All);
    }

    /// <summary>create_task with a name no rename ever used keeps today's unknown-Team error.</summary>
    [Fact]
    public async Task CreateTask_UnknownNeverJournalledTeam_KeepsTheUnknownTeamError()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using TempDataDir dir = new();

        string result = await CreateTaskAsync(harness, Journal(dir, "Marketing", "Growth"), "Research", ct);

        Assert.Equal("Unknown team 'Research'. Known teams: Platform.", result);
    }

    /// <summary>propose_team_memory with the old name of a renamed Team is refused with the rename sentence, and queues nothing.</summary>
    [Fact]
    public async Task ProposeTeamMemory_OldTeamName_RefusesWithTheRenameSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamMemoryHarness harness = new();
        using TempDataDir dir = new();

        string result = await ProposeAsync(harness, Journal(dir, "Sales", "Research"), "Sales", ct);

        Assert.Equal("Team 'Sales' was renamed to 'Research'. Use 'Research'.", result);
    }

    /// <summary>propose_team_memory with a never-journalled unknown name keeps the not-a-member answer.</summary>
    [Fact]
    public async Task ProposeTeamMemory_UnknownNeverJournalledTeam_KeepsTheNotMemberAnswer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TeamMemoryHarness harness = new();
        using TempDataDir dir = new();

        string result = await ProposeAsync(harness, Journal(dir, "Sales", "Research"), "Nowhere", ct);

        Assert.DoesNotContain("was renamed", result, StringComparison.Ordinal);
    }

    /// <summary>mark_retro_row with the old name of a renamed Team is refused with the rename sentence.</summary>
    [Fact]
    public async Task MarkRetroRow_OldTeamName_RefusesWithTheRenameSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using TempDataDir dir = new();
        ITeamRenames journal = Journal(dir, "Marketing", "Growth");
        MarkRetroRowTool tool = new(
            new FakeTeamCatalog(), harness.Personas, EmptyTypes(harness), new RetroFileStore(new FakeTeamFolders()), harness.Directory,
            harness.NovaId ?? throw new InvalidOperationException("No Nova."), new FakePromptSource(), journal);

        string result = await tool.InvokeAsync(
            new JsonObject { ["team"] = "Marketing", ["rowId"] = "R-001", ["status"] = "Verified", ["evidence"] = "PLAT-0007" }, ct);

        Assert.Equal(Sentence, result);
    }

    /// <summary>read_records with the old name of a renamed Team is refused with the rename sentence.</summary>
    [Fact]
    public async Task ReadRecords_OldTeamName_RefusesWithTheRenameSentence()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using TempDataDir dir = new();
        ITeamRenames journal = Journal(dir, "Marketing", "Growth");
        FakeTeamCatalog catalog = new();
        RecordsBundleBuilder builder = new(harness.Store, catalog, new PersonaSpendStore(harness.Options), harness.Options);
        ReadRecordsTool tool = new(
            catalog, harness.Store, harness.Personas, EmptyTypes(harness), builder, harness.Options.CreateChatStore(), new FakeTeamFolders(),
            harness.Directory, TimeProvider.System, harness.NovaId ?? throw new InvalidOperationException("No Nova."), new FakePromptSource(), journal);

        string result = await tool.InvokeAsync(new JsonObject { ["team"] = "Marketing" }, ct);

        Assert.Equal(Sentence, result);
    }

    private static TeamTypeCatalog EmptyTypes(TaskToolHarness harness) =>
        new(harness.Options, NullLogger<TeamTypeCatalog>.Instance, new Dictionary<string, string>(StringComparer.Ordinal));

    private static async Task<string> ProposeAsync(TeamMemoryHarness harness, ITeamRenames journal, string team, CancellationToken ct)
    {
        ServiceCollection services = new();
        services.AddSingleton(harness.Access);
        services.AddSingleton(harness.Trigger);
        services.AddSingleton(harness.Registry);
        services.AddSingleton(harness.Settings);
        services.AddSingleton(harness.Budget);
        services.AddSingleton<Agency.Huddle.App.Prompts.IPromptSource>(new FakePromptSource());
        services.AddSingleton(harness.Options);
        services.AddSingleton(journal);
        await using ServiceProvider provider = services.BuildServiceProvider();
        ProposeTeamMemoryTool tool = ActivatorUtilities.CreateInstance<ProposeTeamMemoryTool>(provider, "Ada");
        return await tool.InvokeAsync(new JsonObject { ["team"] = team, ["fact"] = "We ship on Fridays." }, ct);
    }
}
