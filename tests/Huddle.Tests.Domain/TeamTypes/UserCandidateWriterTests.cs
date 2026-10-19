using System.Text.Json;
using Agency.Huddle.App.TeamTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for <see cref="UserCandidateWriter"/>: the user candidate file, the user manifest copy and the refusals (HR-11).</summary>
public sealed class UserCandidateWriterTests
{
    private const string BadRoleId = "The role id must be lower-case letters, digits and hyphens, and start with a letter or digit.";
    private const string UnknownType = "That team type does not exist.";
    private const string Duplicate = "A role with that id already exists in this team type.";
    private const string NoTitle = "The title is required.";
    private const string NoStance = "The stance is required.";
    private const string NotSaved = "The role could not be saved.";

    private static readonly string Manifest = Factories.TeamTypeJson.Manifest("alpha", "Alpha", order: 1, version: "1.0.0", rolesJson: """[{ "id": "lead", "title": "Lead", "candidate": "candidates/lead.md", "manager": true, "required": true }]""");

    private const string Candidate = """
        ---
        title: Lead
        consult_when: always
        ---
        You lead.
        """;

    private static TeamTypeCatalog CreateCatalog(TempDataDir dir)
    {
        Dictionary<string, string> builtins = new(StringComparer.Ordinal)
        {
            ["alpha/team-type.json"] = Manifest,
            ["alpha/candidates/lead.md"] = Candidate,
            ["alpha/processes/flow.md"] = "# Flow",
        };
        return new TeamTypeCatalog(dir.Options(), NullLogger<TeamTypeCatalog>.Instance, builtins);
    }

    /// <summary>A save writes the candidate file with title, consult-when and stance, and a user manifest with the new role.</summary>
    [Fact]
    public void Save_NewRole_WritesCandidateFileAndUserManifest()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = CreateCatalog(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult result = writer.Save("alpha", "researcher", "Researcher", "A question needs facts", "You find things out.");

        Assert.True(result.Success);
        Assert.Null(result.Error);
        string candidatePath = Path.Combine(dir.Path, "TeamTypes", "alpha", "candidates", "researcher.md");
        CandidateParseResult parsed = CandidateTemplateParser.Parse(File.ReadAllText(candidatePath));
        Assert.NotNull(parsed.Template);
        Assert.Equal("Researcher", parsed.Template.Title);
        Assert.Equal("You find things out.", parsed.Template.Stance);
        Assert.Equal("A question needs facts", Assert.Single(parsed.Template.JobDescriptionFields).Value);

        string manifestText = File.ReadAllText(Path.Combine(dir.Path, "TeamTypes", "alpha", "team-type.json"));
        TeamTypeManifest? manifest = JsonSerializer.Deserialize<TeamTypeManifest>(manifestText, TeamTypeJson.Options);
        Assert.NotNull(manifest);
        Assert.Equal(["lead", "researcher"], manifest.Roles.Select(static r => r.Id));
        RoleSpec added = manifest.Roles[1];
        Assert.Equal("Researcher", added.Title);
        Assert.Equal("candidates/researcher.md", added.Candidate);
        Assert.False(added.Required);
        Assert.False(added.Manager);
        Assert.Equal(1, added.MaxPerTeam);
        Assert.Equal(ModelTier.Balanced, added.Model.Tier);
    }

    /// <summary>After a save and a catalog reload the type still validates, is user-defined and lists and reads the new role.</summary>
    [Fact]
    public void Save_ThenReload_TypeValidatesAndCatalogListsNewRole()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = CreateCatalog(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult result = writer.Save("alpha", "researcher", "Researcher", "A question needs facts", "You find things out.");
        catalog.Reload();

        Assert.True(result.Success);
        Assert.Empty(catalog.Problems);
        Assert.True(catalog.IsUserDefined("alpha"));
        TeamTypeManifest? type = catalog.Find("alpha");
        Assert.NotNull(type);
        Assert.Equal(["lead", "researcher"], type.Roles.Select(static r => r.Id));
        Assert.Equal("flow", Assert.Single(type.Processes).Id);
        Assert.Equal("Lead", catalog.ReadCandidate("alpha", "lead")?.Title);
        Assert.Equal("You find things out.", catalog.ReadCandidate("alpha", "researcher")?.Stance);
    }

    /// <summary>A second save appends to the existing user manifest and keeps the first added role.</summary>
    [Fact]
    public void Save_SecondRole_AppendsToExistingUserManifest()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = CreateCatalog(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult first = writer.Save("alpha", "researcher", "Researcher", "when", "stance one");
        catalog.Reload();
        WriteResult second = writer.Save("alpha", "editor", "Editor", "when", "stance two");
        catalog.Reload();

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(["lead", "researcher", "editor"], catalog.Find("alpha")?.Roles.Select(static r => r.Id));
    }

    /// <summary>A role id that already exists in the type is refused and nothing is written.</summary>
    [Fact]
    public void Save_ExistingRoleId_RefusedAndNothingWritten()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = CreateCatalog(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult result = writer.Save("alpha", "lead", "Other Lead", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(Duplicate, result.Error);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "TeamTypes")));
    }

    /// <summary>A leftover candidate file for the role id counts as existing and is not overwritten.</summary>
    [Fact]
    public void Save_CandidateFileAlreadyOnDisk_RefusedAndFileKept()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "TeamTypes", "alpha", "candidates", "researcher.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? dir.Path);
        File.WriteAllText(path, "keep me");
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult result = writer.Save("alpha", "researcher", "Researcher", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(Duplicate, result.Error);
        Assert.Equal("keep me", File.ReadAllText(path));
    }

    /// <summary>A role id that is not a plain lower-case id, including path escapes, is refused and nothing is written anywhere.</summary>
    /// <param name="roleId">The rejected role id.</param>
    [Theory]
    [InlineData("../x")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("X")]
    [InlineData("-x")]
    [InlineData("")]
    public void Save_BadRoleId_RefusedAndNothingWritten(string roleId)
    {
        using TempDataDir dir = new();
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult result = writer.Save("alpha", roleId, "Title", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(BadRoleId, result.Error);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "TeamTypes")));
    }

    /// <summary>A type id that is unknown or tries to escape the TeamTypes folder is refused.</summary>
    /// <param name="typeId">The rejected type id.</param>
    [Theory]
    [InlineData("../x")]
    [InlineData("missing")]
    [InlineData("")]
    public void Save_UnknownOrEscapingTypeId_Refused(string typeId)
    {
        using TempDataDir dir = new();
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult result = writer.Save(typeId, "researcher", "Researcher", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(UnknownType, result.Error);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "TeamTypes")));
    }

    /// <summary>A blank title or stance is refused.</summary>
    [Fact]
    public void Save_BlankTitleOrStance_Refused()
    {
        using TempDataDir dir = new();
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult noTitle = writer.Save("alpha", "researcher", "  ", "when", "stance");
        WriteResult noStance = writer.Save("alpha", "researcher", "Researcher", "when", " ");

        Assert.Equal(NoTitle, noTitle.Error);
        Assert.Equal(NoStance, noStance.Error);
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "TeamTypes")));
    }

    /// <summary>Quotes and line breaks in the title and consult-when survive the round trip through the candidate file.</summary>
    [Fact]
    public void Save_QuotesAndLineBreaksInFields_RoundTrip()
    {
        using TempDataDir dir = new();
        TeamTypeCatalog catalog = CreateCatalog(dir);
        UserCandidateWriter writer = new(dir.Options(), catalog);

        WriteResult result = writer.Save("alpha", "researcher", "It's a \"Researcher\"", "a question\nneeds facts", "Line one.\n\nLine two.");
        catalog.Reload();

        Assert.True(result.Success);
        CandidateTemplate? template = catalog.ReadCandidate("alpha", "researcher");
        Assert.NotNull(template);
        Assert.Equal("It's a \"Researcher\"", template.Title);
        Assert.Equal("a question needs facts", Assert.Single(template.JobDescriptionFields).Value);
        Assert.Equal("Line one.\n\nLine two.", template.Stance.ReplaceLineEndings("\n"));
    }

    /// <summary>A folder that cannot be created yields a refusal, not an exception, and leaves no manifest.</summary>
    [Fact]
    public void Save_CandidatesPathIsAFile_ReturnsNotSaved()
    {
        using TempDataDir dir = new();
        string typeDir = Path.Combine(dir.Path, "TeamTypes", "alpha");
        Directory.CreateDirectory(typeDir);
        File.WriteAllText(Path.Combine(typeDir, "candidates"), "in the way");
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult result = writer.Save("alpha", "researcher", "Researcher", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(NotSaved, result.Error);
        Assert.False(File.Exists(Path.Combine(typeDir, "team-type.json")));
    }

    /// <summary>A failure while replacing the manifest removes the temporary manifest file as well as the candidate.</summary>
    [Fact]
    public void Save_ManifestReplaceFails_LeavesNoTempFile()
    {
        using TempDataDir dir = new();
        string typeDir = Path.Combine(dir.Path, "TeamTypes", "alpha");
        Directory.CreateDirectory(Path.Combine(typeDir, "team-type.json"));
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult result = writer.Save("alpha", "researcher", "Researcher", "when", "stance");

        Assert.False(result.Success);
        Assert.Equal(NotSaved, result.Error);
        Assert.False(File.Exists(Path.Combine(typeDir, "team-type.json.tmp")));
        Assert.False(File.Exists(Path.Combine(typeDir, "candidates", "researcher.md")));
    }

    /// <summary>Concurrent saves of different roles all complete without an error.</summary>
    [Fact]
    public async Task Save_ConcurrentDifferentRoles_AllSucceed()
    {
        using TempDataDir dir = new();
        UserCandidateWriter writer = new(dir.Options(), CreateCatalog(dir));

        WriteResult[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(
            () => writer.Save("alpha", "role-" + i, "Role " + i, "when", "stance"),
            TestContext.Current.CancellationToken)));

        Assert.All(results, static r => Assert.True(r.Success));
    }
}