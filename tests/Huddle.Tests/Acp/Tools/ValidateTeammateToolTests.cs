using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Tools;

/// <summary>
/// Pins <see cref="ValidateTeammateTool"/> against Spec §5.3 Contract B's <c>validate_teammate</c>
/// row: a real <see cref="CandidateChecker"/> over real stores (no mock checker), returning either
/// the literal <c>"Valid."</c> or one line per problem, and never throwing for an expected failure
/// - a missing or malformed <c>candidate</c> argument included.
/// </summary>
public sealed class ValidateTeammateToolTests
{
    /// <summary>A Candidate with every required field valid, and nothing else in the way, checks clean.</summary>
    [Fact]
    public async Task InvokeAsync_Valid_ReturnsValid()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);
        var tool = new ValidateTeammateTool(fixture.Checker, new FakePromptSource());
        var arguments = new JsonObject { ["candidate"] = ValidCandidateJson() };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Valid.", result);
    }

    /// <summary>Two independent faults - an invalid Name and a blank Title - come back as exactly two lines, one per problem.</summary>
    [Fact]
    public async Task InvokeAsync_TwoProblems_ReturnsOneLinePerProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);
        var tool = new ValidateTeammateTool(fixture.Checker, new FakePromptSource());
        var candidate = ValidCandidateJson();
        candidate["name"] = "Vera.";
        candidate["title"] = "   ";
        var arguments = new JsonObject { ["candidate"] = candidate };

        var result = await tool.InvokeAsync(arguments, ct);

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    /// <summary>An absent <c>candidate</c> argument is an expected failure, reported as text, never thrown.</summary>
    [Fact]
    public async Task InvokeAsync_MissingCandidate_ReturnsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);
        var tool = new ValidateTeammateTool(fixture.Checker, new FakePromptSource());

        var result = await tool.InvokeAsync(new JsonObject(), ct);

        Assert.Contains("candidate", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A <c>candidate</c> argument that is not a JSON object is the same expected failure as a missing one, never a thrown exception.</summary>
    [Fact]
    public async Task InvokeAsync_CandidateNotAnObject_ReturnsProblem()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);
        var tool = new ValidateTeammateTool(fixture.Checker, new FakePromptSource());
        var arguments = new JsonObject { ["candidate"] = "not an object" };

        var result = await tool.InvokeAsync(arguments, ct);

        Assert.Contains("candidate", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The tool's wire name is exactly <c>validate_teammate</c> (Spec §5.3 Contract B).</summary>
    [Fact]
    public async Task Name_IsValidateTeammate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateFixtureAsync(ct);
        var tool = new ValidateTeammateTool(fixture.Checker, new FakePromptSource());

        Assert.Equal("validate_teammate", tool.Name);
    }

    /// <summary>A minimal Candidate JSON object that passes every check: a valid Name and Alias, a real Title and Body, no Teams.</summary>
    private static JsonObject ValidCandidateJson()
    {
        return new JsonObject
        {
            ["name"] = "Vera",
            ["alias"] = "vee",
            ["title"] = "Researcher",
            ["body"] = "You research things.",
        };
    }

    /// <summary>
    /// Builds a real <see cref="PersonaStore"/>, <see cref="SqliteTeamDirectory"/> and
    /// <see cref="CandidateChecker"/> in a fresh <see cref="TempDataDir"/> - the real collaborators
    /// this class's tests check <see cref="ValidateTeammateTool"/> against, per the coordinator's
    /// "no mock checker" instruction.
    /// </summary>
    private static async Task<Fixture> CreateFixtureAsync(CancellationToken ct)
    {
        var dataDir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dataDir.Options());
        await directory.InitializeAsync("You", ct);

        var personas = new PersonaStore(
            new TeammatePaths(dataDir.Options()), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        var gateway = new FakeAgentGateway();
        var checker = new CandidateChecker(personas, directory, gateway);

        return new Fixture(dataDir, personas, checker);
    }

    /// <summary>Bundles the real collaborators one <see cref="ValidateTeammateTool"/> under test needs, torn down together.</summary>
    private sealed class Fixture(TempDataDir dataDir, PersonaStore personas, CandidateChecker checker) : IDisposable
    {
        /// <summary>The real <see cref="CandidateChecker"/> the tool under test wraps.</summary>
        public CandidateChecker Checker { get; } = checker;

        /// <summary>Disposes the real <see cref="PersonaStore"/> and the underlying temp directory.</summary>
        public void Dispose()
        {
            personas.Dispose();
            dataDir.Dispose();
        }
    }
}
