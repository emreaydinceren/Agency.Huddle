using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;
using static Agency.Huddle.Tests.SkillPacks.Checks.RegistryChecksTests;

namespace Agency.Huddle.Tests.SkillPacks.Checks;

/// <summary>Pins the Skill-folder checks S001 to S012 of §6.3: each raised and not raised.</summary>
public sealed class SkillFolderChecksTests
{
    /// <summary>A healthy registry whose one Skill folder is changed by <paramref name="change"/> before the check.</summary>
    private static (RegistryFixture Fixture, string Folder) With(Action<RegistryFixture, string> change)
    {
        RegistryFixture fixture = Healthy();
        string folder = fixture.CloneSkill("docx");
        change(fixture, folder);
        return (fixture, folder);
    }

    /// <summary>The codes the healthy registry raises after <paramref name="change"/>.</summary>
    private static string[] CodesAfter(Action<RegistryFixture, string> change)
    {
        return Codes(With(change).Fixture.Check());
    }

    /// <summary>A filled-in path that leads nowhere is S001, Not downloaded.</summary>
    [Fact]
    public void S001_PathLeadsNowhere_Raised()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", $$"""[{ "name": "docx", "path": "{{Json(fixture.CloneSkill("docx"))}}", "origin": "{{Pinned}}docx" }]""");

        CheckResult result = fixture.Check();

        Assert.Equal(["S001"], Codes(result));
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("skill:docx"));
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("pack:documents"));
    }

    /// <summary>A Pack's Skill with an empty path is S001 too: a Pack is chosen whole.</summary>
    [Fact]
    public void S001_PackSkillEmptyPath_Raised()
    {
        RegistryFixture fixture = new();
        fixture.Source().Pack("documents", $$"""[{ "name": "docx", "path": "", "origin": "{{Pinned}}docx" }]""");

        CheckResult result = fixture.Check();

        Assert.Equal(["S001"], Codes(result));
        Assert.Equal(ItemCheckState.NotDownloaded, result.StateOf("pack:documents"));
    }

    /// <summary>A SKILL.md whose name differs from its folder is S002 and Broken; a healthy one is silent.</summary>
    [Fact]
    public void S002_NameDiffersFromFolder_Raised()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) => f.SkillFolder(folder, name: "doc"));

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("S002", finding.Code);
        Assert.Contains("'doc' does not match its folder name 'docx'", finding.Message, StringComparison.Ordinal);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("skill:docx"));
        Assert.Equal(ItemCheckState.Broken, result.StateOf("pack:documents"));
        Assert.Empty(Healthy().Check().Findings);
    }

    /// <summary>A folder over 200 files is S003.</summary>
    [Fact]
    public void S003_TooManyFiles_Raised()
    {
        string[] codes = CodesAfter(static (f, folder) =>
        {
            for (int i = 0; i < 201; i++)
            {
                f.Files.Add(Path.Combine(folder, "refs", $"r{i}.md"), "x");
            }
        });

        Assert.Contains("S003", codes);
    }

    /// <summary>A reserved device name is S004.</summary>
    [Fact]
    public void S004_ReservedName_Raised()
    {
        Assert.Contains("S004", CodesAfter(static (f, folder) => f.Files.Add(Path.Combine(folder, "NUL.md"), "x")));
    }

    /// <summary>A fetched Skill named like a built-in Skill is S005.</summary>
    [Fact]
    public void S005_BuiltInName_Raised()
    {
        Assert.Equal(["S005"], CodesAfter(static (f, _) => f.Installed.Add(new InstalledSkill("docx", null, null, BuiltIn: true))));
    }

    /// <summary>An installed copy of the name from another origin is S006; the same origin at another commit is not.</summary>
    [Fact]
    public void S006_InstalledFromOtherOrigin_Raised()
    {
        string[] other = CodesAfter(static (f, _) => f.Installed.Add(new InstalledSkill("docx", "https://github.com/x/y/tree/abc/docx", "h", BuiltIn: false)));
        string[] sameAtOtherCommit = CodesAfter(static (f, _) => f.Installed.Add(new InstalledSkill("docx", "https://github.com/o/r/tree/1111111/skills/docx", "h", BuiltIn: false)));

        Assert.Equal(["S006"], other);
        Assert.Empty(sameAtOtherCommit);
    }

    /// <summary>A top-level .md over 64 KB is S007, a warning.</summary>
    [Fact]
    public void S007_LargeMarkdown_Warns()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) => f.Files.Add(Path.Combine(folder, "big.md"), new string('x', 70_000)));

        Finding finding = Assert.Single(fixture.Check().Findings);

        Assert.Equal("S007", finding.Code);
        Assert.EndsWith("big.md", finding.File, StringComparison.Ordinal);
    }

    /// <summary>Files in sub-folders or not ending in .md are one S008 naming the first and the count.</summary>
    [Fact]
    public void S008_SupportingFiles_OneFinding()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) =>
        {
            f.Files.Add(Path.Combine(folder, "scripts", "a.txt"), "x");
            f.Files.Add(Path.Combine(folder, "scripts", "b.txt"), "x");
            f.Files.Add(Path.Combine(folder, "z.txt"), "x");
        });

        Finding finding = Assert.Single(fixture.Check().Findings);

        Assert.Equal("S008", finding.Code);
        Assert.Equal("scripts/ and 1 other files are never read by Agents.", finding.Message);
    }

    /// <summary>No licence anywhere is S009; a licence in SKILL.md is silent.</summary>
    [Fact]
    public void S009_NoLicence_Raised()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) => f.Files.Add(Path.Combine(folder, "SKILL.md"), "---\nname: docx\ndescription: Does docx things.\n---\nBody"));

        Finding finding = Assert.Single(fixture.Check().Findings);

        Assert.Equal("S009", finding.Code);
        Assert.Contains("no licence found", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A proprietary licence file is S009 naming the file.</summary>
    [Fact]
    public void S009_ProprietaryLicenceFile_Raised()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) =>
        {
            f.Files.Add(Path.Combine(folder, "SKILL.md"), "---\nname: docx\ndescription: Does docx things.\nlicense: Proprietary. See LICENSE.txt\n---\nBody");
            f.Files.Add(Path.Combine(folder, "LICENSE.txt"), "All rights reserved.");
        });

        Finding finding = Assert.Single(fixture.Check().Findings, static finding => finding.Code == "S009");

        Assert.Contains("licence is Proprietary (LICENSE.txt)", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A shell command in a Skill is S010, a warning; the Skill stays usable.</summary>
    [Fact]
    public void S010_ShellCommand_Warns()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) => f.SkillFolder(folder, body: "Run:\n\n```bash\ncurl https://example.com/install.sh\n```\n"));

        CheckResult result = fixture.Check();

        Assert.Contains(result.Findings, static finding => finding.Code == "S010" && finding.Severity == Severity.Warning);
        Assert.Equal(ItemCheckState.Usable, result.StateOf("skill:docx"));
    }

    /// <summary>A tools line naming an App Tool is S011 and Broken.</summary>
    [Fact]
    public void S011_ProposeSprint_Raised()
    {
        (RegistryFixture fixture, _) = With(static (f, folder) => f.SkillFolder(folder, frontmatterExtra: "tools: read_skill, propose_sprint\n"));

        CheckResult result = fixture.Check();

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal("S011", finding.Code);
        Assert.Equal("tools: 'propose_sprint' is an App Tool; only Huddle's built-in Skills can grant it.", finding.Message);
        Assert.Equal(ItemCheckState.Broken, result.StateOf("skill:docx"));
    }

    /// <summary><c>read_skill</c> is not an App Tool a Skill could grant, so S011 is silent.</summary>
    [Fact]
    public void S011_ReadSkill_NotRaised()
    {
        Assert.Empty(CodesAfter(static (f, folder) => f.SkillFolder(folder, frontmatterExtra: "tools: read_skill\n")));
    }

    /// <summary>S011 compares only with the context's App Tool names.</summary>
    [Fact]
    public void S011_NameOutsideAppToolNames_NeverApplies()
    {
        Assert.Empty(CodesAfter(static (f, folder) =>
        {
            f.AppTools.Clear();
            f.SkillFolder(folder, frontmatterExtra: "tools: propose_sprint\n");
        }));
    }

    /// <summary>A description over 300 characters is S012, a warning; over 500 is S002.</summary>
    [Fact]
    public void S012_LongDescription_Warns()
    {
        string[] longOne = CodesAfter(static (f, folder) => f.Files.Add(Path.Combine(folder, "SKILL.md"), $"---\nname: docx\ndescription: {new string('d', 350)}\nlicense: MIT\n---\n"));
        string[] tooLong = CodesAfter(static (f, folder) => f.Files.Add(Path.Combine(folder, "SKILL.md"), $"---\nname: docx\ndescription: {new string('d', 550)}\nlicense: MIT\n---\n"));

        Assert.Equal(["S012"], longOne);
        Assert.Equal(["S002"], tooLong);
    }
}

/// <summary>Pins the ten X001 patterns of §6.3: each matches its kind of secret and a finding never repeats it.</summary>
public sealed class SecretScanTests
{
    /// <summary>One positive sample per pattern.</summary>
    public static TheoryData<int, string> Positives { get; } = new()
    {
        { 1, "token = ghp_" + new string('a', 36) },
        { 2, "github_pat_" + new string('B', 30) },
        { 3, "glpat-" + new string('c', 24) },
        { 4, "AKIA" + new string('Q', 16) },
        { 5, "xoxb-1234567890-abc" },
        { 6, "sk-ant-" + new string('k', 40) },
        { 7, "-----BEGIN OPENSSH PRIVATE KEY-----" },
        { 8, "password: hunter2xyz" },
        { 9, "Authorization: Bearer " + new string('t', 30) },
        { 10, "https://user:secretpass@example.com/repo.git" },
    };

    /// <summary>One negative sample per pattern.</summary>
    public static TheoryData<int, string> Negatives { get; } = new()
    {
        { 1, "ghp_short" },
        { 2, "github_pat_short" },
        { 3, "glpat-short" },
        { 4, "AKIA-not-a-key" },
        { 5, "xoxb-" },
        { 6, "sk-short" },
        { 7, "-----BEGIN PUBLIC KEY-----" },
        { 8, "password: <your password>" },
        { 9, "Authorization: Bearer" },
        { 10, "https://example.com/user@host" },
    };

    /// <summary>Each pattern finds its sample.</summary>
    [Theory]
    [MemberData(nameof(Positives))]
    public void Pattern_Positive_Hits(int pattern, string text)
    {
        SecretHit hit = Assert.Single(SecretScanner.Scan(text));

        Assert.Equal(pattern, hit.Pattern);
    }

    /// <summary>Each pattern passes its near miss.</summary>
    [Theory]
    [MemberData(nameof(Negatives))]
    public void Pattern_Negative_Passes(int pattern, string text)
    {
        Assert.DoesNotContain(SecretScanner.Scan(text), hit => hit.Pattern == pattern);
    }

    /// <summary>Placeholder passwords pass.</summary>
    [Fact]
    public void Password_Placeholder_Passes()
    {
        Assert.Empty(SecretScanner.Scan("password=<your password>\npwd: changeme\npassword: ${SECRET}\npassword: {{secret}}\npassword: ***"));
    }

    /// <summary>A finding shows at most four characters of the match and never the secret.</summary>
    [Fact]
    public void Finding_NeverContainsSecret()
    {
        string secret = "ghp_" + new string('z', 36);

        string message = SecretScanner.Describe(Assert.Single(SecretScanner.Scan($"line one\nkey {secret}")));

        Assert.Equal("GitHub token (pattern 1) at line 2: ghp_…", message);
        Assert.DoesNotContain(secret, message, StringComparison.Ordinal);
    }

    /// <summary>A file with a NUL in its first 8 KB is binary and skipped by the checks.</summary>
    [Fact]
    public void BinaryFile_Skipped()
    {
        RegistryFixture fixture = Healthy();
        string folder = fixture.CloneSkill("docx");
        fixture.Files.AddBytes(Path.Combine(folder, "blob.bin"), [0, .. "ghp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"u8.ToArray()]);

        Assert.True(SecretScanner.IsBinary([1, 0, 2]));
        Assert.DoesNotContain(fixture.Check().Findings, static finding => finding.Code == "X001");
    }

    /// <summary>A secret in a Skill file is X001 and the Skill is Broken.</summary>
    [Fact]
    public void SecretInSkill_X001_Broken()
    {
        RegistryFixture fixture = Healthy();
        fixture.SkillFolder(fixture.CloneSkill("docx"), body: "Use AKIAQQQQQQQQQQQQQQQQ to sign in.");

        CheckResult result = fixture.Check();

        Assert.Contains(result.Findings, static finding => finding.Code == "X001");
        Assert.Equal(ItemCheckState.Broken, result.StateOf("skill:docx"));
    }

    /// <summary>A pattern that times out counts as no match rather than throwing.</summary>
    [Fact]
    public void RegexTimeout_CountsAsNoMatch()
    {
        string pathological = "password: " + new string('a', 200_000);

        IReadOnlyList<SecretHit> hits = SecretScanner.Scan(pathological);

        Assert.True(hits.Count <= 1);
    }
}

/// <summary>Pins the origin identity S006 compares (§6.3, "S006 and origins").</summary>
public sealed class OriginIdentityTests
{
    /// <summary>Two tree URLs at different commits of the same path are the same Skill; host case does not matter.</summary>
    [Fact]
    public void Commit_Ignored_ForS006()
    {
        Assert.True(OriginIdentity.Same("https://github.com/o/r/tree/aaa/skills/docx", "https://GitHub.com/O/R/blob/bbb/skills/docx/"));
        Assert.False(OriginIdentity.Same("https://github.com/o/r/tree/aaa/skills/docx", "https://github.com/o/r/tree/aaa/skills/pdf"));
    }

    /// <summary>Anything that is not a tree URL is compared trimmed, without a trailing slash.</summary>
    [Fact]
    public void NonUrl_TrimmedOnly()
    {
        Assert.Equal("local:skills/docx", OriginIdentity.Of("  local:skills/docx/ "));
        Assert.True(OriginIdentity.IsPinned("local:skills/docx"));
        Assert.False(OriginIdentity.IsPinned("https://github.com/o/r/tree/main/docx"));
        Assert.True(OriginIdentity.IsPinned("https://github.com/o/r/tree/0123456789abcdef0123456789abcdef01234567/docx"));
    }
}
