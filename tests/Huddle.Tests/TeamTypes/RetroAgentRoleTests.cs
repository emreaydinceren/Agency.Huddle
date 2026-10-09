using System.Reflection;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.TeamTypes;

namespace Agency.Huddle.Tests.TeamTypes;

/// <summary>Tests for the optional Retro agent role in the built-in Development type (RT-1, RT-4, RT-5): the manifest declares it as the retro role, its candidate states the stance, and the retrospective skill carries the procedure.</summary>
public sealed class RetroAgentRoleTests
{
    private const string TypeFolder = "TeamTypes/Builtin/development/";
    private const string RoleId = "retro-agent";

    private static readonly string[] RequiredStanceNames = ["read_records", "propose_retro_changes"];

    private static readonly string[] RetroStages = ["Gather", "Ask", "Report", "Propose", "Register", "Verify"];

    /// <summary>Reads the embedded manifest and parses it with the one set of team-type JSON options.</summary>
    /// <returns>The parsed manifest.</returns>
    private static TeamTypeManifest LoadManifest()
    {
        TeamTypeManifest? manifest = System.Text.Json.JsonSerializer.Deserialize<TeamTypeManifest>(ReadEmbedded(TypeFolder + "team-type.json"), TeamTypeJson.Options);
        Assert.NotNull(manifest);
        return manifest;
    }

    /// <summary>Lists the embedded resource names of the Development type, relative to its folder, so validation sees the files that ship.</summary>
    /// <returns>The relative paths, such as <c>candidates/developer.md</c>.</returns>
    private static HashSet<string> EmbeddedTypeFiles()
    {
        HashSet<string> files = new(StringComparer.Ordinal);
        foreach (string name in typeof(TeamTypeCatalog).Assembly.GetManifestResourceNames())
        {
            string normalised = name.Replace('\\', '/');
            if (normalised.StartsWith(TypeFolder, StringComparison.Ordinal))
            {
                files.Add(normalised[TypeFolder.Length..]);
            }
        }

        return files;
    }

    /// <summary>Reads one embedded text resource by its forward-slash path.</summary>
    /// <param name="relativePath">The resource path, such as <c>TeamTypes/Builtin/development/team-type.json</c>.</param>
    /// <returns>The raw file text.</returns>
    private static string ReadEmbedded(string relativePath)
    {
        Assembly assembly = typeof(TeamTypeCatalog).Assembly;
        string? resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.Replace('\\', '/') == relativePath);
        Assert.NotNull(resource);
        using Stream? stream = assembly.GetManifestResourceStream(resource);
        Assert.NotNull(stream);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Parses the Retro agent candidate and asserts that the parser accepts it.</summary>
    /// <returns>The parsed candidate template.</returns>
    private static CandidateTemplate ParseRetroCandidate()
    {
        CandidateParseResult result = CandidateTemplateParser.Parse(ReadEmbedded(TypeFolder + "candidates/" + RoleId + ".md"));
        Assert.Empty(result.Problems);
        Assert.NotNull(result.Template);
        return result.Template;
    }

    /// <summary>Lists the words that do not occur in a text, so a failing assert names what is missing.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="words">The words that must occur.</param>
    /// <returns>The words that are absent, in the order given.</returns>
    private static string[] MissingFrom(string text, string[] words) => words.Where(word => !text.Contains(word, StringComparison.Ordinal)).ToArray();

    /// <summary>The manifest names the Retro agent as its retro role, so the retro runs through that hire (RT-1).</summary>
    [Fact]
    public void Manifest_RetroRole_NamesRetroAgent()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Equal(RoleId, manifest.RetroRole);
    }

    /// <summary>The Development manifest with the retro role still passes every validator check against the shipped files.</summary>
    [Fact]
    public void Manifest_ShippedFiles_ValidateWithNoProblems()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.Empty(TeamTypeValidator.Validate(manifest, EmbeddedTypeFiles().Contains));
    }

    /// <summary>A retro role that names no role of the type is reported as retro-role-unknown, and nothing else is.</summary>
    [Fact]
    public void Validate_UnknownRetroRole_ReportsRetroRoleUnknown()
    {
        TeamTypeManifest manifest = LoadManifest() with { RetroRole = "ghost" };

        IReadOnlyList<TeamTypeProblem> problems = TeamTypeValidator.Validate(manifest, EmbeddedTypeFiles().Contains);

        Assert.Equal(["retro-role-unknown"], problems.Select(static p => p.Code).ToList());
    }

    /// <summary>The Retro agent is an optional hire, one per Team, on the strongest tier, and reviews a different vendor than the development manager (RT-1).</summary>
    [Fact]
    public void RetroAgentRole_IsOptionalOnePerTeamStrongestAndDifferentVendorFromManager()
    {
        TeamTypeManifest manifest = LoadManifest();

        RoleSpec role = Assert.Single(manifest.Roles, static r => r.Id == RoleId);
        IReadOnlyList<string> vendors = role.Model?.DifferentVendorFrom ?? [];
        IReadOnlyList<SkillRef> skills = role.Skills ?? [];
        Assert.Equal("Retro Agent", role.Title);
        Assert.False(role.Required);
        Assert.Equal(1, role.MaxPerTeam);
        Assert.Equal(ModelTier.Strongest, role.Model?.Tier);
        Assert.Equal(["development-manager"], vendors);
        Assert.Equal([new SkillRef("retrospective", "builtin", "retrospective")], skills);
    }

    /// <summary>Neither preset hires the Retro agent: it is optional and added by the Human (RT-1).</summary>
    [Fact]
    public void RetroAgentRole_IsInNoPreset()
    {
        TeamTypeManifest manifest = LoadManifest();

        Assert.All(manifest.Presets, static p => Assert.DoesNotContain(RoleId, p.Roles));
    }

    /// <summary>The Retro agent candidate parses with no problems and carries the title Retro Agent.</summary>
    [Fact]
    public void Candidate_RetroAgent_ParsesWithTitle()
    {
        CandidateTemplate template = ParseRetroCandidate();

        Assert.Equal("Retro Agent", template.Title);
    }

    /// <summary>The Retro agent stance is between 150 and 300 words, the length the plan sets for a stance.</summary>
    [Fact]
    public void Candidate_RetroAgent_StanceIsBetween150And300Words()
    {
        char[] separators = [' ', '\r', '\n', '\t'];

        string[] words = ParseRetroCandidate().Stance.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        Assert.InRange(words.Length, 150, 300);
    }

    /// <summary>The Retro agent stance names read_records to gather evidence and propose_retro_changes to propose changes (RT-3, RT-5).</summary>
    [Fact]
    public void Candidate_RetroAgent_StanceNamesReadRecordsAndProposeRetroChanges()
    {
        string stance = ParseRetroCandidate().Stance;

        Assert.Empty(MissingFrom(stance, RequiredStanceNames));
    }

    /// <summary>The retrospective skill carries the six retro stages in its procedure: Gather, Ask, Report, Propose, Register and Verify (RT-4, RT-5, RT-6, RT-7).</summary>
    [Fact]
    public void Skill_Retrospective_CarriesTheRetroStages()
    {
        string text = SkillCatalog.All["retrospective"]["SKILL.md"];

        Assert.Empty(MissingFrom(text, RetroStages));
    }
}
