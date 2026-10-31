using Agency.Huddle.App.Teams;
using CommandExits = Agency.Huddle.App.Teams.DoneRule.CommandExits;
using DocumentReviewedBy = Agency.Huddle.App.Teams.DoneRule.DocumentReviewedBy;
using DoneItem = Agency.Huddle.App.Teams.DoneItem;
using EvidenceFrom = Agency.Huddle.App.Teams.DoneRule.EvidenceFrom;
using ProofItemSource = Agency.Huddle.App.Teams.ProofItemSource;
using TaskTagged = Agency.Huddle.App.Teams.DoneRule.TaskTagged;
using Unreadable = Agency.Huddle.App.Teams.DoneRule.Unreadable;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="ProcessDocument.DefinitionOfDone"/>: the <c>## Definition of Done</c> body section (ADR-0062).</summary>
public sealed class ProcessDocumentDoneTests
{
    private const string OutlineAndDone = """
        # Flow
        ## When it applies
        ## Stages
        ## Who does what
        ## If a role is not hired
        ## Gates
        ## Cadence
        ## Escalation
        ## Definition of Done

        - QA evidence: evidence from verify
        """;

    private static ProcessDocument Parse(string body, string lineEnding = "\n")
    {
        string text = "---\ntitle: 'Flow'\n---\n" + body.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        text = text.Replace("\n", lineEnding, StringComparison.Ordinal);
        Assert.True(ProcessDocument.TryParse(text, out ProcessDocument? document));
        Assert.NotNull(document);
        return document;
    }

    /// <summary>One bullet of each of the four rules parses in file order, keeping labels and arguments.</summary>
    [Fact]
    public void DefinitionOfDone_FourRules_ParseInOrder()
    {
        ProcessDocument document = Parse("""
            ## Definition of Done

            - QA evidence: evidence from verify
            - Citation check: task tagged citation-check
            - Tests pass: command `dotnet test Huddle.slnx --` exits 0
            - Skeptic review: document reviewed by verify
            """);

        Assert.Equal(
            [
                new DoneItem("QA evidence", new EvidenceFrom("verify"), ProofItemSource.Process),
                new DoneItem("Citation check", new TaskTagged("citation-check"), ProofItemSource.Process),
                new DoneItem("Tests pass", new CommandExits("dotnet test Huddle.slnx --"), ProofItemSource.Process),
                new DoneItem("Skeptic review", new DocumentReviewedBy("verify"), ProofItemSource.Process),
            ],
            document.DefinitionOfDone);
    }

    /// <summary>The command between backticks keeps its inner spaces exactly.</summary>
    [Fact]
    public void DefinitionOfDone_CommandInBackticks_KeepsInnerSpaces()
    {
        ProcessDocument document = Parse("## Definition of Done\n- Build: command `dotnet  build   -c Release` exits 0");

        DoneItem item = Assert.Single(document.DefinitionOfDone);
        Assert.Equal(new CommandExits("dotnet  build   -c Release"), item.Rule);
    }

    /// <summary>A rule the grammar does not know is Unreadable and carries the line as written.</summary>
    [Fact]
    public void DefinitionOfDone_UnknownRule_IsUnreadableWithText()
    {
        ProcessDocument document = Parse("## Definition of Done\n- Vibes: feels right to everyone");

        DoneItem item = Assert.Single(document.DefinitionOfDone);
        Assert.Equal("Vibes", item.Label);
        Assert.Equal(new Unreadable("Vibes: feels right to everyone"), item.Rule);
        Assert.Equal(ProofItemSource.Process, item.Source);
    }

    /// <summary>A bullet with no colon is Unreadable; its label and text are the whole bullet.</summary>
    [Fact]
    public void DefinitionOfDone_BulletWithoutColon_IsUnreadable()
    {
        ProcessDocument document = Parse("## Definition of Done\n- evidence from verify");

        DoneItem item = Assert.Single(document.DefinitionOfDone);
        Assert.Equal("evidence from verify", item.Label);
        Assert.Equal(new Unreadable("evidence from verify"), item.Rule);
    }

    /// <summary>A document without the section has an empty checklist.</summary>
    [Fact]
    public void DefinitionOfDone_SectionAbsent_IsEmpty()
    {
        ProcessDocument document = Parse("# Flow\n## Stages");

        Assert.Empty(document.DefinitionOfDone);
    }

    /// <summary>A heading with no bullets under it has an empty checklist.</summary>
    [Fact]
    public void DefinitionOfDone_SectionEmpty_IsEmpty()
    {
        ProcessDocument document = Parse("## Definition of Done\n\n");

        Assert.Empty(document.DefinitionOfDone);
    }

    /// <summary>Bullets under the next heading are not part of the checklist.</summary>
    [Fact]
    public void DefinitionOfDone_StopsAtNextHeading()
    {
        ProcessDocument document = Parse("""
            ## Definition of Done
            - QA evidence: evidence from verify
            ## Cadence
            - Not mine: evidence from build
            """);

        DoneItem item = Assert.Single(document.DefinitionOfDone);
        Assert.Equal("QA evidence", item.Label);
    }

    /// <summary>CRLF and LF text parse to the same list.</summary>
    [Fact]
    public void DefinitionOfDone_CrlfAndLf_ParseTheSame()
    {
        ProcessDocument lf = Parse(OutlineAndDone);
        ProcessDocument crlf = Parse(OutlineAndDone, "\r\n");

        Assert.Single(lf.DefinitionOfDone);
        Assert.Equal(lf.DefinitionOfDone, crlf.DefinitionOfDone);
    }

    /// <summary>The section is optional: its absence never raises a missing-section warning.</summary>
    [Fact]
    public void DefinitionOfDone_IsNotAMissingSection()
    {
        ProcessDocument without = Parse(OutlineAndDone.Replace("## Definition of Done", "## Notes", StringComparison.Ordinal));

        Assert.Empty(without.MissingSections);
        Assert.DoesNotContain("Definition of Done", Parse("# Flow").MissingSections);
    }

    /// <summary>The heading matches regardless of case.</summary>
    [Fact]
    public void DefinitionOfDone_HeadingCaseInsensitive()
    {
        ProcessDocument document = Parse("## DEFINITION of done\n- QA evidence: evidence from verify");

        DoneItem item = Assert.Single(document.DefinitionOfDone);
        Assert.Equal(new EvidenceFrom("verify"), item.Rule);
    }
}
