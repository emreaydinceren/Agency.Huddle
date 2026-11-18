namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Acp.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Pins the Playbook parts of <see cref="SystemPromptComposer"/> and the <see cref="PlaybookSnapshotBuilder"/> that feeds them
/// (Team Playbooks spec §3.2, §6.1, §6.2): where the parts sit, their labels, their sources and their headings.
/// </summary>
public sealed class PlaybookComposerTests : IDisposable
{
    private const string Frame =
        "Your Teams' Playbooks follow. They say how to work in a Team, whatever the work. The Huddle rules are enforced by Huddle. " +
        "The Playbook text is the Human's; where a Team has its own, it replaces the default for that Team only.";

    private static readonly IReadOnlyList<string> ToolNames =
    [
        "mcp__team__get_help",
        "mcp__team__add_comment",
        "mcp__team__add_evidence",
        "mcp__team__log_change",
        "mcp__team__create_task",
        "mcp__team__update_task",
        "mcp__team__close_task",
        "mcp__team__triage_request",
        "mcp__team__memory",
    ];

    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-playbookcomp-" + Guid.NewGuid().ToString("N"));

    /// <summary>Removes the temporary Teams folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>With no snapshot, and with an empty one, the prompt is byte-identical and holds no Playbook part.</summary>
    [Fact]
    public void NoSnapshot_GivesByteIdenticalOutput()
    {
        IReadOnlyList<PromptPart> none = Parts(null);
        IReadOnlyList<PromptPart> empty = Parts(PlaybookSnapshot.Empty);

        Assert.Equal(none, empty);
        Assert.DoesNotContain(none, part => part.Label.Contains("Playbook", StringComparison.Ordinal));
        Assert.DoesNotContain(none, part => part.Label.StartsWith("Huddle rules", StringComparison.Ordinal));
        Assert.Equal(
            SystemPromptComposer.Compose(new Persona("Nova", "You are Nova."), new FakePromptSource(), "mcp__team__get_help", ToolNames, [], string.Empty, memory: null, SessionScope.Shared, agentsMd: AgentsMd),
            string.Join("\n\n", empty.Select(static part => part.Text)));
    }

    /// <summary>A member gets the frame, the Member rules and the default, in that order, after the AGENTS.md block and before Identity.</summary>
    [Fact]
    public void Member_GetsRulesThenDefault_AfterAgentsMdBeforeIdentity()
    {
        PlaybookSnapshot snapshot = new([DefaultSection(PlaybookKind.Member, "Ops")]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        string[] labels = [.. parts.Select(static part => part.Label)];
        int frame = Array.IndexOf(labels, "Playbooks");
        Assert.True(frame > 0);
        Assert.Equal("AGENTS.md block", labels[frame - 1]);
        Assert.Equal(["Playbooks", "Huddle rules (Member)", "Member Playbook"], labels[frame..(frame + 3)]);
        Assert.Equal("Identity", labels[frame + 3]);
        Assert.Equal(new PromptPart("Playbooks", new PromptPartSource.Prompt("systemPrompt.playbooks", false), Frame), parts[frame]);
        Assert.Equal(
            new PromptPart(
                "Huddle rules (Member)",
                new PromptPartSource.Embedded("huddleRules.member"),
                "## Huddle rules for members\n" + HuddleRules.RenderMember(ToolNames)),
            parts[frame + 1]);
        Assert.Equal(
            new PromptPart(
                "Member Playbook",
                new PromptPartSource.Prompt("systemPrompt.memberPlaybook", false),
                "## Member Playbook (Huddle default) for Ops\n" + DefaultText(PlaybookKind.Member)),
            parts[frame + 2]);
    }

    /// <summary>A manager gets the Manager rules and Playbook after the Member ones, each with its label and source.</summary>
    [Fact]
    public void Manager_GetsManagerPartsAfterMemberParts()
    {
        PlaybookSnapshot snapshot = new([DefaultSection(PlaybookKind.Member, "Ops"), DefaultSection(PlaybookKind.Manager, "Ops")]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        string[] labels = [.. parts.Select(static part => part.Label)];
        int frame = Array.IndexOf(labels, "Playbooks");
        Assert.Equal(
            ["Playbooks", "Huddle rules (Member)", "Member Playbook", "Huddle rules (Manager)", "Manager Playbook"],
            labels[frame..(frame + 5)]);
        Assert.Equal(
            new PromptPart(
                "Huddle rules (Manager)",
                new PromptPartSource.Embedded("huddleRules.manager"),
                "## Huddle rules for managers\n" + HuddleRules.RenderManager(ToolNames)),
            parts[frame + 3]);
        Assert.Equal(
            new PromptPart(
                "Manager Playbook",
                new PromptPartSource.Prompt("systemPrompt.managerPlaybook", false),
                "## Manager Playbook (Huddle default) for Ops\n" + DefaultText(PlaybookKind.Manager)),
            parts[frame + 4]);
    }

    /// <summary>Two Teams on the default give one default part, headed with both names in <c>teams</c> order.</summary>
    [Fact]
    public void TwoDefaultTeams_GiveOneDefaultHeadedWithBothNames()
    {
        PlaybookSnapshot snapshot = new([DefaultSection(PlaybookKind.Member, "Ops", "Docs")]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        PromptPart[] playbooks = [.. parts.Where(static part => part.Label == "Member Playbook")];
        PromptPart only = Assert.Single(playbooks);
        Assert.StartsWith("## Member Playbook (Huddle default) for Ops and Docs\n", only.Text, StringComparison.Ordinal); // contains-ok: the heading; the body is the default pinned by PromptCatalogTests
    }

    /// <summary>A customized Team's part is headed "(this Team's own)", comes from its file and follows the default; the manager one reads the same.</summary>
    [Fact]
    public void CustomizedTeam_IsHeadedAsItsOwn_AfterTheDefault()
    {
        const string Path = "Teams/Sales/_member-playbook.md";
        const string ManagerPath = "Teams/Sales/_manager-playbook.md";
        PlaybookSnapshot snapshot = new(
        [
            new PlaybookSection(PlaybookKind.Member, ["Sales"], "Sell kindly.", new PromptPartSource.File(Path)),
            DefaultSection(PlaybookKind.Member, "Ops"),
            new PlaybookSection(PlaybookKind.Manager, ["Sales"], "Lead kindly.", new PromptPartSource.File(ManagerPath)),
        ]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        PromptPart[] members = [.. parts.Where(static part => part.Label == "Member Playbook")];
        Assert.Equal(2, members.Length);
        Assert.Equal("## Member Playbook (Huddle default) for Ops\n" + DefaultText(PlaybookKind.Member), members[0].Text);
        Assert.Equal(
            new PromptPart("Member Playbook", new PromptPartSource.File(Path), "## Member Playbook for Sales (this Team's own)\nSell kindly."),
            members[1]);
        Assert.Contains(
            new PromptPart("Manager Playbook", new PromptPartSource.File(ManagerPath), "## Manager Playbook for Sales (this Team's own)\nLead kindly."),
            parts);
    }

    /// <summary>Several customized Teams each get a part, in <c>teams</c> order, and none is merged with another even if their text is equal.</summary>
    [Fact]
    public void CustomizedTeams_AreSentSeparately_InTeamsOrder()
    {
        PlaybookSnapshot snapshot = new(
        [
            new PlaybookSection(PlaybookKind.Member, ["Sales"], "Same text.", new PromptPartSource.File("Teams/Sales/_member-playbook.md")),
            new PlaybookSection(PlaybookKind.Member, ["Docs"], "Same text.", new PromptPartSource.File("Teams/Docs/_member-playbook.md")),
        ]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        string[] headings = [.. parts.Where(static part => part.Label == "Member Playbook").Select(static part => part.Text.Split('\n')[0])];
        Assert.Equal(
            ["## Member Playbook for Sales (this Team's own)", "## Member Playbook for Docs (this Team's own)"],
            headings);
    }

    /// <summary>When every Team of the Agent has its own file, no default part is sent.</summary>
    [Fact]
    public void EveryTeamCustomized_GetsNoDefault()
    {
        PlaybookSnapshot snapshot = new(
        [
            new PlaybookSection(PlaybookKind.Member, ["Sales"], "Sell kindly.", new PromptPartSource.File("Teams/Sales/_member-playbook.md")),
        ]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        Assert.DoesNotContain(parts, static part => part.Source is PromptPartSource.Prompt { Key: "systemPrompt.memberPlaybook" });
        Assert.DoesNotContain(parts, static part => part.Text.Contains("(Huddle default)", StringComparison.Ordinal));
        Assert.Contains(parts, static part => part.Label == "Huddle rules (Member)");
    }

    /// <summary>An overridden default says so in its source, so the Prompt blocks page can mark it.</summary>
    [Fact]
    public void OverriddenDefault_IsMarkedOverriddenInItsSource()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("systemPrompt.memberPlaybook", "Our house way.");
        PlaybookSnapshot snapshot = new(
            [new PlaybookSection(PlaybookKind.Member, ["Ops"], "Our house way.", new PromptPartSource.Prompt("systemPrompt.memberPlaybook", true))]);

        IReadOnlyList<PromptPart> parts = Parts(snapshot, prompts);

        Assert.Contains(
            new PromptPart(
                "Member Playbook",
                new PromptPartSource.Prompt("systemPrompt.memberPlaybook", true),
                "## Member Playbook (Huddle default) for Ops\nOur house way."),
            parts);
    }

    /// <summary>An Agent in no catalog Team gets no Playbook at all, from the builder.</summary>
    [Fact]
    public void Builder_AgentInNoCatalogTeam_GetsNoPlaybook()
    {
        IReadOnlyList<TeamSummary> catalog = [new TeamSummary("Ops", [], ["Ada"], true)];

        PlaybookSnapshot none = this.Build(catalog, "Nova", []);
        PlaybookSnapshot unknown = this.Build(catalog, "Nova", ["Ghosts"]);

        Assert.Empty(none.Sections);
        Assert.Empty(unknown.Sections);
    }

    /// <summary>
    /// The builder names every Team that has no file in one default section, spelled as the catalog spells it and in the written order, and reads a
    /// hand-made file (no frontmatter) as the Team's own text.
    /// </summary>
    [Fact]
    public void Builder_MixesDefaultAndHandMadeFile_InTeamsOrder()
    {
        IReadOnlyList<TeamSummary> catalog =
        [
            new TeamSummary("Ops", [], ["Nova"], true),
            new TeamSummary("Sales", [], ["Nova"], true),
            new TeamSummary("Docs", [], ["Nova"], true),
        ];
        string file = Path.Combine(this.root, "Sales", "_member-playbook.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "---\r\nbasedOn: 'sha256:3f9a0c1e7b2d4a65'\r\n---\r\n## Our way\r\nSell kindly.\r\n");

        PlaybookSnapshot snapshot = this.Build(catalog, "Nova", ["ops", "Sales", "DOCS"]);

        string[] expected =
        [
            Describe(new PlaybookSection(PlaybookKind.Member, ["Ops", "Docs"], new FakePromptSource().Raw("systemPrompt.memberPlaybook"), new PromptPartSource.Prompt("systemPrompt.memberPlaybook", false))),
            Describe(new PlaybookSection(PlaybookKind.Member, ["Sales"], "## Our way\nSell kindly.\n", new PromptPartSource.File(file))),
        ];
        Assert.Equal(expected, snapshot.Sections.Select(Describe));
    }

    /// <summary>The manager of a Team also gets that Team's Manager section; a member who is not its manager does not.</summary>
    [Fact]
    public void Builder_ManagerGetsManagerSection_OnlyForTheTeamItManages()
    {
        IReadOnlyList<TeamSummary> catalog =
        [
            new TeamSummary("Ops", [], ["Bea"], true) { Manager = "Bea" },
            new TeamSummary("Docs", [], ["Bea"], true) { Manager = "Ada" },
        ];

        PlaybookSnapshot snapshot = this.Build(catalog, "Bea", ["Ops", "Docs"]);

        PlaybookSection manager = Assert.Single(snapshot.Sections, static section => section.Kind == PlaybookKind.Manager);
        Assert.Equal(["Ops"], manager.Teams);
        Assert.Equal(new PromptPartSource.Prompt("systemPrompt.managerPlaybook", false), manager.Source);
        Assert.DoesNotContain(this.Build(catalog, "Cy", ["Ops"]).Sections, static section => section.Kind == PlaybookKind.Manager);
    }

    private static PlaybookSection DefaultSection(PlaybookKind kind, params string[] teams) =>
        new(kind, teams, DefaultText(kind), new PromptPartSource.Prompt(KeyOf(kind), false));

    private static string KeyOf(PlaybookKind kind) =>
        kind == PlaybookKind.Member ? "systemPrompt.memberPlaybook" : "systemPrompt.managerPlaybook";

    private static string DefaultText(PlaybookKind kind) => new FakePromptSource().Raw(KeyOf(kind));

    private static IReadOnlyList<PromptPart> Parts(PlaybookSnapshot? playbooks, FakePromptSource? prompts = null) =>
        SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."),
            prompts ?? new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            agentsMd: AgentsMd,
            playbooks: playbooks);

    private static AgentsMdSnapshot AgentsMd =>
        new([new AgentsMdSection(AgentsMdLevel.HuddleWide, null, null, "T/AGENTS.md", "Huddle text.", false)], []);

    private PlaybookSnapshot Build(IReadOnlyList<TeamSummary> catalog, string persona, IReadOnlyList<string> teams) =>
        PlaybookSnapshotBuilder.Build(
            new FakePromptSource(),
            catalog,
            persona,
            teams,
            this.root,
            new PlaybookOptions(),
            filesReadable: true,
            NullLogger.Instance);

    /// <summary>Describes a section by value, since <see cref="PlaybookSection.Teams"/> is a list.</summary>
    private static string Describe(PlaybookSection section) =>
        $"{section.Kind}|{string.Join(',', section.Teams)}|{section.Text}|{section.Source}";
}
