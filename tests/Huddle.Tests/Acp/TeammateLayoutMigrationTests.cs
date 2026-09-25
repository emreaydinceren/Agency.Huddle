using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins <see cref="TeammateLayoutMigration"/>: the one-time, idempotent start-up step that moves an
/// old <c>{DataDir}/Teams/</c> + <c>{DataDir}/work/</c> layout into ADR-0031's
/// <c>{DataDir}/Teammates/&lt;Name&gt;/</c> sibling-folder layout, per Spec §6.15.
/// </summary>
public sealed class TeammateLayoutMigrationTests
{
    /// <summary>A flat definition directly under the old <c>Teams/</c> moves into its own Teammate folder, named from frontmatter.</summary>
    [Fact]
    public void Run_FlatDefinitions_MoveIntoTeammateFolders()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "wrong-filename.md", PersonaText("Nova"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        string target = Path.Combine(dataDir.Path, "Teammates", "Nova", "Nova.md");
        Assert.True(File.Exists(target));
        Assert.Equal(PersonaText("Nova"), File.ReadAllText(target));
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "Teams", "wrong-filename.md")));
    }

    /// <summary>A definition nested under an organisational sub-folder moves too, and the now-empty sub-folder is removed while <c>Teams/</c> itself survives.</summary>
    [Fact]
    public void Run_OrganisationalSubfolder_MovesAndRemovesEmptyFolder()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, Path.Combine("Marketing", "Ada.md"), PersonaText("Ada"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teammates", "Ada", "Ada.md")));
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "Teams", "Marketing")));
        Assert.True(Directory.Exists(Path.Combine(dataDir.Path, "Teams")));
    }

    /// <summary>A Work Dir under the old <c>work/</c> moves under its Teammate's folder, and <c>work/</c> is removed once empty.</summary>
    [Fact]
    public void Run_WorkDirs_MoveUnderTeammate()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        WriteWorkFile(dataDir.Path, Path.Combine("Nova", "memory", "x.md"), "hello");
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        string target = Path.Combine(dataDir.Path, "Teammates", "Nova", "work", "memory", "x.md");
        Assert.True(File.Exists(target));
        Assert.Equal("hello", File.ReadAllText(target));
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "work")));
    }

    /// <summary>A Work Dir with no definition still moves: the app never deletes a Work Dir.</summary>
    [Fact]
    public void Run_WorkDirWithoutDefinition_StillMoves()
    {
        using TempDataDir dataDir = new();
        WriteWorkFile(dataDir.Path, Path.Combine("Ghost", "a.md"), "ghost content");
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        string target = Path.Combine(dataDir.Path, "Teammates", "Ghost", "work", "a.md");
        Assert.True(File.Exists(target));
        Assert.Equal("ghost content", File.ReadAllText(target));
    }

    /// <summary>A <c>.md</c> file that is not a valid Persona moves to <c>Teammates/_unsorted/</c>, keeping its relative path.</summary>
    [Fact]
    public void Run_InvalidPersonaFile_GoesToUnsorted()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, Path.Combine("sub", "readme.md"), "Just notes, no frontmatter here.");
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        string target = Path.Combine(dataDir.Path, "Teammates", "_unsorted", "sub", "readme.md");
        Assert.True(File.Exists(target));
        Assert.Equal("Just notes, no frontmatter here.", File.ReadAllText(target));
    }

    /// <summary>An organisational folder that still holds a non-Persona file is not removed, and the file stays put.</summary>
    [Fact]
    public void Run_FolderStillHoldingFiles_IsKept()
    {
        using TempDataDir dataDir = new();
        string keepPath = Path.Combine(dataDir.Path, "Teams", "Marketing", "keep.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(keepPath)!);
        File.WriteAllText(keepPath, "do not touch");
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        Assert.True(File.Exists(keepPath));
        Assert.True(Directory.Exists(Path.Combine(dataDir.Path, "Teams", "Marketing")));
    }

    /// <summary>Running the migration a second time changes nothing and logs that there was nothing to migrate.</summary>
    [Fact]
    public void Run_SecondRun_IsNoOp()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        RecordingLogger<TeammateLayoutMigrationTests> firstLogger = new();
        TeammateLayoutMigration.Run(dataDir.Options(), firstLogger);
        string target = Path.Combine(dataDir.Path, "Teammates", "Nova", "Nova.md");
        string beforeSecondRun = File.ReadAllText(target);
        RecordingLogger<TeammateLayoutMigrationTests> secondLogger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), secondLogger);

        Assert.Equal(beforeSecondRun, File.ReadAllText(target));
        Assert.Contains(secondLogger.Entries, entry => entry.Message.Contains("nothing to migrate", StringComparison.Ordinal));
    }

    /// <summary>When only <c>Teammates/</c> already exists (no <c>Teams/</c>, no <c>work/</c>), the migration makes no change.</summary>
    [Fact]
    public void Run_NewLayoutAlreadyPresent_IsNoOp()
    {
        using TempDataDir dataDir = new();
        string existing = Path.Combine(dataDir.Path, "Teammates", "Nova", "Nova.md");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, PersonaText("Nova"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        Assert.Equal(PersonaText("Nova"), File.ReadAllText(existing));
    }

    /// <summary>When a move cannot complete (the target is blocked), the migration throws naming the path and leaves the old layout readable.</summary>
    [Fact]
    public void Run_MoveFails_ThrowsAndLeavesOldLayoutReadable()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        string blocker = Path.Combine(dataDir.Path, "Teammates", "Nova");
        Directory.CreateDirectory(Path.Combine(dataDir.Path, "Teammates"));
        File.WriteAllText(blocker, "this is a file, not a folder");
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => TeammateLayoutMigration.Run(dataDir.Options(), logger));

        Assert.Contains("Nova", exception.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teams", "Nova.md")));
    }

    /// <summary>The migration logs one entry per item it moves.</summary>
    [Fact]
    public void Run_LogsEveryMove()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        WriteTeamsFile(dataDir.Path, "Ada.md", PersonaText("Ada"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        string novaSource = Path.Combine(dataDir.Path, "Teams", "Nova.md");
        string novaTarget = Path.Combine(dataDir.Path, "Teammates", "Nova", "Nova.md");
        string adaSource = Path.Combine(dataDir.Path, "Teams", "Ada.md");
        string adaTarget = Path.Combine(dataDir.Path, "Teammates", "Ada", "Ada.md");
        Assert.Equal(2, logger.Entries.Count);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains(novaSource, StringComparison.Ordinal) && entry.Message.Contains(novaTarget, StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains(adaSource, StringComparison.Ordinal) && entry.Message.Contains(adaTarget, StringComparison.Ordinal));
    }

    /// <summary>Two files sharing a Name are both rejected by <see cref="PersonaIndex.Build(IReadOnlyList{ValueTuple{string, string}})"/> and both go to <c>_unsorted/</c>.</summary>
    [Fact]
    public void Run_DuplicateNames_BothGoToUnsorted()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "a.md", PersonaText("Nova"));
        WriteTeamsFile(dataDir.Path, "b.md", PersonaText("Nova"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teammates", "_unsorted", "a.md")));
        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teammates", "_unsorted", "b.md")));
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "Teammates", "Nova")));
    }

    /// <summary>Once <c>Teammates/</c> already holds a folder, the migration never scans <c>Teams/</c> for Personas again, so a Persona-like note left there is untouched.</summary>
    [Fact]
    public void Run_TeammatesPopulated_LeavesPersonaLikeNoteInTeams()
    {
        using TempDataDir dataDir = new();
        string existing = Path.Combine(dataDir.Path, "Teammates", "Existing", "Existing.md");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, PersonaText("Existing"));
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(dataDir.Options(), logger);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teams", "Nova.md")));
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "Teammates", "Nova")));
    }

    /// <summary>A missing <c>DataDir</c> is a no-op: the migration does not create it.</summary>
    [Fact]
    public void Run_MissingDataDir_IsNoOp()
    {
        string missingDataDir = Path.Combine(Path.GetTempPath(), "team-tests", Guid.NewGuid().ToString("N"));
        IOptions<TeamOptions> options = Options.Create(new TeamOptions { DataDir = missingDataDir });
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(options, logger);

        Assert.False(Directory.Exists(missingDataDir));
    }

    /// <summary>The migration's source is always the literal <c>Teams/</c> folder, never the configured <c>Team:Teams:Dir</c>.</summary>
    [Fact]
    public void Run_TeamsDirOverride_DoesNotChangeSource()
    {
        using TempDataDir dataDir = new();
        WriteTeamsFile(dataDir.Path, "Nova.md", PersonaText("Nova"));
        IOptions<TeamOptions> options = Options.Create(new TeamOptions
        {
            DataDir = dataDir.Path,
            Teams = new TeamsOptions { Dir = "CustomTeams" },
        });
        RecordingLogger<TeammateLayoutMigrationTests> logger = new();

        TeammateLayoutMigration.Run(options, logger);

        Assert.True(File.Exists(Path.Combine(dataDir.Path, "Teammates", "Nova", "Nova.md")));
        Assert.False(Directory.Exists(Path.Combine(dataDir.Path, "CustomTeams")));
    }

    /// <summary>Writes a file under the old <c>{DataDir}/Teams/</c> layout, creating parent folders as needed.</summary>
    private static void WriteTeamsFile(string dataDir, string relativePath, string text)
    {
        string path = Path.Combine(dataDir, "Teams", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>Writes a file under the old <c>{DataDir}/work/</c> layout, creating parent folders as needed.</summary>
    private static void WriteWorkFile(string dataDir, string relativePath, string text)
    {
        string path = Path.Combine(dataDir, "work", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A minimal, valid Persona file's raw text: only the required identity fields.</summary>
    private static string PersonaText(string name) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\nYou are {name}.";

    /// <summary>
    /// A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has no
    /// mocking framework. Copied from <c>PersonaRenameCascadeTests.RecordingLogger</c> (no shared fake
    /// logger exists).
    /// </summary>
    /// <typeparam name="T">The category type the recorded logger stands in for.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /// <summary>Every call made to this logger so far, in call order.</summary>
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        /// <summary>Not used by this fake: scoping is irrelevant to the tests that need it, so this returns a no-op.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>A no-op <see cref="IDisposable"/>.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Always enabled, so every call this fake receives is actually recorded.</summary>
        /// <param name="logLevel">The level being checked.</param>
        /// <returns><see langword="true"/>, always.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Records one log call's level and formatted message.</summary>
        /// <typeparam name="TState">The state type carrying this call's structured values.</typeparam>
        /// <param name="logLevel">The call's severity.</param>
        /// <param name="eventId">Unused by this fake.</param>
        /// <param name="state">The call's structured state, passed to <paramref name="formatter"/>.</param>
        /// <param name="exception">The call's exception, if any.</param>
        /// <param name="formatter">Formats <paramref name="state"/> and <paramref name="exception"/> into the message text.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            this.Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
