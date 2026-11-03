using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests that <see cref="ProcessDocument"/> reads the prose of an <c>AGENTS.md</c> body, with or without frontmatter (Spec 6.2, D-10).</summary>
public sealed class ProcessProseTests
{
    private const string Prose = "# Home\n## Identity\nWe fix things.\n## Onboarding\n- Step one\n## Definition of Done\n- [ ] Tests pass\n";

    private static readonly string[] AllOutlineHeadings =
    [
        "When it applies",
        "Stages",
        "Who does what",
        "If a role is not hired",
        "Gates",
        "Cadence",
        "Escalation",
    ];

    private static ProcessDocument Parse(string text)
    {
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);
        return document;
    }

    /// <summary>A body with no frontmatter parses and keeps its first heading as the title.</summary>
    [Fact]
    public void TryParse_NoFrontmatter_Succeeds()
    {
        ProcessDocument document = Parse(Prose);

        Assert.Equal("Home", document.Title);
        Assert.Equal(Prose, document.Text);
    }

    /// <summary>The onboarding steps of a frontmatter-free body are exactly the section's bullet.</summary>
    [Fact]
    public void OnboardingSteps_NoFrontmatter_ReturnsTheSectionBullet()
    {
        Assert.Equal("- Step one", ProcessDocument.OnboardingSteps(Prose));
    }

    /// <summary>The Done items of a frontmatter-free body are the checklist labels without the checkbox.</summary>
    [Fact]
    public void DefinitionOfDone_NoFrontmatter_ReadsTheChecklistLabels()
    {
        ProcessDocument document = Parse(Prose);

        Assert.Equal(["Tests pass"], document.DefinitionOfDone.Select(static item => item.Label).ToArray());
        Assert.All(document.DefinitionOfDone, static item => Assert.Equal(ProofItemSource.Process, item.Source));
    }

    /// <summary>A body that carries none of the outline headings reports all seven, in outline order.</summary>
    [Fact]
    public void MissingSections_NoFrontmatterNoOutline_ListsEveryHeadingInOrder()
    {
        ProcessDocument document = Parse(Prose);

        Assert.Equal(AllOutlineHeadings, document.MissingSections.ToArray());
    }

    /// <summary>A body that carries every outline heading reports none missing.</summary>
    [Fact]
    public void MissingSections_NoFrontmatterFullOutline_IsEmpty()
    {
        string text = "# Flow\n" + string.Concat(AllOutlineHeadings.Select(static heading => "## " + heading + "\n"));

        Assert.Empty(Parse(text).MissingSections);
    }

    /// <summary>A leading frontmatter block is skipped: the prose reads the same as without it.</summary>
    [Fact]
    public void TryParse_WithFrontmatter_ReadsTheSameProse()
    {
        ProcessDocument document = Parse("---\ntitle: 'Other'\n---\n" + Prose);

        Assert.Equal("Home", document.Title);
        Assert.Equal("- Step one", ProcessDocument.OnboardingSteps("---\ntitle: 'Other'\n---\n" + Prose));
        Assert.Equal(["Tests pass"], document.DefinitionOfDone.Select(static item => item.Label).ToArray());
        Assert.Equal(AllOutlineHeadings, document.MissingSections.ToArray());
    }

    /// <summary>CRLF line endings parse identically to LF.</summary>
    [Fact]
    public void TryParse_NoFrontmatterCrlf_ReadsTheSameProse()
    {
        string crlf = Prose.Replace("\n", "\r\n", StringComparison.Ordinal);
        ProcessDocument document = Parse(crlf);

        Assert.Equal("- Step one", ProcessDocument.OnboardingSteps(crlf));
        Assert.Equal(["Tests pass"], document.DefinitionOfDone.Select(static item => item.Label).ToArray());
    }
}
