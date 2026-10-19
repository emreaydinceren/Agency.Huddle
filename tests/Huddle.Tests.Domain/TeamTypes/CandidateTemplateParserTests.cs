using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="CandidateTemplateParser"/> (Spec §Candidate).</summary>
public sealed class CandidateTemplateParserTests
{
    /// <summary>The Spec's example file parses into a template with no problems.</summary>
    [Fact]
    public void Parse_SpecExample_ReturnsTemplate()
    {
        const string text = "---\r\ntitle: 'Reviewer'\r\nconsult_when: 'A change is in Review'\r\n---\r\nYou assume every change is wrong until it is shown otherwise.\r\n";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.Empty(result.Problems);
        Assert.NotNull(result.Template);
        Assert.Equal("Reviewer", result.Template.Title);
        Assert.Null(result.Template.Alias);
        Assert.Null(result.Template.Adapter);
        Assert.Empty(result.Template.Skills);
        Assert.Equal("You assume every change is wrong until it is shown otherwise.", result.Template.Stance);
        Assert.Single(result.Template.JobDescriptionFields);
        Assert.Equal("consult_when", result.Template.JobDescriptionFields[0].Key);
        Assert.Equal("A change is in Review", result.Template.JobDescriptionFields[0].Value);
    }

    /// <summary>Alias, skills and adapter are read into their own members and kept out of the job description fields.</summary>
    [Fact]
    public void Parse_AliasSkillsAdapter_AreSeparatedFromJobDescription()
    {
        const string text = "---\ntitle: 'Developer'\nalias: 'dev'\nskills: ['tdd', 'incremental']\nadapter: 'claude-agent-acp'\nconsult_when: 'Ready to build'\n---\nBuild it.";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.Empty(result.Problems);
        Assert.NotNull(result.Template);
        Assert.Equal("dev", result.Template.Alias);
        Assert.Equal("claude-agent-acp", result.Template.Adapter);
        Assert.Equal(["tdd", "incremental"], result.Template.Skills);
        Assert.Equal(["consult_when"], result.Template.JobDescriptionFields.Select(static f => f.Key));
    }

    /// <summary>Non-reserved keys reach the job description in file order.</summary>
    [Fact]
    public void Parse_SeveralJobDescriptionKeys_PreservesFileOrder()
    {
        const string text = "---\nzeta: 'last alphabetically'\ntitle: 'Tester'\nconsult_when: 'Always'\nalpha: 'first alphabetically'\n---\nTest.";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.NotNull(result.Template);
        Assert.Equal(["zeta", "consult_when", "alpha"], result.Template.JobDescriptionFields.Select(static f => f.Key));
    }

    /// <summary>Keys starting with an underscore are reserved and never reach the job description.</summary>
    [Fact]
    public void Parse_UnderscoreKeys_AreExcluded()
    {
        const string text = "---\ntitle: 'Tester'\n_role: 'qa/tester@1.0.0'\n_packs: ['x@1']\nconsult_when: 'Always'\n---\nTest.";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.NotNull(result.Template);
        Assert.Equal(["consult_when"], result.Template.JobDescriptionFields.Select(static f => f.Key));
    }

    /// <summary>A file that carries a name is not a candidate.</summary>
    [Fact]
    public void Parse_NamePresent_ReportsProblemAndNoTemplate()
    {
        const string text = "---\nname: 'Jane'\ntitle: 'Developer'\n---\nBuild.";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.Null(result.Template);
        Assert.Equal(["A candidate has no name; remove the 'name' field. Hiring supplies the name."], result.Problems);
    }

    /// <summary>A candidate without a title is rejected.</summary>
    [Fact]
    public void Parse_TitleMissing_ReportsProblem()
    {
        const string text = "---\nconsult_when: 'Always'\n---\nBuild.";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.Null(result.Template);
        Assert.Equal(["The candidate has no 'title' field."], result.Problems);
    }

    /// <summary>A candidate with no body has no stance and is rejected.</summary>
    [Fact]
    public void Parse_NoStanceBody_ReportsProblem()
    {
        const string text = "---\ntitle: 'Developer'\n---\n   \n";

        CandidateParseResult result = CandidateTemplateParser.Parse(text);

        Assert.Null(result.Template);
        Assert.Equal(["The candidate has no stance: the text after the frontmatter is empty."], result.Problems);
    }

    /// <summary>Text with no frontmatter reports the missing title and, when the body is empty, the missing stance.</summary>
    [Fact]
    public void Parse_EmptyText_ReportsTitleAndStance()
    {
        CandidateParseResult result = CandidateTemplateParser.Parse(string.Empty);

        Assert.Null(result.Template);
        Assert.Equal(2, result.Problems.Count);
    }
}
