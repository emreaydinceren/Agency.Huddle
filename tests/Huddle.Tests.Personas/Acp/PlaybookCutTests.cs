namespace Agency.Huddle.Tests.Acp;

using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>Pins the size limits at delivery (Team Playbooks spec §5.3, §6.3): the per-Playbook cut and the total cap.</summary>
public sealed class PlaybookCutTests
{
    private const string NoFilesLine = "The rest was cut; ask the Human to shorten this Playbook.";

    private static readonly IReadOnlyList<string> ToolNames = ["mcp__team__get_help"];

    /// <summary>The two limits default to 4000 and 16000 characters (<c>Team:Playbooks:MaxChars</c>, <c>MaxTotalChars</c>).</summary>
    [Fact]
    public void Options_DefaultToTheSpecLimits()
    {
        PlaybookOptions options = new();

        Assert.Equal(4000, options.MaxChars);
        Assert.Equal(16000, options.MaxTotalChars);
        Assert.Equal("Team:Playbooks", PlaybookOptions.SectionName);
    }

    /// <summary>A body over <c>MaxChars</c> is cut at the last line break before the limit and the file pointer follows.</summary>
    [Fact]
    public void BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer()
    {
        const string Path = "Teams/Sales/_member-playbook.md";
        string body = Body("alpha", 60);
        PlaybookSnapshot snapshot = new([Custom(PlaybookKind.Member, "Sales", body, Path)]) { MaxChars = 1000 };

        PromptPart part = MemberPart(Parts(snapshot));

        string expectedKept = body[..body[..1000].LastIndexOf('\n')];
        string head = "## Member Playbook for Sales (this Team's own)\n";
        string pointer = PointerTo(Path);
        Assert.StartsWith(head + expectedKept, part.Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        Assert.EndsWith(pointer, part.Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        string between = part.Text[(head.Length + expectedKept.Length)..^pointer.Length];
        Assert.Equal(string.Empty, between.Trim('\n'));
        Assert.DoesNotContain("alpha line 59", part.Text, StringComparison.Ordinal);
        Assert.Equal(new PromptPartSource.File(Path), part.Source);
    }

    /// <summary>A body of exactly <c>MaxChars</c> is not cut and carries no pointer.</summary>
    [Fact]
    public void BodyAtMaxChars_IsNotCut()
    {
        string body = Body("alpha", 10);
        PlaybookSnapshot snapshot = new([Custom(PlaybookKind.Member, "Sales", body, "Teams/Sales/_member-playbook.md")]) { MaxChars = body.Length };

        PromptPart part = MemberPart(Parts(snapshot));

        Assert.Equal("## Member Playbook for Sales (this Team's own)\n" + body, part.Text);
    }

    /// <summary>An Adapter without file tools gets the no-files line, never a path to read.</summary>
    [Fact]
    public void BodyOverMaxChars_WithoutFileTools_GetsTheNoFilesLine()
    {
        const string Path = "Teams/Sales/_member-playbook.md";
        PlaybookSnapshot snapshot = new([Custom(PlaybookKind.Member, "Sales", Body("alpha", 60), Path)])
        {
            MaxChars = 1000,
            FilesReadable = false,
        };

        PromptPart part = MemberPart(Parts(snapshot));

        Assert.EndsWith(NoFilesLine, part.Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        Assert.DoesNotContain(Path, part.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("The rest of this Playbook is in", part.Text, StringComparison.Ordinal);
    }

    /// <summary>Over the total, the last customized Playbook in <c>teams</c> order is replaced by its pointer first; the first is kept.</summary>
    [Fact]
    public void TotalOverCap_DropsTheLastCustomizedFirst()
    {
        PlaybookSnapshot snapshot = Three(maxTotal: int.MaxValue);
        int total = Total(Parts(snapshot));

        PlaybookSnapshot capped = Three(maxTotal: total - 1500);

        IReadOnlyList<PromptPart> parts = Parts(capped);
        PromptPart[] members = [.. parts.Where(static part => part.Label == "Member Playbook")];
        Assert.Contains("alpha line 0", members[0].Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        Assert.Contains("bravo line 0", members[1].Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        AssertDropped(members[2], "charlie", "Teams/C/_member-playbook.md");
    }

    /// <summary>When one drop is not enough, the next to last goes too, still last-first; the first is kept.</summary>
    [Fact]
    public void TotalFarOverCap_DropsLastFirstThenTheOneBefore()
    {
        int total = Total(Parts(Three(maxTotal: int.MaxValue)));

        IReadOnlyList<PromptPart> parts = Parts(Three(maxTotal: total - 3000));

        PromptPart[] members = [.. parts.Where(static part => part.Label == "Member Playbook")];
        Assert.Contains("alpha line 0", members[0].Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        AssertDropped(members[1], "bravo", "Teams/B/_member-playbook.md");
        AssertDropped(members[2], "charlie", "Teams/C/_member-playbook.md");
    }

    /// <summary>However small the total, the Huddle rules and the default Playbook are never dropped.</summary>
    [Fact]
    public void TotalCapOfOne_NeverDropsRulesOrDefaults()
    {
        PlaybookSnapshot snapshot = new(
        [
            new PlaybookSection(PlaybookKind.Member, ["Ops"], new FakePromptSource().Raw("systemPrompt.memberPlaybook"), new PromptPartSource.Prompt("systemPrompt.memberPlaybook", false)),
            Custom(PlaybookKind.Member, "A", Body("alpha", 5), "Teams/A/_member-playbook.md"),
            new PlaybookSection(PlaybookKind.Manager, ["Ops"], new FakePromptSource().Raw("systemPrompt.managerPlaybook"), new PromptPartSource.Prompt("systemPrompt.managerPlaybook", false)),
        ])
        {
            MaxTotalChars = 1,
        };

        IReadOnlyList<PromptPart> parts = Parts(snapshot);

        Assert.Contains(parts, static part => part.Label == "Huddle rules (Member)" && part.Text.Contains("Record on the Task.", StringComparison.Ordinal)); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        Assert.Contains(parts, static part => part.Label == "Huddle rules (Manager)");
        Assert.Contains(parts, static part => part.Text.StartsWith("## Member Playbook (Huddle default) for Ops\n## How to work on a Task", StringComparison.Ordinal)); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        Assert.Contains(parts, static part => part.Text.StartsWith("## Manager Playbook (Huddle default) for Ops\n## How to run the Team", StringComparison.Ordinal)); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
        AssertDropped(parts.Single(static part => part.Source is PromptPartSource.File), "alpha", "Teams/A/_member-playbook.md");
    }

    private static void AssertDropped(PromptPart part, string marker, string path)
    {
        Assert.DoesNotContain(marker + " line 0", part.Text, StringComparison.Ordinal);
        Assert.EndsWith(PointerTo(path), part.Text, StringComparison.Ordinal); // contains-ok: a kept or cut part is a prefix of its body plus the pointer line; the cut point is pinned by BodyOverMaxChars_IsCutAtALineBreak_WithTheFilePointer
    }

    private static string PointerTo(string path) =>
        "The rest of this Playbook is in `" + path + "`; read it before you work on this Team's Tasks.";

    private static PlaybookSnapshot Three(int maxTotal) =>
        new(
        [
            Custom(PlaybookKind.Member, "A", Body("alpha", 30), "Teams/A/_member-playbook.md"),
            Custom(PlaybookKind.Member, "B", Body("bravo", 30), "Teams/B/_member-playbook.md"),
            Custom(PlaybookKind.Member, "C", Body("charlie", 30), "Teams/C/_member-playbook.md"),
        ])
        {
            MaxChars = 4000,
            MaxTotalChars = maxTotal,
        };

    /// <summary>The sum of the lengths of every Playbook part's text: the frame, the rules and each Playbook.</summary>
    private static int Total(IReadOnlyList<PromptPart> parts) =>
        parts
            .Where(static part => part.Label == "Playbooks" || part.Label.StartsWith("Huddle rules", StringComparison.Ordinal) || part.Label.EndsWith("Playbook", StringComparison.Ordinal))
            .Sum(static part => part.Text.Length);

    private static PlaybookSection Custom(PlaybookKind kind, string team, string body, string path) =>
        new(kind, [team], body, new PromptPartSource.File(path));

    private static string Body(string marker, int lines) =>
        string.Join('\n', Enumerable.Range(0, lines).Select(index => $"{marker} line {index} of the Playbook, with some words to fill it."));

    private static PromptPart MemberPart(IReadOnlyList<PromptPart> parts) =>
        Assert.Single(parts, static part => part.Label == "Member Playbook");

    private static IReadOnlyList<PromptPart> Parts(PlaybookSnapshot snapshot) =>
        SystemPromptComposer.ComposeParts(
            new Persona("Nova", "You are Nova."),
            new FakePromptSource(),
            "mcp__team__get_help",
            ToolNames,
            [],
            string.Empty,
            memory: null,
            SessionScope.Shared,
            agentsMd: null,
            playbooks: snapshot);
}
