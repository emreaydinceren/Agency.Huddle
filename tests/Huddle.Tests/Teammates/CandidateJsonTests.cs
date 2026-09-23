using System.Text.Json.Nodes;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Pins <see cref="CandidateJson.TryParse"/> against the Spec §7.2 Candidate table and the Spec
/// §8.3 order-1 shape checks: required fields, forbidden fields, and field types. Every problem
/// is a model-facing string, so this also pins their exact wording.
/// </summary>
public sealed class CandidateJsonTests
{
    /// <summary>A JSON object carrying every Candidate field parses into a matching <see cref="Candidate"/>.</summary>
    [Fact]
    public void TryParse_ValidObject_ReturnsCandidate()
    {
        JsonNode? node = JsonNode.Parse(
            """
            {"name":"Vera","alias":"vee","title":"Researcher","body":"You research things.","teams":["research"],"consult_when":"needs data"}
            """);
        List<string> problems = [];

        bool result = CandidateJson.TryParse(node, 1, out Candidate? candidate, problems);

        Assert.True(result);
        Assert.Empty(problems);
        Assert.NotNull(candidate);
        Assert.Equal("Vera", candidate.Name);
        Assert.Equal("vee", candidate.Alias);
        Assert.Equal("Researcher", candidate.Title);
        Assert.Equal("You research things.", candidate.Body);
        Assert.Equal(["research"], candidate.Teams);
        Assert.Equal("needs data", candidate.ConsultWhen);
    }

    /// <summary>A Candidate missing the required 'alias' field is reported by field name and 1-based index.</summary>
    [Fact]
    public void TryParse_MissingAlias_ReportsProblem()
    {
        JsonNode? node = JsonNode.Parse("""{"name":"Vera","title":"Researcher","body":"You research things."}""");
        List<string> problems = [];

        bool result = CandidateJson.TryParse(node, 1, out Candidate? candidate, problems);

        Assert.False(result);
        Assert.Null(candidate);
        Assert.Contains("Candidate 1 is missing 'alias'.", problems);
    }

    /// <summary>
    /// Each field a Candidate may never set (Spec D-5: Model, Effort, Adapter, Skills are the
    /// Human's decision on the Teammate card) is reported with the same wording, naming the field.
    /// </summary>
    [Theory]
    [InlineData("model")]
    [InlineData("effort")]
    [InlineData("adapter")]
    [InlineData("skills")]
    [InlineData("_builtin")]
    public void TryParse_ForbiddenField_ReportsProblem(string field)
    {
        JsonObject node = new()
        {
            ["name"] = "Vera",
            ["alias"] = "vee",
            ["title"] = "Researcher",
            ["body"] = "You research things.",
            [field] = "x",
        };
        List<string> problems = [];

        bool result = CandidateJson.TryParse(node, 1, out Candidate? candidate, problems);

        Assert.False(result);
        Assert.Null(candidate);
        Assert.Contains($"A Candidate cannot set '{field}'; the Human sets it on the Teammate card.", problems);
    }

    /// <summary>A 'teams' field that is not a JSON array is a shape problem, not a silent coercion.</summary>
    [Fact]
    public void TryParse_TeamsNotArray_ReportsProblem()
    {
        JsonObject node = new()
        {
            ["name"] = "Vera",
            ["alias"] = "vee",
            ["title"] = "Researcher",
            ["body"] = "You research things.",
            ["teams"] = "research",
        };
        List<string> problems = [];

        bool result = CandidateJson.TryParse(node, 1, out Candidate? candidate, problems);

        Assert.False(result);
        Assert.Null(candidate);
        Assert.NotEmpty(problems);
    }

    /// <summary>An unrecognised field is reported by name and index rather than silently ignored.</summary>
    [Fact]
    public void TryParse_UnknownField_ReportsProblem()
    {
        JsonObject node = new()
        {
            ["name"] = "Vera",
            ["alias"] = "vee",
            ["title"] = "Researcher",
            ["body"] = "You research things.",
            ["color"] = "blue",
        };
        List<string> problems = [];

        bool result = CandidateJson.TryParse(node, 1, out Candidate? candidate, problems);

        Assert.False(result);
        Assert.Null(candidate);
        Assert.Contains("Candidate 1 has an unknown field 'color'.", problems);
    }
}
