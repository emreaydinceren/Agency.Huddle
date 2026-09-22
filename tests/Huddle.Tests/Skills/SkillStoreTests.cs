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
}
