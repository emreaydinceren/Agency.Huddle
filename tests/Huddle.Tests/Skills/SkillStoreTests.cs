using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>
/// Pins Spec §6.2 (<see cref="SkillStore"/>): an install with an empty <c>{DataDir}</c> still
/// resolves every shipped Skill from <see cref="SkillCatalog"/> alone, and the constructor creates
/// the Skills directory it is about to watch rather than requiring it to pre-exist.
/// </summary>
public sealed class SkillStoreTests
{
    /// <summary>
    /// With nothing on disk, <c>team-building</c> resolves from <see cref="SkillCatalog"/> alone:
    /// its <see cref="Skill.Source"/> is <see cref="SkillSource.Default"/> and it has no on-disk
    /// <see cref="Skill.FolderPath"/> to point at.
    /// </summary>
    [Fact]
    public void All_EmptyDataDir_ReturnsTeamBuildingAsDefault()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get("team-building");

        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Default, skill.Source);
        Assert.Null(skill.FolderPath);
    }

    /// <summary>
    /// <see cref="FileSystemWatcher"/> throws when asked to watch a directory that does not exist
    /// yet, so the constructor must create <c>{DataDir}/Skills</c> itself before it starts watching,
    /// the same reasoning <c>{DataDir}/avatars/</c> is created eagerly (Spec §6.2, Implementation
    /// notes).
    /// </summary>
    [Fact]
    public void Constructor_MissingSkillsDir_CreatesIt()
    {
        using TempDataDir dataDir = new();
        string skillsDir = Path.Combine(dataDir.Path, "Skills");
        Assert.False(Directory.Exists(skillsDir));

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.True(Directory.Exists(skillsDir));
    }

    /// <summary>
    /// Overriding only <c>roles.md</c> on disk still classifies <c>team-building</c> as
    /// <see cref="SkillSource.Overridden"/> — the per-file overlay merges the disk folder's files
    /// into the defaults rather than replacing the whole Skill, so its other three files stay
    /// present too (Spec §6.2, per-file overlay).
    /// </summary>
    [Fact]
    public void Get_DiskOverridesRolesOnly_RolesFromDiskOthersDefault_SourceOverridden()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "team-building");
        Directory.CreateDirectory(skillFolder);
        File.WriteAllText(Path.Combine(skillFolder, "roles.md"), "# Roles\n\nOverridden roles text.\n");

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get("team-building");

        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Overridden, skill.Source);
        Assert.Equal(skillFolder, skill.FolderPath);
        List<string> expectedFiles = ["SKILL.md", "onboarding.md", "roles.md", "team-patterns.md"];
        Assert.Equal(expectedFiles.OrderBy(name => name, StringComparer.Ordinal), skill.Files.OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// A disk folder that names no shipped default resolves as a Skill the Human wrote
    /// (<see cref="SkillSource.Yours"/>), pointing at its own folder.
    /// </summary>
    [Fact]
    public void All_FolderWithNoDefault_IsYours()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "custom-skill");
        Directory.CreateDirectory(skillFolder);
        string skillMd = """
            ---
            name: custom-skill
            description: A Skill the Human wrote, with no shipped default of the same name.
            ---
            Body text.
            """;
        File.WriteAllText(Path.Combine(skillFolder, "SKILL.md"), skillMd);

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get("custom-skill");

        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Yours, skill.Source);
        Assert.Equal(skillFolder, skill.FolderPath);
    }

    /// <summary>
    /// Spec §8.1's last two lines: "Error on a Yours Skill → Skill omitted from the snapshot, Error
    /// kept in Issues." A folder with no <c>SKILL.md</c> and no shipped default of the same name
    /// fails validation with no default to fall back to, so it never appears in <see cref="SkillStore.All"/>.
    /// </summary>
    [Fact]
    public void All_InvalidYoursSkill_OmittedAndErrorInIssues()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "broken-skill");
        Directory.CreateDirectory(skillFolder);
        File.WriteAllText(Path.Combine(skillFolder, "roles.md"), "Some content, but no SKILL.md.");

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.Null(store.Get("broken-skill"));
        Assert.DoesNotContain(store.All, skill => skill.Name == "broken-skill");
        Assert.Contains(store.Issues, issue => issue.Skill == "broken-skill" && issue.Severity == SkillIssueSeverity.Error);
    }

    /// <summary>
    /// Spec §8.1's last two lines: "Error on an Overridden SKILL.md → fall back to the default
    /// SKILL.md, record the Error as a Warning." An override that fails validation must not simply
    /// disappear the way an invalid Yours Skill does — <c>team-building</c> still resolves, using the
    /// shipped default's <c>SKILL.md</c>, with the override's Error downgraded to a Warning.
    /// </summary>
    [Fact]
    public void Get_InvalidOverrideSkillMd_FallsBackToDefaultSkillMd_WarningInIssues()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "team-building");
        Directory.CreateDirectory(skillFolder);
        string invalidSkillMd = """
            ---
            name: team-building
            ---
            Body text with no description field.
            """;
        File.WriteAllText(Path.Combine(skillFolder, "SKILL.md"), invalidSkillMd);

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get("team-building");

        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Overridden, skill.Source);
        Assert.Contains(store.Issues, issue => issue.Skill == "team-building" && issue.Severity == SkillIssueSeverity.Warning);
    }

    /// <summary>
    /// A sub-folder inside a Skill folder is ignored for file resolution (Spec §6.2 Constraints:
    /// "Supporting files are flat") and reported as an Info Issue, rather than silently vanishing
    /// with no trace anywhere.
    /// </summary>
    [Fact]
    public void All_SubFolderInsideSkill_IgnoredWithInfo()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "team-building");
        string nestedFolder = Path.Combine(skillFolder, "nested");
        Directory.CreateDirectory(nestedFolder);
        File.WriteAllText(Path.Combine(nestedFolder, "note.md"), "Nested content.");

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get("team-building");

        Assert.NotNull(skill);
        Assert.DoesNotContain("note.md", skill.Files);
        Assert.Contains(store.Issues, issue => issue.Skill == "team-building" && issue.Severity == SkillIssueSeverity.Info);
    }

    /// <summary>
    /// Pins Spec §6.2 (Implementation notes) and Spec §12 F-7: <c>file</c> is looked up as a literal
    /// key in the Skill's resolved files and is never joined into a path, so a traversal segment, a
    /// drive-rooted or absolute path, or a name that merely fails to match by case or extension all
    /// come back <see langword="null"/> instead of ever touching the real filesystem.
    /// </summary>
    /// <param name="file">A file name that must not resolve against <c>team-building</c>.</param>
    [Theory]
    [InlineData("../../Teams/x.md")]
    [InlineData("..\\SKILL.md")]
    [InlineData("C:\\Windows\\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("SKILL")]
    [InlineData("roles.MD")]
    public void ReadFile_NameNotInFiles_ReturnsNull(string file)
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.Null(store.ReadFile("team-building", file));
    }

    /// <summary>A Skill name that resolves to no Skill at all returns <see langword="null"/> rather than throwing.</summary>
    [Fact]
    public void ReadFile_UnknownSkill_ReturnsNull()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.Null(store.ReadFile("no-such-skill", "SKILL.md"));
    }

    /// <summary>A known file name resolves, and a disk override's text wins over the shipped default.</summary>
    [Fact]
    public void ReadFile_KnownFile_ReturnsResolvedText()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "team-building");
        Directory.CreateDirectory(skillFolder);
        const string overriddenText = "# Roles\n\nOverridden roles text.\n";
        File.WriteAllText(Path.Combine(skillFolder, "roles.md"), overriddenText);

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.Equal(overriddenText, store.ReadFile("team-building", "roles.md"));
    }

    /// <summary>
    /// Pins Spec §6.2 (watching), Spec §9 (full rebuild, debounced) and Spec §12 F-6: a new Skill
    /// folder written to disk after the store has already started raises
    /// <see cref="SkillStore.SkillsChanged"/> once the watcher's debounce settles, and the new
    /// Skill is visible immediately afterwards.
    /// </summary>
    [Fact]
    public async Task SkillsChanged_NewSkillFolderWritten_RaisedAndSkillVisible()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);
        TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        store.SkillsChanged += () => changed.TrySetResult();

        string skillFolder = Path.Combine(dataDir.Path, "Skills", "notes");
        Directory.CreateDirectory(skillFolder);
        string skillMd = """
            ---
            name: notes
            description: A Skill the Human wrote after the store had already started watching.
            ---
            Body text.
            """;
        File.WriteAllText(Path.Combine(skillFolder, "SKILL.md"), skillMd);

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Skill? skill = store.Get("notes");
        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Yours, skill.Source);
    }

    /// <summary>
    /// Pins Spec §6.2 (RestoreDefault) and use case U10: restoring an Overridden Skill deletes its
    /// on-disk override folder and the shipped default text applies again immediately, for every
    /// file the override touched — not only <c>SKILL.md</c>.
    /// </summary>
    [Fact]
    public void RestoreDefault_OverriddenSkill_DeletesFolderAndSourceIsDefault()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "team-building");
        Directory.CreateDirectory(skillFolder);
        File.WriteAllText(Path.Combine(skillFolder, "roles.md"), "# Roles\n\nOverridden roles text.\n");

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        store.RestoreDefault("team-building");

        Assert.False(Directory.Exists(skillFolder));
        Skill? skill = store.Get("team-building");
        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Default, skill.Source);
        Assert.Null(skill.FolderPath);
        Assert.Equal(SkillCatalog.All["team-building"]["roles.md"], store.ReadFile("team-building", "roles.md"));
    }

    /// <summary>
    /// A Skill the Human wrote has no shipped default to fall back to, so <see cref="SkillStore.RestoreDefault(string)"/>
    /// must refuse rather than delete the Human's only copy — and it must not touch the folder before refusing.
    /// </summary>
    [Fact]
    public void RestoreDefault_YoursSkill_ThrowsInvalidOperation()
    {
        using TempDataDir dataDir = new();
        string skillFolder = Path.Combine(dataDir.Path, "Skills", "custom-skill");
        Directory.CreateDirectory(skillFolder);
        string skillMd = """
            ---
            name: custom-skill
            description: A Skill the Human wrote, with no shipped default of the same name.
            ---
            Body text.
            """;
        File.WriteAllText(Path.Combine(skillFolder, "SKILL.md"), skillMd);

        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Assert.Throws<InvalidOperationException>(() => store.RestoreDefault("custom-skill"));
        Assert.True(Directory.Exists(skillFolder));
    }

    /// <summary>A Skill already at its shipped default (no disk folder) has nothing to restore: a no-op, not a throw.</summary>
    [Fact]
    public void RestoreDefault_DefaultSkill_NoOp()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        store.RestoreDefault("team-building");

        Skill? skill = store.Get("team-building");
        Assert.NotNull(skill);
        Assert.Equal(SkillSource.Default, skill.Source);
        Assert.Null(skill.FolderPath);
    }

    /// <summary>
    /// Pins Spec §6.2 (Resolve) and Spec §12 F-1: known names resolve to Skills, an unknown name
    /// becomes a Warning instead of an error, and a name repeated in the list collapses to its
    /// first occurrence rather than appearing twice.
    /// </summary>
    [Fact]
    public void Resolve_KnownAndUnknown_SkillsInOrderAndWarningForUnknown()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        SkillResolution resolution = store.Resolve(["team-building", "nope", "team-building"]);

        Skill skill = Assert.Single(resolution.Skills);
        Assert.Equal("team-building", skill.Name);
        List<string> expectedWarnings = ["Skill 'nope' does not exist."];
        Assert.Equal(expectedWarnings, resolution.Warnings);
    }

    /// <summary>An empty input list resolves to no Skills and no Warnings.</summary>
    [Fact]
    public void Resolve_EmptyList_NoSkillsNoWarnings()
    {
        using TempDataDir dataDir = new();
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        SkillResolution resolution = store.Resolve([]);

        Assert.Empty(resolution.Skills);
        Assert.Empty(resolution.Warnings);
    }

    private const string ImportedName = "imported-skill";

    private static string WriteImportedSkill(TempDataDir dataDir, bool withSource)
    {
        string folder = Path.Combine(dataDir.Path, "Skills", ImportedName);
        Directory.CreateDirectory(Path.Combine(folder, "scripts"));
        Directory.CreateDirectory(Path.Combine(folder, "refs"));
        File.WriteAllText(
            Path.Combine(folder, "SKILL.md"),
            "---\nname: imported-skill\ndescription: An imported skill with scripts.\n---\n\n# Imported\n");
        File.WriteAllText(Path.Combine(folder, "scripts", "x.ps1"), "Write-Host 'hi'\n");
        File.WriteAllText(Path.Combine(folder, "refs", "a.md"), "# Reference A\n");
        if (withSource)
        {
            SkillProvenance provenance = new(
                "https://github.com/acme/skills",
                "skills/imported-skill",
                "0123456789abcdef0123456789abcdef01234567",
                SkillFolderHash.ComputeFolder(folder),
                "acme",
                "MIT",
                new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
            provenance.Write(folder);
        }

        return folder;
    }

    private static async Task EditAndWaitAsync(SkillStore store, string file, string text)
    {
        TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler()
        {
            changed.TrySetResult();
        }

        store.SkillsChanged += Handler;
        try
        {
            File.WriteAllText(file, text);
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
        finally
        {
            store.SkillsChanged -= Handler;
        }
    }

    /// <summary>A skill folder with a valid <c>.source.json</c> keeps its scripts and sub-folder files, and exposes its provenance.</summary>
    [Fact]
    public void Get_ImportedSkillWithSubfolders_IsAcceptedWithAllFiles()
    {
        using TempDataDir dataDir = new();
        WriteImportedSkill(dataDir, withSource: true);
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get(ImportedName);

        Assert.NotNull(skill);
        Assert.Equal(["SKILL.md", "refs/a.md", "scripts/x.ps1"], skill.Files);
        Assert.NotNull(skill.Provenance);
        Assert.Equal("acme", skill.Provenance.Publisher);
        Assert.False(skill.ModifiedLocally);
    }

    /// <summary>The same layout without <c>.source.json</c> keeps the flat <c>.md</c> rule: only <c>SKILL.md</c> loads and the sub-folders are reported.</summary>
    [Fact]
    public void Get_SameLayoutWithoutSource_StaysFlat()
    {
        using TempDataDir dataDir = new();
        WriteImportedSkill(dataDir, withSource: false);
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        Skill? skill = store.Get(ImportedName);

        Assert.NotNull(skill);
        Assert.Equal(["SKILL.md"], skill.Files);
        Assert.Null(skill.Provenance);
        Assert.False(skill.ModifiedLocally);
        Assert.Contains(
            new SkillIssue(
                ImportedName,
                "'scripts' is a sub-folder inside 'imported-skill'; Skill files are flat, so it and everything inside it are ignored.",
                SkillIssueSeverity.Info),
            store.Issues);
    }

    /// <summary>Editing an imported file flips <see cref="Skill.ModifiedLocally"/> on, and restoring the text flips it back off.</summary>
    [Fact]
    public async Task ModifiedLocally_EditThenRestore_FlipsAndFlipsBack()
    {
        using TempDataDir dataDir = new();
        string folder = WriteImportedSkill(dataDir, withSource: true);
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);
        string script = Path.Combine(folder, "scripts", "x.ps1");

        await EditAndWaitAsync(store, script, "Write-Host 'changed'\n");
        bool modified = store.Get(ImportedName)?.ModifiedLocally ?? false;
        await EditAndWaitAsync(store, script, "Write-Host 'hi'\n");
        bool restored = store.Get(ImportedName)?.ModifiedLocally ?? true;

        Assert.True(modified);
        Assert.False(restored);
    }

    /// <summary>A relative path with <c>/</c> separators reads a sub-folder file; a <c>..</c> path and an unknown path return null.</summary>
    [Fact]
    public void ReadFile_RelativePath_ReadsSubfolderFileAndRejectsTraversal()
    {
        using TempDataDir dataDir = new();
        WriteImportedSkill(dataDir, withSource: true);
        File.WriteAllText(Path.Combine(dataDir.Path, "Skills", "secret.md"), "secret");
        using SkillStore store = new(dataDir.Options(), NullLogger<SkillStore>.Instance);

        string? inside = store.ReadFile(ImportedName, "refs/a.md");
        string? script = store.ReadFile(ImportedName, "scripts/x.ps1");
        string? traversal = store.ReadFile(ImportedName, "../secret.md");
        string? nested = store.ReadFile(ImportedName, "refs/../../secret.md");

        Assert.Equal("# Reference A\n", inside);
        Assert.Equal("Write-Host 'hi'\n", script);
        Assert.Null(traversal);
        Assert.Null(nested);
    }

    /// <summary>A provenance-backed folder over 200 files is an Error issue; at 200 it is accepted.</summary>
    [Fact]
    public void Validate_ImportedFolderOver200Files_IsAnError()
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal)
        {
            ["SKILL.md"] = "---\nname: big\ndescription: Big.\n---\n",
        };
        for (int index = 0; index < 199; index++)
        {
            files[$"refs/f{index}.md"] = "x";
        }

        SkillValidation atLimit = SkillValidator.Validate("big", files, allowSupportingFiles: true);
        files["refs/one-more.md"] = "x";
        SkillValidation overLimit = SkillValidator.Validate("big", files, allowSupportingFiles: true);

        Assert.False(atLimit.HasErrors);
        Assert.Contains(overLimit.Issues, issue => issue.Severity == SkillIssueSeverity.Error);
        Assert.Equal("'big' has 201 files; an imported Skill may have no more than 200 files.", overLimit.Issues[0].Message);
    }

    /// <summary>A file the process may not read during a rebuild is handled like a sharing violation: the previous snapshot stays and nothing is thrown.</summary>
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void TryRebuildWithRetries_UnreadableFile_ReturnsNullWithoutThrowing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Needs a Windows ACL deny rule.");
        using TempDataDir dir = new();
        using SkillStore store = new(dir.Options(), NullLogger<SkillStore>.Instance);
        string folder = Path.Combine(store.SkillsDirectory, "mine");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "SKILL.md");
        File.WriteAllText(file, "---\nname: mine\ndescription: Mine.\n---\nBody\n");
        FileInfo info = new(file);
        System.Security.AccessControl.FileSecurity security = info.GetAccessControl();
        System.Security.Principal.SecurityIdentifier me = System.Security.Principal.WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("No current user SID.");
        System.Security.AccessControl.FileSystemAccessRule deny = new(me, System.Security.AccessControl.FileSystemRights.Read, System.Security.AccessControl.AccessControlType.Deny);
        security.AddAccessRule(deny);
        info.SetAccessControl(security);
        try
        {
            SkillStore.SkillSnapshot? snapshot = store.TryRebuildWithRetries();

            Assert.Null(snapshot);
        }
        finally
        {
            security.RemoveAccessRule(deny);
            info.SetAccessControl(security);
        }
    }
}