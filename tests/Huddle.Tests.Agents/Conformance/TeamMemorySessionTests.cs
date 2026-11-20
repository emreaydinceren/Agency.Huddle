using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Conformance;

/// <summary>
/// Spec §6.4 and §12 T12-T13 (Task 5.6): whether <see cref="Agency.Huddle.App.Acp.DotAcpAgentHostFactory.StartAsync"/> builds the
/// Team Memory snapshot of the Teams a Persona belongs to and whether that block reaches the real composed
/// system prompt, read from <c>session/new</c> the same way <see cref="MemoryConformanceTests"/> reads the
/// personal Memory block. Every Team folder is created BEFORE the host is built, because a real Team catalog
/// only sees a folder created later after the 500 ms Task Store debounce.
/// </summary>
public sealed class TeamMemorySessionTests
{
    private const string TeamBlockHeading = "## Team Memory";

    private const string PersonalBlockMarker = "Your memory is the folder";

    /// <summary>A Persona in Team Business, whose Team-wide <c>memory/a.md</c> exists, is told the Team Memory block with that entry.</summary>
    [Fact]
    public async Task SessionStart_PersonaInATeam_ListsTheTeamMemory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string teamFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        TeamMemorySessionTests.WriteMemoryFile(teamFolder, "a.md", "Business fact alpha");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Business", ct: ct);

        string expected = TeamMemorySessionTests.ExpectedBlock(
            teamFolder,
            "Business (folder: Business):",
            "- a.md - Business fact alpha");
        // contains-ok: composed system prompt text; the whole Team Memory block is the expected value.
        Assert.Contains(expected, prompt, StringComparison.Ordinal);
    }

    /// <summary>A Project's <c>memory/b.md</c> is listed under its own <c>Team › Project</c> heading, after the Team-wide scope, which has nothing yet.</summary>
    [Fact]
    public async Task SessionStart_ProjectMemory_IsListedUnderItsProject()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string teamFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        string projectFolder = Path.Combine(teamFolder, "Marketing");
        TeamMemorySessionTests.WriteMemoryFile(projectFolder, "b.md", "Marketing fact beta");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Business", ct: ct);

        string expected = TeamMemorySessionTests.ExpectedBlock(
            teamFolder,
            "Business (folder: Business):",
            "Nothing yet.",
            "Business › Marketing (folder: Business/Marketing):",
            "- b.md - Marketing fact beta");
        // contains-ok: composed system prompt text; the whole Team Memory block is the expected value.
        Assert.Contains(expected, prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// With the resolved Adapter Profile's <c>ReadsFiles</c> false, the Team Memory and personal Memory blocks still reach the prompt, because
    /// the memory tool is offered whatever the Adapter can read (Instructions spec §6.1, U-10); only the sentence that names File Changes is left out.
    /// </summary>
    [Fact]
    public async Task SessionStart_AdapterThatCannotReadFiles_StillGetsTheTeamMemoryBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string teamFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        TeamMemorySessionTests.WriteMemoryFile(teamFolder, "a.md", "Business fact alpha");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Business", readsFiles: false, ct: ct);

        // contains-ok: composed system prompt text; the blocks are present and the File Changes sentence is not.
        Assert.Contains(TeamBlockHeading, prompt, StringComparison.Ordinal);
        Assert.Contains("- a.md - Business fact alpha", prompt, StringComparison.Ordinal);
        Assert.Contains(PersonalBlockMarker, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("the change is listed at the start of your next Turn", prompt, StringComparison.Ordinal);
    }

    /// <summary>A Persona that manages one of its two Teams reads the manager paragraph for that Team only, and the member paragraph for the other (Instructions spec §6.2).</summary>
    [Fact]
    public async Task SessionStart_PersonaManagingOneOfTwoTeams_SeesTheManagerParagraphOnlyForThatTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string business = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        TeamMemorySessionTests.TeamFolder(dataDir, "Ops");
        File.WriteAllText(Path.Combine(business, "_team.md"), "---\nteamType: 'development@1.0.0'\nmanager: 'nova'\n---\n");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Business, Ops", ct: ct);

        // contains-ok: composed system prompt text; the two paragraphs are the assertions.
        Assert.Contains("You look after the shared Memory of Business:", prompt, StringComparison.Ordinal);
        Assert.Contains("Ops keep shared Memory:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("You look after the shared Memory of Ops", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Business keep shared Memory", prompt, StringComparison.Ordinal);
        Assert.Equal(1, prompt.Split(TeamBlockHeading).Length - 1);
    }

    /// <summary>
    /// Build a team's real hire order (<c>CreateTeam</c> with no manager, <c>HireDrafts</c>, then the manager written to <c>_team.md</c>), with the
    /// manager's session starting straight after, no wait: its prompt must still carry the manager paragraph, not the member's (#348 Part 1).
    /// </summary>
    [Fact]
    public async Task SessionStart_ManagerHiredInBuildTeamOrder_ReadsTheManagerParagraph()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        const string text = "---\nName: Devon\nTitle: Lead\nAlias: devon\nTeams: [Dev Team]\n---\nYou are Devon.";
        Persona persona = new("devon", text);

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(
            persona,
            beforeRunnerStart: TeamMemorySessionTests.HireDevonAsManager,
            cancellationToken: ct);
        string prompt = await fixture.AppendedSystemPromptAsync(ct);

        // contains-ok: composed system prompt text; the two paragraphs are the assertions.
        Assert.Contains("You look after the shared Memory of Dev Team:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Dev Team keep shared Memory", prompt, StringComparison.Ordinal);
    }

    /// <summary>Does what <c>BuildTeam.ApproveCoreAsync</c> does to the stores, in its order: create the Team without a manager, hire, then write the manager.</summary>
    /// <param name="services">The started host's services.</param>
    private static void HireDevonAsManager(IServiceProvider services)
    {
        ITeamFolders folders = services.GetRequiredService<ITeamFolders>();
        HireService hiring = services.GetRequiredService<HireService>();
        TeamFile file = new(new VersionedRef("development", "1.0.0"), null, null, null, string.Empty);
        Assert.Equal(TeamCreateOutcome.Created, folders.CreateTeam("Dev Team", file).Outcome);

        DraftHire devon = new("Devon", "Lead", "You are Devon.", null, new Avatar(null, null, null), null, null);
        DraftHireResult result = hiring.HireDrafts("Dev Team", [devon]);
        Assert.Null(result.Error);
        Assert.True(folders.UpdateTeamFile("Dev Team", current => current with { Manager = "Devon" }));
    }

    /// <summary>
    /// A Persona in a Team is offered <c>propose_team_memory</c>, and one in no Team is not (Team Memory Helper spec §6.1): the
    /// session's tool list names it only for the member.
    /// </summary>
    [Fact]
    public async Task SessionStart_ProposeTool_IsOfferedOnlyToAPersonaInATeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir member = new();
        TeamMemorySessionTests.TeamFolder(member, "Business");
        using TempDataDir loner = new();
        TeamMemorySessionTests.TeamFolder(loner, "Business");

        string memberPrompt = await TeamMemorySessionTests.ComposedPromptAsync(member, teams: "Business", ct: ct);
        string lonerPrompt = await TeamMemorySessionTests.ComposedPromptAsync(loner, teams: null, ct: ct);

        // contains-ok: composed system prompt text; the tool list is the assertion.
        Assert.Contains("mcp__team__propose_team_memory", memberPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("propose_team_memory", lonerPrompt, StringComparison.Ordinal);
    }

    /// <summary>A Persona with no Teams line is told no Team Memory, though a Team with a Memory file exists. Guard: green before the change.</summary>
    [Fact]
    public async Task SessionStart_PersonaInNoTeam_HasNoBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string teamFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        TeamMemorySessionTests.WriteMemoryFile(teamFolder, "a.md", "Business fact alpha");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: null, ct: ct);

        // contains-ok: composed system prompt text; proves the prompt was read and carries the personal block.
        Assert.Contains(PersonalBlockMarker, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(TeamBlockHeading, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Business fact alpha", prompt, StringComparison.Ordinal);
    }

    /// <summary>A Persona whose Team has no folder is told no Team Memory block, though another Team's folder holds a file. Guard: green before the change.</summary>
    [Fact]
    public async Task SessionStart_TeamWithoutAFolder_HasNoBlock()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string otherFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Other");
        TeamMemorySessionTests.WriteMemoryFile(otherFolder, "a.md", "Other fact alpha");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Ghost", ct: ct);

        // contains-ok: composed system prompt text; proves the prompt was read and carries the personal block.
        Assert.Contains(PersonalBlockMarker, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(TeamBlockHeading, prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Other fact alpha", prompt, StringComparison.Ordinal);
    }

    /// <summary>With <c>Team:Teams:MaxMemoryEntries</c> 1 and two files, exactly one is listed and the closing more line counts the other.</summary>
    [Fact]
    public async Task SessionStart_HonoursMaxMemoryEntries()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        string teamFolder = TeamMemorySessionTests.TeamFolder(dataDir, "Business");
        TeamMemorySessionTests.WriteMemoryFile(teamFolder, "a.md", "Business fact alpha");
        TeamMemorySessionTests.WriteMemoryFile(teamFolder, "b.md", "Business fact bravo");

        string prompt = await TeamMemorySessionTests.ComposedPromptAsync(dataDir, teams: "Business", maxEntries: 1, ct: ct);

        // contains-ok: composed system prompt text; the closing line and the count of listed summaries are the assertions.
        Assert.Contains("…and 1 more in the Teams' memory folders.", prompt, StringComparison.Ordinal);
        string[] summaries = ["Business fact alpha", "Business fact bravo"];
        // contains-ok: composed system prompt text; counts how many of the two summaries were listed.
        int listed = summaries.Count(summary => prompt.Contains(summary, StringComparison.Ordinal));
        Assert.Equal(1, listed);
    }

    /// <summary>Creates <c>Teams/{team}</c> under the data directory and returns its full path.</summary>
    private static string TeamFolder(TempDataDir dataDir, string team)
    {
        string folder = Path.Combine(dataDir.Path, "Teams", team);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Writes <c>{scopeFolder}/memory/{fileName}</c> whose first line is <paramref name="firstLine"/>.</summary>
    private static void WriteMemoryFile(string scopeFolder, string fileName, string firstLine)
    {
        string memoryFolder = Path.Combine(scopeFolder, "memory");
        Directory.CreateDirectory(memoryFolder);
        File.WriteAllText(Path.Combine(memoryFolder, fileName), firstLine + "\n");
    }

    /// <summary>The Team Memory block the default prompts render for a member of Team <c>Business</c>, ending with the given index lines.</summary>
    /// <param name="teamFolder">The Team's folder, whose <c>memory</c> and <c>&lt;Project&gt;/memory</c> paths the two folder lines name.</param>
    /// <param name="indexLines">The index lines under the block's blank line, in order.</param>
    private static string ExpectedBlock(string teamFolder, params string[] indexLines)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        string[] head =
        [
            TeamBlockHeading,
            "Business keep shared Memory:",
            $"- Business, whole Team: {Path.Combine(teamFolder, "memory")}{separator}",
            $"- Business, one Project: {Path.Combine(teamFolder, "<Project>", "memory")}{separator}",
            "Read it with the mcp__team__memory tool, passing the Team or Project as the folder. You do not write it.",
            "When you learn something the whole Team should keep, propose it with mcp__team__propose_team_memory, one fact at a time; Huddle files it and records that you proposed it. Keep your own notes in your own Memory.",
            string.Empty,
        ];

        return string.Join('\n', [.. head, .. indexLines]);
    }

    /// <summary>
    /// Starts a real session for a Persona carrying <paramref name="teams"/> against <paramref name="dataDir"/>
    /// (its <c>Team:DataDir</c> replaces the fixture's own) and returns the appended system prompt of its <c>session/new</c>.
    /// </summary>
    /// <param name="dataDir">The data directory, with every Team folder already created.</param>
    /// <param name="teams">The Teams line's single label, or <see langword="null"/> for a Persona with no Teams line.</param>
    /// <param name="readsFiles">Whether the resolved Adapter Profile's <c>ReadsFiles</c> is true.</param>
    /// <param name="maxEntries">The <c>Team:Teams:MaxMemoryEntries</c> value, or <see langword="null"/> for the default.</param>
    /// <param name="ct">Cancels the wait.</param>
    private static async Task<string> ComposedPromptAsync(
        TempDataDir dataDir,
        string? teams,
        CancellationToken ct,
        bool readsFiles = true,
        int? maxEntries = null)
    {
        string teamsLine = teams is null ? string.Empty : $"\nTeams: [{teams}]";
        string text = $"---\nName: Nova\nTitle: Nova\nAlias: Nova{teamsLine}\n---\nYou are Nova.";
        Persona persona = readsFiles ? new("nova", text) : new("nova", text, Adapter: "agency");

        Dictionary<string, string?> config = new(StringComparer.Ordinal)
        {
            ["Team:DataDir"] = dataDir.Path,
        };

        if (!readsFiles)
        {
            config["Team:Acp:Adapters:0:Id"] = "agency";
            config["Team:Acp:Adapters:0:Command"] = "node";
            config["Team:Acp:Adapters:0:Args:0"] = "--mock-adapter-fixture";
            config["Team:Acp:Adapters:0:ReadsFiles"] = "false";
        }

        if (maxEntries is not null)
        {
            config["Team:Teams:MaxMemoryEntries"] = maxEntries.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await using MockAdapterFixture fixture = await MockAdapterFixture.StartAsync(persona, config, cancellationToken: ct);

        return await fixture.AppendedSystemPromptAsync(ct);
    }
}
