using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teammates;

namespace Agency.Huddle.Tests.Teammates;

/// <summary>
/// Pins <see cref="BuiltinTeammateSeeder"/> against Spec §6.12 and §8.6, and use case U15: an
/// empty Persona library gets a Chief of Staff on startup, carrying the <c>chief-of-staff</c>
/// marker and the <c>team-building</c> Skill. Uses a real <see cref="PersonaStore"/> over a
/// <see cref="TempDataDir"/>, the same style <c>ProposalServiceTests</c> already uses for this
/// layer, rather than a mock.
/// </summary>
public sealed class BuiltinTeammateSeederTests
{
    /// <summary>
    /// Spec §6.12's internal flow: with no Persona file on disk, <c>StartAsync</c> writes the
    /// default Chief of Staff, and the resulting entry carries the default Name and Alias, the
    /// <c>_builtin: chief-of-staff</c> marker, and the <c>team-building</c> Skill.
    /// </summary>
    [Fact]
    public async Task Start_EmptyLibrary_WritesChiefOfStaffWithMarkerAndSkill()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        BuiltinTeammateSeeder seeder = new(personas, NullLogger<BuiltinTeammateSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos", entry.Alias);
        Assert.Equal("chief-of-staff", entry.Builtin);
        Assert.Equal(["team-building"], entry.Skills);
    }

    /// <summary>
    /// Spec §12 F-13: the marker is what the seeder looks for, never the Name. A Persona named
    /// "Alfred" that already carries <c>_builtin: chief-of-staff</c> satisfies the seeder, so
    /// <c>StartAsync</c> writes nothing and the library still holds exactly that one Persona.
    /// </summary>
    [Fact]
    public async Task Start_MarkerUnderOtherName_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Alfred", "Assistant", "alf", Teams: [], Builtin: BuiltinTeammate.ChiefOfStaffMarker),
            "You are Alfred, and you already carry the Chief of Staff marker.");
        BuiltinTeammateSeeder seeder = new(personas, NullLogger<BuiltinTeammateSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry entry = Assert.Single(personas.Entries);
        Assert.Equal("Alfred", entry.Name);
        Assert.False(File.Exists(Path.Combine(personas.TeamsDirectory, "Chief of Staff.md")));
    }

    /// <summary>
    /// Spec §12 F-14: a Persona already occupies the default Name "Chief of Staff" but carries no
    /// marker, so it does not satisfy the seeder's check. <c>StartAsync</c> must fall back to the
    /// next free (Name, Alias) pair from Spec §8.6's search and write "Chief of Staff 2" / "cos2"
    /// there, carrying the marker, rather than colliding with the file already on disk.
    /// </summary>
    [Fact]
    public async Task Start_NameTaken_WritesChiefOfStaff2WithAliasCos2()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Someone Else", "boss", Teams: []),
            "You hold the default Name, but not the marker.");
        BuiltinTeammateSeeder seeder = new(personas, NullLogger<BuiltinTeammateSeeder>.Instance);

        await seeder.StartAsync(ct);

        PersonaEntry? entry = personas.Entries.SingleOrDefault(e => string.Equals(e.Name, "Chief of Staff 2", StringComparison.Ordinal));
        Assert.NotNull(entry);
        Assert.Equal("cos2", entry.Alias);
        Assert.Equal("chief-of-staff", entry.Builtin);
    }

    /// <summary>
    /// Spec §6.12 Constraints: the seeder never reverts an edit. A Chief of Staff file that
    /// already carries the marker, but whose Title and Body a Human has since edited away from
    /// the shipped default, is left byte-for-byte untouched by <c>StartAsync</c> - only an
    /// absent marker ever triggers a write.
    /// </summary>
    [Fact]
    public async Task Start_EditedChiefOfStaff_NotReverted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Edited Title", "cos", Teams: [], Builtin: BuiltinTeammate.ChiefOfStaffMarker),
            "This Body was hand-edited and no longer matches the shipped default.");
        var path = personas.PathFor("Chief of Staff");
        var before = File.ReadAllText(path);
        BuiltinTeammateSeeder seeder = new(personas, NullLogger<BuiltinTeammateSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal(before, File.ReadAllText(path));
    }

    /// <summary>
    /// Spec §12 F-15: two Personas under different Names both carry the marker - an ordinary
    /// situation the seeder is satisfied by (one marked entry is enough), not a case it
    /// reconciles down to one. <c>StartAsync</c> writes nothing and the library still holds
    /// exactly those two.
    /// </summary>
    [Fact]
    public async Task Start_TwoFilesWithMarker_WritesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        personas.Add(
            new PersonaIdentity("Chief of Staff", "Chief of Staff", "cos", Teams: [], Builtin: BuiltinTeammate.ChiefOfStaffMarker),
            "The original.");
        personas.Add(
            new PersonaIdentity("Backup Chief of Staff", "Chief of Staff", "backup-cos", Teams: [], Builtin: BuiltinTeammate.ChiefOfStaffMarker),
            "A hand-made copy.");
        var countBefore = personas.Entries.Count;
        BuiltinTeammateSeeder seeder = new(personas, NullLogger<BuiltinTeammateSeeder>.Instance);

        await seeder.StartAsync(ct);

        Assert.Equal(countBefore, personas.Entries.Count);
    }

    /// <summary>
    /// Spec §8.7: a failed write logs an Error and lets startup continue - the app is usable
    /// without the Chief of Staff. Sabotages the Teams directory itself (replaced with a plain
    /// file) so that <see cref="PersonaStore.Add"/>'s own <c>Directory.CreateDirectory</c> step
    /// fails for whichever (Name, Alias) pair Spec §8.6's free-name search settles on - a genuine
    /// write failure, not a naming collision the search would simply route around.
    /// </summary>
    [Fact]
    public async Task Start_WriteFails_LogsAndDoesNotThrow()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TempDataDir dataDir = new();
        using PersonaStore personas = new(
            dataDir.Options(), new PersonaModelStore(dataDir.Options()), new PersonaEffortStore(dataDir.Options()), NullLogger<PersonaStore>.Instance);
        var teamsDir = personas.TeamsDirectory;
        Directory.Delete(teamsDir, recursive: true);
        File.WriteAllText(teamsDir, "A plain file occupying where the Teams directory should be.");
        RecordingLogger<BuiltinTeammateSeeder> logger = new();
        BuiltinTeammateSeeder seeder = new(personas, logger);

        var exception = await Record.ExceptionAsync(() => seeder.StartAsync(ct));

        Assert.Null(exception);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    /// <summary>
    /// A hand-written fake <see cref="ILogger{T}"/> that records every call, since this repo has
    /// no mocking framework. Modelled after <c>PromptStoreTests.RecordingLogger</c>.
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
