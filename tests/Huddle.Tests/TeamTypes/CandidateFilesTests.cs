using System.Reflection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the seven built-in Development candidate files (TT-7, OR-4, PL-1 to PL-5, RQ-10): each parses as a candidate, carries its job description, and states the stance the plan requires.</summary>
public sealed class CandidateFilesTests
{
    private const string CandidateFolder = "TeamTypes/Builtin/development/candidates/";

    private static readonly string[] RoleIds =
    [
        "development-manager",
        "tech-lead",
        "developer",
        "reviewer",
        "security-engineer",
        "qa-engineer",
        "release-engineer",
    ];

    /// <summary>Reads one embedded candidate file by its role id.</summary>
    /// <param name="roleId">The role id, such as <c>tech-lead</c>.</param>
    /// <returns>The raw file text.</returns>
    private static string ReadCandidate(string roleId)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string expected = CandidateFolder + roleId + ".md";
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == expected);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Parses one embedded candidate file and asserts that the parser accepts it.</summary>
    /// <param name="roleId">The role id, such as <c>tech-lead</c>.</param>
    /// <returns>The parsed template.</returns>
    private static CandidateTemplate ParseCandidate(string roleId)
    {
        CandidateParseResult result = CandidateTemplateParser.Parse(ReadCandidate(roleId));
        Assert.Empty(result.Problems);
        Assert.NotNull(result.Template);
        return result.Template;
    }

    /// <summary>Every role's candidate file parses with the candidate parser and has no problems.</summary>
    [Fact]
    public void Candidates_AllSevenRoles_ParseWithoutProblems()
    {
        foreach (string roleId in RoleIds)
        {
            CandidateTemplate template = ParseCandidate(roleId);

            Assert.False(string.IsNullOrWhiteSpace(template.Title), $"Role '{roleId}' has no title.");
        }
    }

    /// <summary>Every role's candidate declares a one-line <c>consult_when</c> in its job description.</summary>
    [Fact]
    public void Candidates_AllSevenRoles_HaveConsultWhen()
    {
        foreach (string roleId in RoleIds)
        {
            CandidateTemplate template = ParseCandidate(roleId);

            PersonaFrontmatterField consultWhen = Assert.Single(template.JobDescriptionFields, static f => f.Key == "consult_when");
            Assert.False(string.IsNullOrWhiteSpace(consultWhen.Value), $"Role '{roleId}' has an empty consult_when.");
            Assert.DoesNotContain("\n", consultWhen.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>Every role's stance is substantial: at least 400 characters of body text.</summary>
    [Fact]
    public void Candidates_AllSevenRoles_BodyIsAtLeast400Characters()
    {
        foreach (string roleId in RoleIds)
        {
            CandidateTemplate template = ParseCandidate(roleId);

            Assert.True(template.Stance.Length >= 400, $"Role '{roleId}' stance is {template.Stance.Length} characters.");
        }
    }

    /// <summary>The Development Manager's stance names the tools for reading colleagues, assigning work and proposing sprints.</summary>
    [Fact]
    public void DevelopmentManager_Stance_NamesListAgentsUpdateTaskAndProposeSprint()
    {
        CandidateTemplate template = ParseCandidate("development-manager");

        // contains-ok: the stance is prose, not markup; each tool name must be spelled out in it so the Teammate knows the tool.
        Assert.Contains("list_agents", template.Stance, StringComparison.Ordinal);
        // contains-ok: the stance is prose, not markup; each tool name must be spelled out in it so the Teammate knows the tool.
        Assert.Contains("update_task", template.Stance, StringComparison.Ordinal);
        // contains-ok: the stance is prose, not markup; each tool name must be spelled out in it so the Teammate knows the tool.
        Assert.Contains("propose_sprint", template.Stance, StringComparison.Ordinal);
    }

    /// <summary>The Tech Lead's stance orders child Tasks with <c>blocked_by</c>.</summary>
    [Fact]
    public void TechLead_Stance_NamesBlockedBy()
    {
        CandidateTemplate template = ParseCandidate("tech-lead");

        // contains-ok: the stance is prose, not markup; the blocked_by tool argument must be spelled out in it.
        Assert.Contains("blocked_by", template.Stance, StringComparison.Ordinal);
    }

    /// <summary>The Reviewer and the Security Engineer state that they are read-only in their stance.</summary>
    [Fact]
    public void ReviewerAndSecurityEngineer_Stance_SaysReadOnly()
    {
        CandidateTemplate reviewer = ParseCandidate("reviewer");
        CandidateTemplate security = ParseCandidate("security-engineer");

        // contains-ok: the stance is prose, not markup; the read-only statement must appear in it.
        Assert.Contains("read-only", reviewer.Stance, StringComparison.OrdinalIgnoreCase);
        // contains-ok: the stance is prose, not markup; the read-only statement must appear in it.
        Assert.Contains("read-only", security.Stance, StringComparison.OrdinalIgnoreCase);
    }
}
