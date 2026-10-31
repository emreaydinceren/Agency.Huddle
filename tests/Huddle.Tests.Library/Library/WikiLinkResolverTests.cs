using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>
/// Tests for <c>WikiLinkResolver</c>: Spec §6.5 <i>Resolution follows Obsidian</i>, rules 1-4, as a
/// pure function over a fixed note set (corrections-B5 D8 item 1 for the path-suffix rules).
/// </summary>
public sealed class WikiLinkResolverTests
{
    /// <summary>The fixed note set the plan gives for 8.2.t, as root-relative paths.</summary>
    private static readonly string[] Notes =
    [
        "Marketing/brand-voice.md",
        "Marketing/Launch Q4/plan.md",
        "Marketing/Website/plan.md",
        "Platform/plan.md",
        "Marketing/Launch Q4/research/competitors.md",
        "img/diagram.png",
    ];

    /// <summary>Every plan-table row, plus the corrections-B5 D8 item 1 path-suffix rows.</summary>
    public static TheoryData<string, string, string?, bool> ResolveCases()
    {
        TheoryData<string, string, string?, bool> data = new();

        data.Add("Marketing/Launch Q4/plan.md", "brand-voice", "Marketing/brand-voice.md", false);
        data.Add("Marketing/Launch Q4/plan.md", "BRAND-VOICE", "Marketing/brand-voice.md", false);
        data.Add("Marketing/Launch Q4/research/competitors.md", "plan", "Marketing/Launch Q4/plan.md", false);
        data.Add("Marketing/Website/x.md", "plan", "Marketing/Website/plan.md", false);
        data.Add("Platform/x.md", "plan", "Platform/plan.md", false);
        data.Add("Marketing/x.md", "plan", "Marketing/Launch Q4/plan.md", true);
        data.Add("Platform/x.md", "Marketing/Website/plan", "Marketing/Website/plan.md", false);
        data.Add("Platform/x.md", "diagram.png", "img/diagram.png", false);
        data.Add("Platform/x.md", "nope", null, false);
        data.Add("Platform/plan.md", "", "Platform/plan.md", false);

        // corrections-B5 D8 item 1: a target containing '/' is a path SUFFIX, matched segment by segment.
        data.Add("Platform/x.md", "Website/plan", "Marketing/Website/plan.md", false);
        // a leading '/' anchors the suffix at the root.
        data.Add("Platform/x.md", "/Platform/plan", "Platform/plan.md", false);
        data.Add("Platform/x.md", "/Website/plan", null, false);
        // suffix matching is whole-segment: "site" must not match inside "Website".
        data.Add("Platform/x.md", "site/plan", null, false);
        // [[plan.md]] (written .md) resolves like [[plan]].
        data.Add("Platform/x.md", "plan.md", "Platform/plan.md", false);
        // a path target compares segments case-insensitively too.
        data.Add("Platform/x.md", "marketing/website/PLAN", "Marketing/Website/plan.md", false);

        return data;
    }

    /// <summary>Resolves each scenario against the fixed note set and asserts the resolved path and the ambiguity flag.</summary>
    [Theory]
    [MemberData(nameof(ResolveCases))]
    public void Resolve_VariousTargets_ReturnsExpectedResolution(string fromNote, string target, string? expectedPath, bool expectedAmbiguous)
    {
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(target);

        WikiLinkResolution resolution = WikiLinkResolver.Resolve(Notes, fromNote, target);

        Assert.Equal(expectedPath, resolution.Path);
        Assert.Equal(expectedAmbiguous, resolution.IsAmbiguous);
    }

    /// <summary>
    /// Scenarios needing a note set other than the fixed <see cref="Notes"/> list: a name with no known
    /// extension matching <c>&lt;name&gt;.md</c> (corrections-B5 D8 item 2), a <c>.markdown</c> file matching
    /// only when written out, and an ambiguous tie between two path-suffix matches.
    /// </summary>
    public static TheoryData<string[], string, string, string?, bool> ResolveWithOwnNotesCases()
    {
        TheoryData<string[], string, string, string?, bool> data = new();

        string[] meetingNotes = ["Meeting 3.5.md", "Other/x.md"];
        data.Add(meetingNotes, "Other/x.md", "Meeting 3.5", "Meeting 3.5.md", false);

        string[] markdownNotes = ["Notes/log.markdown", "Other/x.md"];
        data.Add(markdownNotes, "Other/x.md", "log", null, false);
        data.Add(markdownNotes, "Other/x.md", "log.markdown", "Notes/log.markdown", false);

        string[] tieNotes = ["A/Website/plan.md", "B/Website/plan.md", "C/x.md"];
        data.Add(tieNotes, "C/x.md", "Website/plan", "A/Website/plan.md", true);

        // link text matches case-insensitively on every OS, even where two notes differ only in
        // case (possible on a case-sensitive file system such as Linux).
        string[] caseOnlyNotes = ["A/plan.md", "A/Plan.md", "A/x.md"];
        data.Add(caseOnlyNotes, "A/x.md", "plan", "A/Plan.md", true);

        return data;
    }

    /// <summary>Resolves each scenario against its own note set.</summary>
    [Theory]
    [MemberData(nameof(ResolveWithOwnNotesCases))]
    public void Resolve_OwnNoteSet_ReturnsExpectedResolution(string[] notes, string fromNote, string target, string? expectedPath, bool expectedAmbiguous)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(target);

        WikiLinkResolution resolution = WikiLinkResolver.Resolve(notes, fromNote, target);

        Assert.Equal(expectedPath, resolution.Path);
        Assert.Equal(expectedAmbiguous, resolution.IsAmbiguous);
    }

    /// <summary>The plan's four <c>ShortestTarget</c> rows against the fixed note set.</summary>
    public static TheoryData<string, string, string> ShortestTargetCases()
    {
        TheoryData<string, string, string> data = new();

        data.Add("Platform/x.md", "Marketing/brand-voice.md", "brand-voice");
        data.Add("Platform/x.md", "Marketing/Website/plan.md", "Website/plan");
        data.Add("Marketing/Launch Q4/x.md", "Marketing/Launch Q4/plan.md", "plan");
        data.Add("Marketing/brand-voice.md", "img/diagram.png", "diagram.png");

        return data;
    }

    /// <summary>Computes the shortest unique target and asserts the exact string the plan names.</summary>
    [Theory]
    [MemberData(nameof(ShortestTargetCases))]
    public void ShortestTarget_VariousTargets_ReturnsShortestUniqueForm(string fromNote, string targetPath, string expected)
    {
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(targetPath);

        string actual = WikiLinkResolver.ShortestTarget(Notes, fromNote, targetPath);

        Assert.Equal(expected, actual);
    }

    /// <summary>Every ordered pair of distinct notes in the fixed set, for the round-trip theory below.</summary>
    public static TheoryData<string, string> NotePairs()
    {
        TheoryData<string, string> data = new();

        foreach (string from in Notes)
        {
            foreach (string note in Notes)
            {
                if (!string.Equals(from, note, StringComparison.Ordinal))
                {
                    data.Add(from, note);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// For every note, from every other note, <c>Resolve(notes, from, ShortestTarget(notes, from, note))</c>
    /// resolves back to that same note, unambiguously - the round trip the plan pins.
    /// </summary>
    [Theory]
    [MemberData(nameof(NotePairs))]
    public void ShortestTarget_RoundTrip_ResolvesBackUnambiguously(string fromNote, string note)
    {
        ArgumentNullException.ThrowIfNull(fromNote);
        ArgumentNullException.ThrowIfNull(note);

        string shortest = WikiLinkResolver.ShortestTarget(Notes, fromNote, note);
        WikiLinkResolution resolution = WikiLinkResolver.Resolve(Notes, fromNote, shortest);

        Assert.Equal(note, resolution.Path);
        Assert.False(resolution.IsAmbiguous);
    }
}
