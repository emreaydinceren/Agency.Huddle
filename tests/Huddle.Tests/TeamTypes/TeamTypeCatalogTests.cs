using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="TeamTypeCatalog"/>: built-in and user loading, replacement, problems and path safety (TT-1 to TT-6).</summary>
public sealed class TeamTypeCatalogTests
{
    private const string Candidate = """
        ---
        title: Developer
        consult_when: code changes
        ---
        You build things.
        """;

    private static string Manifest(string id, string name, int order, string candidate = "candidates/lead.md", string process = "processes/flow.md", bool manager = true)
    {
        string managerText = manager ? "true" : "false";
        string json = """
            { "schemaVersion": 1, "id": "@ID", "version": "1.0.0", "displayName": "@NAME", "order": @ORDER,
              "workspace": { "kind": "none", "label": "None", "required": false },
              "processes": [{ "id": "flow", "file": "@PROCESS", "default": true }],
              "roles": [{ "id": "lead", "title": "Lead", "candidate": "@CANDIDATE", "manager": @MANAGER, "required": true }] }
            """;
        return json
            .Replace("@ID", id, StringComparison.Ordinal)
            .Replace("@NAME", name, StringComparison.Ordinal)
            .Replace("@ORDER", order.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("@PROCESS", process.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("@CANDIDATE", candidate.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace("@MANAGER", managerText, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Builtins()
    {
        return new(StringComparer.Ordinal)
        {
            ["alpha/team-type.json"] = Manifest("alpha", "Alpha", 20),
            ["alpha/candidates/lead.md"] = Candidate,
            ["alpha/processes/flow.md"] = "# Built-in flow",
            ["beta/team-type.json"] = Manifest("beta", "Beta", 10),
            ["beta/candidates/lead.md"] = Candidate,
            ["beta/processes/flow.md"] = "# Beta flow",
        };
    }

    private static TeamTypeCatalog Create(TempDataDir dir, Dictionary<string, string>? builtins = null)
    {
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins ?? Builtins());
    }

    private static void WriteUser(TempDataDir dir, string relativePath, string text)
    {
        string path = Path.Combine(dir.Path, "TeamTypes", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dir.Path);
        File.WriteAllText(path, text);
    }

    /// <summary>Valid types are listed ordered by Order, then display name.</summary>
    [Fact]
    public void Types_Builtins_OrderedByOrderThenName()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal(["beta", "alpha"], catalog.Types.Select(t => t.Id));
        Assert.Empty(catalog.Problems);
    }

    /// <summary>Equal orders fall back to the display name.</summary>
    [Fact]
    public void Types_EqualOrder_OrderedByDisplayName()
    {
        using TempDataDir dir = new();
        Dictionary<string, string> builtins = Builtins();
        builtins["beta/team-type.json"] = Manifest("beta", "Zed", 20);
        TeamTypeCatalog catalog = Create(dir, builtins);

        Assert.Equal(["alpha", "beta"], catalog.Types.Select(t => t.Id));
    }

    /// <summary>A user folder with the same id replaces the built-in manifest (TT-3).</summary>
    [Fact]
    public void Types_UserSameId_ReplacesBuiltin()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "alpha/team-type.json", Manifest("alpha", "My Alpha", 5));
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal("My Alpha", catalog.Find("alpha")?.DisplayName);
        Assert.Equal(2, catalog.Types.Count);
    }

    /// <summary>A user folder adding one candidate file extends the built-in type without copying it.</summary>
    [Fact]
    public void ReadCandidate_UserFileExtendsBuiltin_ResolvesUserFirstThenBuiltin()
    {
        using TempDataDir dir = new();
        string extended = Manifest("alpha", "Alpha", 20).Replace("\"roles\": [", "\"roles\": [{ \"id\": \"extra\", \"title\": \"Extra\", \"candidate\": \"candidates/extra.md\" },", StringComparison.Ordinal);
        WriteUser(dir, "alpha/team-type.json", extended);
        WriteUser(dir, "alpha/candidates/extra.md", Candidate.Replace("Developer", "Extra", StringComparison.Ordinal));
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal("Extra", catalog.ReadCandidate("alpha", "extra")?.Title);
        Assert.Equal("Developer", catalog.ReadCandidate("alpha", "lead")?.Title);
        Assert.Empty(catalog.Problems);
    }

    /// <summary>A user process file overrides the built-in one; otherwise the built-in text is returned.</summary>
    [Fact]
    public void ReadProcess_UserFirstThenBuiltin()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "alpha/processes/flow.md", "# User flow");
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal("# User flow", catalog.ReadProcess("alpha", "flow"));
        Assert.Equal("# Beta flow", catalog.ReadProcess("beta", "flow"));
        Assert.Null(catalog.ReadProcess("beta", "nope"));
        Assert.Null(catalog.ReadProcess("nope", "flow"));
    }

    /// <summary>An invalid user manifest is excluded from Types and listed in Problems with a reason.</summary>
    [Fact]
    public void Problems_InvalidManifest_ListedWithReason()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "gamma/team-type.json", Manifest("gamma", "Gamma", 1, manager: false));
        WriteUser(dir, "gamma/candidates/lead.md", Candidate);
        WriteUser(dir, "gamma/processes/flow.md", "# g");
        TeamTypeCatalog catalog = Create(dir);

        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("gamma", problem.TypeId);
        Assert.Equal("The type must declare exactly one manager role; none does.", problem.Message);
        Assert.Null(catalog.Find("gamma"));
        Assert.Equal(2, catalog.Types.Count);
    }

    /// <summary>Malformed JSON is a problem, never an exception.</summary>
    [Fact]
    public void Problems_MalformedJson_Listed()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "broken/team-type.json", "{ not json");
        TeamTypeCatalog catalog = Create(dir);

        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("broken", problem.TypeId);
        Assert.Equal("The manifest is not valid team-type JSON.", problem.Message);
    }

    /// <summary>A manifest missing its required fields is a problem, not an exception.</summary>
    [Fact]
    public void Problems_MissingRequiredFields_Listed()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "empty/team-type.json", "{}");
        TeamTypeCatalog catalog = Create(dir);

        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("The manifest lacks id, version, displayName or workspace.", problem.Message);
    }

    /// <summary>A manifest that cannot be opened is a problem, not an exception.</summary>
    [Fact]
    public void Problems_UnreadableManifest_Listed()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "locked/team-type.json", Manifest("locked", "Locked", 1));
        using FileStream hold = new(Path.Combine(dir.Path, "TeamTypes", "locked", "team-type.json"), FileMode.Open, FileAccess.Read, FileShare.None);
        TeamTypeCatalog catalog = Create(dir);

        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("The manifest could not be read.", problem.Message);
    }

    /// <summary>A user manifest whose id differs from its folder name is rejected.</summary>
    [Fact]
    public void Problems_FolderIdMismatch_Listed()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "delta/team-type.json", Manifest("other", "Other", 1));
        TeamTypeCatalog catalog = Create(dir);

        TeamTypeFileProblem problem = Assert.Single(catalog.Problems);
        Assert.Equal("The manifest id 'other' does not match its folder 'delta'.", problem.Message);
        Assert.Null(catalog.Find("other"));
    }

    /// <summary>When a user replacement of a built-in is invalid, the built-in stays and the problem is reported.</summary>
    [Fact]
    public void Types_InvalidUserReplacement_KeepsBuiltinAndReportsProblem()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "alpha/team-type.json", Manifest("alpha", "Bad", 1, manager: false));
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal("Alpha", catalog.Find("alpha")?.DisplayName);
        Assert.Single(catalog.Problems);
    }

    /// <summary>Candidate and process paths containing .. or rooted paths are rejected as problems and never read.</summary>
    /// <param name="path">The unsafe path value.</param>
    [Theory]
    [InlineData("../secret.md")]
    [InlineData("candidates/../../secret.md")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/secret.md")]
    [InlineData("C:\\secret.md")]
    public void Problems_PathTraversal_Rejected(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "secret.md"), Candidate);
        WriteUser(dir, "evil/team-type.json", Manifest("evil", "Evil", 1, candidate: path));
        WriteUser(dir, "evil2/team-type.json", Manifest("evil2", "Evil2", 1, process: path));
        WriteUser(dir, "evil2/candidates/lead.md", Candidate);
        TeamTypeCatalog catalog = Create(dir);

        Assert.Equal(2, catalog.Problems.Count);
        Assert.Null(catalog.Find("evil"));
        Assert.Null(catalog.Find("evil2"));
        Assert.Null(catalog.ReadCandidate("evil", "lead"));
        Assert.Null(catalog.ReadProcess("evil2", "flow"));
    }

    /// <summary>Find of an unknown id and ReadCandidate of an unknown role are null.</summary>
    [Fact]
    public void Find_Unknown_ReturnsNull()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = Create(dir);

        Assert.Null(catalog.Find("nope"));
        Assert.Null(catalog.ReadCandidate("alpha", "nope"));
        Assert.Null(catalog.ReadCandidate("nope", "lead"));
    }

    /// <summary>A candidate that does not parse makes ReadCandidate null.</summary>
    [Fact]
    public void ReadCandidate_Unparseable_ReturnsNull()
    {
        using TempDataDir dir = new();
        WriteUser(dir, "alpha/candidates/lead.md", "no frontmatter and no title");
        TeamTypeCatalog catalog = Create(dir);

        Assert.Null(catalog.ReadCandidate("alpha", "lead"));
    }

    /// <summary>Reload picks up a new user type and raises Changed.</summary>
    [Fact]
    public void Reload_NewUserType_AppearsAndRaisesChanged()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = Create(dir);
        int raised = 0;
        catalog.Changed += () => raised++;

        WriteUser(dir, "zeta/team-type.json", Manifest("zeta", "Zeta", 1));
        WriteUser(dir, "zeta/candidates/lead.md", Candidate);
        WriteUser(dir, "zeta/processes/flow.md", "# z");
        catalog.Reload();

        Assert.Equal(1, raised);
        Assert.NotNull(catalog.Find("zeta"));
    }
}
