using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;

namespace Agency.Huddle.Tests.Factories;

/// <summary>
/// Tests for the shared domain factories of Phase 2 (R5 rows 1-5): <see cref="TestOptions"/>,
/// <see cref="TestStores"/>, <see cref="ChatServiceFactory"/>, <see cref="TestPersonas"/> and the
/// <see cref="TaskToolHarness"/> extensions.
/// </summary>
public sealed class TestFactoriesTests
{
    /// <summary><see cref="TestOptions.For"/> points the options at the directory and applies the configure callback on top.</summary>
    [Fact]
    public void For_ConfigureSetsHumanName_SetsBothDataDirAndHumanName()
    {
        using TempDataDir dir = new();

        IOptions<TeamOptions> options = TestOptions.For(dir, o => o.HumanName = "X");

        Assert.Equal(dir.Path, options.Value.DataDir);
        Assert.Equal("X", options.Value.HumanName);
    }

    /// <summary><see cref="TestOptions.Create"/> leaves <see cref="TeamOptions.DataDir"/> at its default so a test of default paths stays valid.</summary>
    [Fact]
    public void Create_NoConfigure_KeepsDefaults()
    {
        IOptions<TeamOptions> options = TestOptions.Create();

        Assert.Equal(new TeamOptions().DataDir, options.Value.DataDir);
        Assert.Equal("You", options.Value.HumanName);
    }

    /// <summary><see cref="TempDataDir.Options(Action{TeamOptions}?)"/> applies the callback after setting the data directory.</summary>
    [Fact]
    public void Options_WithConfigure_AppliesCallbackAndKeepsDataDir()
    {
        using TempDataDir dir = new();

        IOptions<TeamOptions> options = dir.Options(o => o.HumanName = "Y");

        Assert.Equal(dir.Path, options.Value.DataDir);
        Assert.Equal("Y", options.Value.HumanName);
    }

    /// <summary><see cref="TestStores.CreateDirectoryAsync(TempDataDir, CancellationToken, string)"/> returns a directory that is already initialised with the human named "You".</summary>
    [Fact]
    public async Task CreateDirectoryAsync_Default_ReturnsInitialisedDirectoryWithHumanYou()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();

        SqliteTeamDirectory directory = await dir.CreateDirectoryAsync(ct);

        User human = await directory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
    }

    /// <summary>The synchronous directory, chat store and persona store factories build over the directory's data folder.</summary>
    [Fact]
    public void CreateStores_OverTempDir_ReturnInstances()
    {
        using TempDataDir dir = new();

        SqliteTeamDirectory directory = dir.CreateDirectory();
        FileChatStore chatStore = dir.CreateChatStore();
        using PersonaStore personas = dir.CreatePersonaStore();

        Assert.NotNull(directory);
        Assert.NotNull(chatStore);
        Assert.Empty(personas.ListNames());
    }

    /// <summary><see cref="ChatServiceFactory.Create"/> returns a stack whose Chat raises its Room events on the stack's own Events hub.</summary>
    [Fact]
    public async Task Create_PostMessage_RaisesMessagePostedOnStackEvents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        SqliteTeamDirectory directory = await dir.CreateDirectoryAsync(ct);
        ChatStack stack = ChatServiceFactory.Create(directory, dir.Options());
        User? alpha = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(alpha);
        User human = await directory.GetHumanAsync(ct);
        Room room = await stack.Chat.CreateRoomForAsync([alpha.Id], ct);
        List<MessagePostedEvent> seen = [];
        stack.Events.MessagePosted += seen.Add;

        await stack.Chat.PostAsync(room.Id, human.Id, "hello", ct: ct);

        MessagePostedEvent posted = Assert.Single(seen);
        Assert.Equal("hello", posted.Message.Text);
    }

    /// <summary><see cref="TestPersonas.Nova"/> is the lowercase "nova" Persona with the "You are Nova." body (OP-4).</summary>
    [Fact]
    public void Nova_IsLowercaseNovaWithBody()
    {
        Persona nova = TestPersonas.Nova;

        Assert.Equal("nova", nova.Name);
        Assert.Equal("You are Nova.", nova.Text);
    }

    /// <summary><see cref="TestPersonas.Named"/> keeps the casing it is given and defaults the body to "You are {name}.".</summary>
    [Fact]
    public void Named_NoBody_UsesGivenCasingAndDefaultBody()
    {
        Persona persona = TestPersonas.Named("Nova");

        Assert.Equal("Nova", persona.Name);
        Assert.Equal("You are Nova.", persona.Text);
    }

    /// <summary><see cref="TestPersonas.FrontmatterText"/> without alias or teams writes Name, Title and Alias all equal to the name.</summary>
    [Fact]
    public void FrontmatterText_NoAliasNoTeams_WritesMinimalFrontmatter()
    {
        string text = TestPersonas.FrontmatterText("kai", "body");

        Assert.Equal("---\nName: kai\nTitle: kai\nAlias: kai\n---\nbody", text);
    }

    /// <summary><see cref="TestPersonas.FrontmatterText"/> with alias, title, teams and adapter writes each field and parses back.</summary>
    [Fact]
    public void FrontmatterText_WithAliasAndTeams_WritesThemAndParsesBack()
    {
        string text = TestPersonas.FrontmatterText("kai", "body", title: "Engineer", alias: "k", teams: ["Platform", "Business"], adapter: "mock");

        bool read = PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out string error);

        Assert.True(read, error);
        Assert.NotNull(identity);
        Assert.Equal("kai", identity.Name);
        Assert.Equal("Engineer", identity.Title);
        Assert.Equal("k", identity.Alias);
        Assert.Equal(["Platform", "Business"], identity.Teams);
        Assert.Equal("mock", identity.Adapter);
    }

    /// <summary><see cref="TestPersonas.FrontmatterText"/> with no alias and no teams parses back with the name as alias and no teams.</summary>
    [Fact]
    public void FrontmatterText_NoAliasNoTeams_ParsesBackWithNameAsAliasAndNoTeams()
    {
        bool read = PersonaFrontmatter.TryReadIdentity(TestPersonas.FrontmatterText("kai"), out PersonaIdentity? identity, out string error);

        Assert.True(read, error);
        Assert.NotNull(identity);
        Assert.Equal("kai", identity.Alias);
        Assert.Empty(identity.Teams);
    }

    /// <summary><see cref="TestPersonas.Identity"/> defaults title and alias to the name and teams to none.</summary>
    [Fact]
    public void Identity_NameOnly_DefaultsTitleAliasAndTeams()
    {
        PersonaIdentity identity = TestPersonas.Identity("kai");

        Assert.Equal("kai", identity.Title);
        Assert.Equal("kai", identity.Alias);
        Assert.Empty(identity.Teams);
    }

    /// <summary><see cref="TaskToolHarnessExtensions.CreateTask"/> with no arguments saves a ToDo task titled "T" in the Platform team.</summary>
    [Fact]
    public void CreateTask_NoArguments_SavesToDoTaskTitledTInPlatform()
    {
        using TaskToolHarness harness = new();

        TaskItem task = harness.CreateTask();

        Assert.Equal("T", task.Title);
        Assert.Equal(TaskState.ToDo, task.Status);
        Assert.Equal("Platform", task.Location.Team);
    }

    /// <summary><see cref="TaskToolHarnessExtensions.NovaIdOrThrow"/> returns the seeded Nova id, and <see cref="TaskToolHarnessExtensions.HumanActor"/> returns the Human actor.</summary>
    [Fact]
    public async Task NovaIdOrThrowAndHumanActor_SeededHarness_ReturnSeededValues()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);

        string novaId = harness.NovaIdOrThrow();
        TaskActor actor = harness.HumanActor();

        Assert.Equal(harness.NovaId, novaId);
        Assert.Equal(TaskActors.Human(harness.Options.Value), actor);
    }

    /// <summary><see cref="TaskToolHarnessExtensions.NovaIdOrThrow"/> throws on a harness that was not seeded through CreateAsync.</summary>
    [Fact]
    public void NovaIdOrThrow_UnseededHarness_Throws()
    {
        using TaskToolHarness harness = new();

        Assert.Throws<InvalidOperationException>(harness.NovaIdOrThrow);
    }
}
