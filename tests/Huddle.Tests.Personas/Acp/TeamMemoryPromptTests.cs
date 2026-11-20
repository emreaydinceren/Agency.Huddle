namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Task 5.5.t (Spec §6.4 Rendering and Implementation notes, corrections D5 items 10-15): the Team Memory
/// block the composer adds after the personal Memory block. Every block assertion compares the WHOLE
/// block, from <c>## Team Memory</c> up to the closing part that follows it minus the blank-line
/// separator, against an expected value built independently of the composer.
/// </summary>
public sealed class TeamMemoryPromptTests
{
    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help", "mcp__team__memory", "mcp__team__propose_team_memory"];

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "huddle-teams-root");

    private static readonly string Sep = Path.DirectorySeparatorChar.ToString();

    /// <summary>An omitted <c>teamMemory</c> and an explicit <see langword="null"/> compose the same prompt, and neither has a Team Memory block.</summary>
    [Fact]
    public void Compose_TeamMemoryNull_ByteIdenticalToOmitted_AndNoBlock()
    {
        FakePromptSource prompts = new();

        var omitted = SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."), prompts, "mcp__team__get_help", ToolNames, [], string.Empty, null, SessionScope.Shared);
        var explicitNull = Compose(prompts, teamMemory: null);

        Assert.Equal(omitted, explicitNull);
        Assert.DoesNotContain("Team Memory", explicitNull, StringComparison.Ordinal);
    }

    /// <summary>A snapshot with no Groups gets no block, whatever its <c>NotListed</c> says: the prompt equals the null one.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Compose_EmptyGroups_IdenticalToNull(int notListed)
    {
        FakePromptSource prompts = new();

        var withNull = Compose(prompts, teamMemory: null);
        var withEmpty = Compose(prompts, teamMemory: new TeamMemorySnapshot([], NotListed: notListed));

        Assert.Equal(withNull, withEmpty);
        Assert.DoesNotContain("Team Memory", withEmpty, StringComparison.Ordinal);
        Assert.DoesNotContain("more in the Teams' memory folders", withEmpty, StringComparison.Ordinal);
    }

    /// <summary>One Team: Team-wide entry, a Project with an entry, an empty Project (at most 5 Projects, so it is listed with "Nothing yet.").</summary>
    [Fact]
    public void Compose_OneGroup_RendersWholeBlock()
    {
        FakePromptSource prompts = new();
        var invoicing = Path.Combine(MemoryDir("Business"), "invoicing.md");
        var launch = Path.Combine(ProjectMemoryDir("Business", "Marketing"), "launch-date.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0, new MemoryEntry("Invoices go out on the 1st.", invoicing)),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 0, new MemoryEntry("The campaign launches on 3 November.", launch))),
                    ("Taxes", Snapshot(ProjectMemoryDir("Business", "Taxes"), 0))),
            ],
            NotListed: 0);

        var expected = Block(
            $"""
            - Business, whole Team: {MemoryDir("Business")}{Sep}
            - Business, one Project: {Path.Combine(TeamDir("Business"), "<Project>", "memory")}{Sep}
            """,
            $"""
            Business (folder: Business):
            - invoicing.md - Invoices go out on the 1st.
            Business › Marketing (folder: Business/Marketing):
            - launch-date.md - The campaign launches on 3 November.
            Business › Taxes (folder: Business/Taxes):
            Nothing yet.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>With 6 Projects and only one non-empty, only that Project is listed; the empty Team-wide scope still reads "Nothing yet.".</summary>
    [Fact]
    public void Compose_SixProjectsOneNonEmpty_ListsOnlyThatProject()
    {
        FakePromptSource prompts = new();
        var kickoff = Path.Combine(ProjectMemoryDir("Business", "Charlie"), "kickoff.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0),
                    ("Alpha", Snapshot(ProjectMemoryDir("Business", "Alpha"), 0)),
                    ("Bravo", Snapshot(ProjectMemoryDir("Business", "Bravo"), 0)),
                    ("Charlie", Snapshot(ProjectMemoryDir("Business", "Charlie"), 0, new MemoryEntry("Kickoff is on Monday.", kickoff))),
                    ("Delta", Snapshot(ProjectMemoryDir("Business", "Delta"), 0)),
                    ("Echo", Snapshot(ProjectMemoryDir("Business", "Echo"), 0)),
                    ("Foxtrot", Snapshot(ProjectMemoryDir("Business", "Foxtrot"), 0))),
            ],
            NotListed: 0);

        var expected = Block(
            BusinessPaths(),
            $"""
            Business (folder: Business):
            Nothing yet.
            Business › Charlie (folder: Business/Charlie):
            - kickoff.md - Kickoff is on Monday.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>The boundary: exactly 5 empty Projects are all listed, each with "Nothing yet.".</summary>
    [Fact]
    public void Compose_FiveEmptyProjects_AllListed()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0),
                    ("Alpha", Snapshot(ProjectMemoryDir("Business", "Alpha"), 0)),
                    ("Bravo", Snapshot(ProjectMemoryDir("Business", "Bravo"), 0)),
                    ("Charlie", Snapshot(ProjectMemoryDir("Business", "Charlie"), 0)),
                    ("Delta", Snapshot(ProjectMemoryDir("Business", "Delta"), 0)),
                    ("Echo", Snapshot(ProjectMemoryDir("Business", "Echo"), 0))),
            ],
            NotListed: 0);

        var expected = Block(
            BusinessPaths(),
            """
            Business (folder: Business):
            Nothing yet.
            Business › Alpha (folder: Business/Alpha):
            Nothing yet.
            Business › Bravo (folder: Business/Bravo):
            Nothing yet.
            Business › Charlie (folder: Business/Charlie):
            Nothing yet.
            Business › Delta (folder: Business/Delta):
            Nothing yet.
            Business › Echo (folder: Business/Echo):
            Nothing yet.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>Six empty Projects: none is listed, and the Team-wide heading is still there with "Nothing yet.".</summary>
    [Fact]
    public void Compose_SixEmptyProjects_OnlyTeamWideHeadingListed()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0),
                    ("Alpha", Snapshot(ProjectMemoryDir("Business", "Alpha"), 0)),
                    ("Bravo", Snapshot(ProjectMemoryDir("Business", "Bravo"), 0)),
                    ("Charlie", Snapshot(ProjectMemoryDir("Business", "Charlie"), 0)),
                    ("Delta", Snapshot(ProjectMemoryDir("Business", "Delta"), 0)),
                    ("Echo", Snapshot(ProjectMemoryDir("Business", "Echo"), 0)),
                    ("Foxtrot", Snapshot(ProjectMemoryDir("Business", "Foxtrot"), 0))),
            ],
            NotListed: 0);

        var expected = Block(
            BusinessPaths(),
            """
            Business (folder: Business):
            Nothing yet.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary><c>NotListed</c> of 12 ends the block with the "…and 12 more" line; 0 leaves no such line.</summary>
    [Theory]
    [InlineData(12, "…and 12 more in the Teams' memory folders.")]
    [InlineData(0, null)]
    public void Compose_NotListed_ControlsTheMoreLine(int notListed, string? moreLine)
    {
        FakePromptSource prompts = new();
        var invoicing = Path.Combine(MemoryDir("Business"), "invoicing.md");
        TeamMemorySnapshot snapshot = new(
            [Group("Business", Snapshot(MemoryDir("Business"), 0, new MemoryEntry("Invoices go out on the 1st.", invoicing)))],
            NotListed: notListed);

        var entryIndex = $"""
            Business (folder: Business):
            - invoicing.md - Invoices go out on the 1st.
            """;
        var expected = Block(BusinessPaths(), moreLine is null ? entryIndex : entryIndex + "\n" + moreLine);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>Two groups, given out of alphabetical order: both paths pairs come first, then both indexes, in the given order, then one "more" line.</summary>
    [Fact]
    public void Compose_TwoGroups_BothPathsThenBothIndexesInOrder()
    {
        FakePromptSource prompts = new();
        var invoicing = Path.Combine(MemoryDir("Business"), "invoicing.md");
        var launch = Path.Combine(ProjectMemoryDir("Business", "Marketing"), "launch-date.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group("Ops", Snapshot(MemoryDir("Ops"), 0)),
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0, new MemoryEntry("Invoices go out on the 1st.", invoicing)),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 0, new MemoryEntry("The campaign launches on 3 November.", launch)))),
            ],
            NotListed: 3);

        var expected = Block(
            teams: "Ops and Business",
            paths: $"""
            - Ops, whole Team: {MemoryDir("Ops")}{Sep}
            - Ops, one Project: {Path.Combine(TeamDir("Ops"), "<Project>", "memory")}{Sep}
            {BusinessPaths()}
            """,
            index: $"""
            Ops (folder: Ops):
            Nothing yet.
            Business (folder: Business):
            - invoicing.md - Invoices go out on the 1st.
            Business › Marketing (folder: Business/Marketing):
            - launch-date.md - The campaign launches on 3 November.
            …and 3 more in the Teams' memory folders.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>
    /// The same file name in two folders is told apart by the heading: each group states the folder to pass to the memory tool once, the
    /// Team for its Team-wide Memory and <c>Team/Project</c> for a Project's, while the entry lines keep the <c>file - description</c> form.
    /// </summary>
    [Fact]
    public void Compose_SameFileNameInTwoFolders_EachHeadingNamesItsFolder()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0, new MemoryEntry("The Team's notes.", Path.Combine(MemoryDir("Business"), "notes.md"))),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 0, new MemoryEntry("The campaign notes.", Path.Combine(ProjectMemoryDir("Business", "Marketing"), "notes.md"))))),
            ],
            NotListed: 0);

        var expected = Block(
            BusinessPaths(),
            $"""
            Business (folder: Business):
            - notes.md - The Team's notes.
            Business › Marketing (folder: Business/Marketing):
            - notes.md - The campaign notes.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>An overridden <c>systemPrompt.teamMemoryHeading</c> of <c>[{{scope}}]</c> is honoured for the Team and for each Project.</summary>
    [Fact]
    public void Compose_OverriddenHeading_IsHonoured()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.teamMemoryHeading", "[{{scope}}]");
        var launch = Path.Combine(ProjectMemoryDir("Business", "Marketing"), "launch-date.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 0, new MemoryEntry("The campaign launches on 3 November.", launch)))),
            ],
            NotListed: 0);

        var expected = Block(
            BusinessPaths(),
            $"""
            [Business]
            Nothing yet.
            [Business › Marketing]
            - launch-date.md - The campaign launches on 3 November.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>Overridden <c>teamMemoryPaths</c> and <c>teamMemoryMore</c> receive their placeholders too.</summary>
    [Fact]
    public void Compose_OverriddenPathsAndMore_ReceiveTheirPlaceholders()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.teamMemoryPaths", "{{team}}|{{teamMemoryPath}}|{{projectMemoryPattern}}");
        prompts.SetOverride("systemPrompt.teamMemoryMore", "+{{count}}");
        TeamMemorySnapshot snapshot = new([Group("Business", Snapshot(MemoryDir("Business"), 0))], NotListed: 7);

        var expected = Block(
            $"Business|{MemoryDir("Business")}{Sep}|{Path.Combine(TeamDir("Business"), "<Project>", "memory")}{Sep}",
            """
            Business (folder: Business):
            Nothing yet.
            +7
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>With personal Memory present, the Team block follows it, directly, and precedes the closing part.</summary>
    [Fact]
    public void Compose_WithPersonalMemory_TeamBlockComesAfterPersonalBlock()
    {
        FakePromptSource prompts = new();
        var personalFile = Path.Combine(Root, "Nova", "memory", "code-language.md");
        MemorySnapshot personal = new(
            Path.Combine(Root, "Nova", "memory"),
            [new MemoryEntry("The Human prefers C# for all code.", personalFile)],
            NotListed: 0);
        TeamMemorySnapshot snapshot = new([Group("Business", Snapshot(MemoryDir("Business"), 0))], NotListed: 0);
        var teamBlock = Block(BusinessPaths(), "Business (folder: Business):\nNothing yet.");

        var prompt = Compose(prompts, snapshot, personal);

        Assert.True(prompt.IndexOf("code-language.md", StringComparison.Ordinal) < prompt.IndexOf("## Team Memory", StringComparison.Ordinal));
        Assert.Equal(teamBlock, TeamBlock(prompt, prompts));
        Assert.EndsWith(teamBlock + "\n\n" + SharedText(prompts), prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("code-language.md - The Human prefers C# for all code.\n\n## Team Memory", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>With personal Memory <see langword="null"/> and a Team snapshot present, the Team block is still rendered, right before the closing part.</summary>
    [Fact]
    public void Compose_NoPersonalMemory_TeamBlockStillRendered()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new([Group("Business", Snapshot(MemoryDir("Business"), 0))], NotListed: 0);
        var teamBlock = Block(BusinessPaths(), "Business (folder: Business):\nNothing yet.");

        var prompt = Compose(prompts, snapshot, memory: null);

        Assert.Equal(teamBlock, TeamBlock(prompt, prompts));
        Assert.EndsWith(teamBlock + "\n\n" + SharedText(prompts), prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>Per-Room scope: the Team block precedes <c>roomSessions</c>, and the carry line stays gated on personal Memory (correction 15), so it is absent without it.</summary>
    [Fact]
    public void Compose_PerRoom_NoPersonalMemory_TeamBlockBeforeRoomSessions_NoCarry()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new([Group("Business", Snapshot(MemoryDir("Business"), 0))], NotListed: 0);
        var teamBlock = Block(BusinessPaths(), "Business (folder: Business):\nNothing yet.");
        var roomSessions = prompts.Render("systemPrompt.roomSessions", new Dictionary<string, string>());
        var carry = prompts.Render("systemPrompt.roomSessionsCarry", new Dictionary<string, string>());

        var prompt = Compose(prompts, snapshot, memory: null, SessionScope.PerRoom);

        Assert.Equal(teamBlock, TeamBlock(prompt, prompts, roomSessions));
        Assert.EndsWith(teamBlock + "\n\n" + roomSessions, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.DoesNotContain(carry, prompt, StringComparison.Ordinal);
    }

    /// <summary>Per-Room scope with personal Memory: the order is personal block, Team block, roomSessions, carry.</summary>
    [Fact]
    public void Compose_PerRoom_WithPersonalMemory_TeamBlockBetweenMemoryAndRoomSessions()
    {
        FakePromptSource prompts = new();
        var personalFile = Path.Combine(Root, "Nova", "memory", "code-language.md");
        MemorySnapshot personal = new(
            Path.Combine(Root, "Nova", "memory"),
            [new MemoryEntry("The Human prefers C# for all code.", personalFile)],
            NotListed: 0);
        TeamMemorySnapshot snapshot = new([Group("Business", Snapshot(MemoryDir("Business"), 0))], NotListed: 0);
        var teamBlock = Block(BusinessPaths(), "Business (folder: Business):\nNothing yet.");
        var roomSessions = prompts.Render("systemPrompt.roomSessions", new Dictionary<string, string>());
        var carry = prompts.Render("systemPrompt.roomSessionsCarry", new Dictionary<string, string>());

        var prompt = Compose(prompts, snapshot, personal, SessionScope.PerRoom);

        Assert.Equal(teamBlock, TeamBlock(prompt, prompts, roomSessions));
        Assert.EndsWith(teamBlock + "\n\n" + roomSessions + "\n\n" + carry, prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
        Assert.Contains("code-language.md - The Human prefers C# for all code.\n\n## Team Memory", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>Cap 0 with files present (D5 item 14): the scopes hold only unlisted files, so no "Nothing yet." appears, the Project is omitted, and the "more" line carries the total.</summary>
    [Fact]
    public void Compose_CapSpent_ScopesWithOnlyUnlistedFiles_ShowNoNothingYet()
    {
        FakePromptSource prompts = new();
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 3),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 2))),
            ],
            NotListed: 5);

        var expected = Block(
            BusinessPaths(),
            """
            Business (folder: Business):
            …and 5 more in the Teams' memory folders.
            """);

        var block = TeamBlock(Compose(prompts, snapshot), prompts);

        Assert.Equal(expected, block);
        Assert.DoesNotContain("Nothing yet.", block, StringComparison.Ordinal);
    }

    /// <summary>A Project holding only unlisted files is omitted, while a genuinely empty sibling (at most 5 Projects) is still listed with "Nothing yet.".</summary>
    [Fact]
    public void Compose_ProjectWithOnlyUnlistedFiles_OmittedWhileEmptySiblingListed()
    {
        FakePromptSource prompts = new();
        var invoicing = Path.Combine(MemoryDir("Business"), "invoicing.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    "Business",
                    Snapshot(MemoryDir("Business"), 0, new MemoryEntry("Invoices go out on the 1st.", invoicing)),
                    ("Marketing", Snapshot(ProjectMemoryDir("Business", "Marketing"), 2)),
                    ("Taxes", Snapshot(ProjectMemoryDir("Business", "Taxes"), 0))),
            ],
            NotListed: 2);

        var expected = Block(
            BusinessPaths(),
            $"""
            Business (folder: Business):
            - invoicing.md - Invoices go out on the 1st.
            Business › Taxes (folder: Business/Taxes):
            Nothing yet.
            …and 2 more in the Teams' memory folders.
            """);

        Assert.Equal(expected, TeamBlock(Compose(prompts, snapshot), prompts));
    }

    /// <summary>A Team, Project and summary containing <c>{{x}}</c>-style tokens stay literal: values are rendered first and never re-scanned (D5 item 13).</summary>
    [Fact]
    public void Compose_NamesAndSummariesWithPlaceholderTokens_StayLiteral()
    {
        FakePromptSource prompts = new();
        const string team = "{{x}}";
        const string project = "{{scope}}";
        const string summary = "{{path}}";
        var file = Path.Combine(MemoryDir(team), "note.md");
        TeamMemorySnapshot snapshot = new(
            [
                Group(
                    team,
                    Snapshot(MemoryDir(team), 0, new MemoryEntry(summary, file)),
                    (project, Snapshot(ProjectMemoryDir(team, project), 0))),
            ],
            NotListed: 0);

        var expected = Block(
            teams: team,
            paths: $"""
            - {team}, whole Team: {MemoryDir(team)}{Sep}
            - {team}, one Project: {Path.Combine(TeamDir(team), "<Project>", "memory")}{Sep}
            """,
            index: $"""
            {team} (folder: {team}):
            - note.md - {summary}
            {team} › {project} (folder: {team}/{project}):
            Nothing yet.
            """);

        var block = TeamBlock(Compose(prompts, snapshot), prompts);

        Assert.Equal(expected, block);
        Assert.Contains("- note.md - {{path}}", block, StringComparison.Ordinal); // contains-ok: prompt text; the whole block is asserted above
    }

    private static string TeamDir(string team) => Path.Combine(Root, team);

    private static string MemoryDir(string team) => Path.Combine(Root, team, "memory");

    private static string ProjectMemoryDir(string team, string project) => Path.Combine(Root, team, project, "memory");

    private static MemorySnapshot Snapshot(string memoryDir, int notListed, params MemoryEntry[] entries) =>
        new(memoryDir, entries, notListed);

    private static TeamMemoryGroup Group(string team, MemorySnapshot teamWide, params (string Project, MemorySnapshot Memory)[] projects) =>
        new(team, MemoryDir(team), teamWide, projects);

    private static string BusinessPaths() =>
        $"""
        - Business, whole Team: {MemoryDir("Business")}{Sep}
        - Business, one Project: {Path.Combine(TeamDir("Business"), "<Project>", "memory")}{Sep}
        """;

    /// <summary>
    /// The default-Prompts block for a Persona that is a member (not the manager) of every Team named: the frame, the member
    /// paragraph around a paths part, then an index part, with the composer's line endings.
    /// </summary>
    private static string Block(string paths, string index, string teams = "Business") =>
        $"""
        ## Team Memory
        {teams} keep shared Memory:
        {paths}
        Read it with the mcp__team__memory tool, passing the Team or Project as the folder. You do not write it.
        When you learn something the whole Team should keep, propose it with mcp__team__propose_team_memory, one fact at a time; Huddle files it and records that you proposed it. Keep your own notes in your own Memory.

        {index}
        """.ReplaceLineEndings("\n");

    private static string SharedText(FakePromptSource prompts) =>
        prompts.Render("systemPrompt.sharedSession", new Dictionary<string, string>());

    private static string Compose(
        FakePromptSource prompts,
        TeamMemorySnapshot? teamMemory,
        MemorySnapshot? memory = null,
        SessionScope scope = SessionScope.Shared)
    {
        return SystemPromptComposer.Compose(
            new Persona("Nova", "You are Nova."),
            prompts,
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory,
            scope,
            teamMemory: teamMemory);
    }

    /// <summary>The Team block: from <c>## Team Memory</c> up to the closing part, minus the <c>\n\n</c> separator (D5 item 12).</summary>
    private static string TeamBlock(string prompt, FakePromptSource prompts, string? closing = null)
    {
        var start = prompt.IndexOf("## Team Memory", StringComparison.Ordinal);
        var end = prompt.LastIndexOf("\n\n" + (closing ?? SharedText(prompts)), StringComparison.Ordinal);

        Assert.True(start >= 0, "The prompt has no '## Team Memory' block.");
        Assert.True(end > start, "The Team Memory block is not followed by the closing part.");

        return prompt[start..end];
    }
}
