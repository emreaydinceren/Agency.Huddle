using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.AgentsMdChanges;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.AgentsMdChanges;

/// <summary>
/// Pins <see cref="AgentsMdLedger.Observe"/> (AgentsMdChanges Spec §8.1, §7.1, §6.2): a first sight records one revision
/// from the absent hash, a save only moves the pending marker, and the ledger file survives a new instance. The ledger
/// is given Helper factories that throw, so any attempt to start a judgement fails the test.
/// </summary>
public sealed class AgentsMdLedgerObserveTests : IDisposable
{
    private const string Relative = "Home/AGENTS.md";

    private readonly TempDataDir dir = new();
    private readonly RecordingLogger<AgentsMdLedger> logger = new();
    private int writes;

    private string TeamsRoot => Path.Combine(this.dir.Path, "Teams");

    private string LedgerDir => Path.Combine(this.dir.Path, "agents-md-ledger");

    /// <inheritdoc />
    public void Dispose() => this.dir.Dispose();

    /// <summary>The one place the ledger is built, so its constructor can grow without touching the tests.</summary>
    private AgentsMdLedger NewLedger()
    {
        Microsoft.Extensions.Options.IOptions<TeamOptions> options = this.dir.Options();
        HelperSettingsResolver settings = new(new HelperSettingsStore(options), options, NullLogger<HelperSettingsResolver>.Instance);
        HelperBudget budget = new(new HelperUsageStore(options));
        return new AgentsMdLedger(
            static () => throw new InvalidOperationException("The dispatcher must not be reached by Observe."),
            static () => throw new InvalidOperationException("The Helper must not be reached by Observe."),
            settings,
            budget,
            options,
            this.logger);
    }

    /// <summary>Describes an <c>AGENTS.md</c> at a relative path below the Teams root.</summary>
    /// <param name="relativePath">The path with <c>/</c> separators.</param>
    private AgentsMdFile FileAt(string relativePath = Relative) =>
        new(AgentsMdLevel.Team, "Home", null, Path.Combine(this.TeamsRoot, relativePath), relativePath);

    /// <summary>Writes the file and gives it a distinct, explicit modification time.</summary>
    /// <param name="file">The file to write.</param>
    /// <param name="text">The new text.</param>
    private void Save(AgentsMdFile file, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file.FullPath) ?? this.TeamsRoot);
        File.WriteAllText(file.FullPath, text);
        this.writes++;
        File.SetLastWriteTimeUtc(file.FullPath, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).AddMinutes(this.writes));
    }

    /// <summary>The hash the ledger uses for a text.</summary>
    /// <param name="text">The file text.</param>
    private static string HashOf(string text) => AgentsMdBody.Parse(text).Hash;

    /// <summary>The text of the single ledger file on disk.</summary>
    private string LedgerJson() => File.ReadAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")));

    /// <summary>A file the ledger has never seen records a first sight: one Comparison revision from the absent hash, and no Helper call.</summary>
    [Fact]
    public void Observe_FirstSight_OneRevisionFromAbsent_NoEnqueue()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "## Rules\n\nLead with the number.\n");
        AgentsMdLedger ledger = this.NewLedger();

        _ = ledger.Observe(file);

        AgentsMdRevision revision = Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        Assert.Equal(string.Empty, revision.FromHash);
        Assert.Equal(HashOf("## Rules\n\nLead with the number.\n"), revision.ToHash);
        Assert.IsType<AgentsMdNotice.Comparison>(revision.Notice);
        Assert.Equal(revision.ToHash, ledger.FirstSightHash(Relative));
        Assert.Empty(ledger.PendingEdits());
    }

    /// <summary>Observing the same text again changes nothing: no second revision and no pending edit.</summary>
    [Fact]
    public void Observe_SameHash_NothingChanges()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Same text.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);

        _ = ledger.Observe(file);

        Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        Assert.Empty(ledger.PendingEdits());
    }

    /// <summary>Changed text marks a pending edit with the new hash; it adds no revision and stores no text.</summary>
    [Fact]
    public void Observe_NewText_MarksPending_NoRevision_NoTextStored()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head text.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);
        this.Save(file, "Unsettled-only-marker.\n");

        _ = ledger.Observe(file);

        AgentsMdPendingEdit pending = Assert.Single(ledger.PendingEdits());
        Assert.Equal(HashOf("Unsettled-only-marker.\n"), pending.Hash);
        Assert.Equal(Relative, pending.File.RelativePath);
        Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        string json = this.LedgerJson();
        Assert.DoesNotContain("Unsettled-only-marker", json, StringComparison.Ordinal);
        // contains-ok: the ledger's JSON text, not markup; the test shows only the head's text is stored.
        Assert.Contains("Head text.", json, StringComparison.Ordinal);
    }

    /// <summary>However many saves land, the head stays put and the pending marker follows the latest text.</summary>
    [Fact]
    public void Observe_ManySaves_HeadStaysPut()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "First.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);
        string firstHash = HashOf("First.\n");

        foreach (string text in new[] { "Second.\n", "Third.\n", "Fourth.\n" })
        {
            this.Save(file, text);
            _ = ledger.Observe(file);
        }

        Assert.Equal(firstHash, ledger.FirstSightHash(Relative));
        IReadOnlyList<AgentsMdRevision>? after = ledger.Since(Relative, firstHash);
        Assert.NotNull(after);
        Assert.Empty(after);
        Assert.Equal(HashOf("Fourth.\n"), Assert.Single(ledger.PendingEdits()).Hash);
    }

    /// <summary>Saving back the head's text clears the pending edit.</summary>
    [Fact]
    public void Observe_RevertToHead_ClearsPending()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);
        this.Save(file, "Changed.\n");
        _ = ledger.Observe(file);
        Assert.Single(ledger.PendingEdits());
        this.Save(file, "Head.\n");

        _ = ledger.Observe(file);

        Assert.Empty(ledger.PendingEdits());
    }

    /// <summary>A file that cannot be read changes nothing: no pending edit and no new entry.</summary>
    [Fact]
    public void Observe_ReadFails_ChangesNothing()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);
        File.Delete(file.FullPath);
        Directory.CreateDirectory(file.FullPath);

        _ = ledger.Observe(file);

        Assert.Empty(ledger.PendingEdits());
        Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        Assert.Equal(HashOf("Head.\n"), ledger.FirstSightHash(Relative));
    }

    /// <summary>A new instance over the same data directory finds the head, the chain and the pending edit.</summary>
    [Fact]
    public void Ledger_SurvivesANewInstance()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        AgentsMdLedger first = this.NewLedger();
        _ = first.Observe(file);
        this.Save(file, "Changed.\n");
        _ = first.Observe(file);

        AgentsMdLedger second = this.NewLedger();
        _ = second.Observe(file);

        Assert.Equal(HashOf("Head.\n"), second.FirstSightHash(Relative));
        Assert.Single(second.Since(Relative, string.Empty) ?? []);
        Assert.Equal(HashOf("Changed.\n"), Assert.Single(second.PendingEdits()).Hash);
    }

    /// <summary>A ledger file that is not JSON is logged at Warning and treated as missing: the next Observe is a first sight.</summary>
    [Fact]
    public void Ledger_CorruptFile_IsAFirstSight_AndWarns()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        _ = this.NewLedger().Observe(file);
        File.WriteAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")), "{ this is not json");
        this.Save(file, "Newer.\n");

        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);

        Assert.Equal(HashOf("Newer.\n"), ledger.FirstSightHash(Relative));
        Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        Assert.Single(this.logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>JSON that parses but has the wrong shape is corrupt too: a first sight and a Warning, never a throw.</summary>
    /// <param name="body">The ledger file's text.</param>
    [Theory]
    [InlineData("""{"revisions":null}""")]
    [InlineData("""{"relativePath":"Home/AGENTS.md","firstSightHash":"AB","headHash":"AB","revisions":[{"fromHash":"","toHash":"AB","notice":null}]}""")]
    [InlineData("{}")]
    public void Ledger_ValidJsonWrongShape_IsAFirstSight_AndWarns(string body)
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        _ = this.NewLedger().Observe(file);
        File.WriteAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")), body);
        this.Save(file, "Newer.\n");

        AgentsMdLedger ledger = this.NewLedger();
        AgentsMdObservation observation = ledger.Observe(file);

        Assert.True(observation.FirstSight);
        Assert.Equal(HashOf("Newer.\n"), ledger.FirstSightHash(Relative));
        Assert.Single(ledger.Since(Relative, string.Empty) ?? []);
        Assert.Single(this.logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>E-14: after a corrupt ledger a Room behind the lost chain takes the unknown path, and the next edit is only marked pending: no call.</summary>
    [Fact]
    public void Ledger_Corrupt_NextEdit_TakesTheUnknownPath_NoCall()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        _ = this.NewLedger().Observe(file);
        File.WriteAllText(Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json")), """{"revisions":null}""");
        this.Save(file, "Newer.\n");
        AgentsMdLedger ledger = this.NewLedger();
        _ = ledger.Observe(file);
        this.Save(file, "Newest.\n");

        _ = ledger.Observe(file);

        Assert.Null(ledger.Since(Relative, HashOf("Head.\n")));
        Assert.Equal(HashOf("Newest.\n"), Assert.Single(ledger.PendingEdits()).Hash);
    }

    /// <summary>A ledger file that is locked is not corrupt: the Observe changes nothing, the file is left alone, and the chain is found once the lock lifts.</summary>
    [Fact]
    public void Ledger_LockedFile_DoesNotOverwriteTheChain()
    {
        AgentsMdFile file = this.FileAt();
        this.Save(file, "Head.\n");
        AgentsMdLedger first = this.NewLedger();
        _ = first.Observe(file);
        this.Save(file, "Changed.\n");
        _ = first.Observe(file);
        string ledgerFile = Assert.Single(Directory.GetFiles(this.LedgerDir, "*.json"));
        byte[] before = File.ReadAllBytes(ledgerFile);
        AgentsMdLedger second = this.NewLedger();

        AgentsMdObservation locked;
        using (FileStream held = new(ledgerFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.NotNull(held);
            locked = second.Observe(file);
        }

        Assert.False(locked.FirstSight);
        Assert.Equal(before, File.ReadAllBytes(ledgerFile));
        Assert.NotEmpty(this.logger.AtExactly(LogLevel.Warning));
        AgentsMdObservation later = second.Observe(file);
        Assert.False(later.FirstSight);
        Assert.Equal(HashOf("Head.\n"), second.FirstSightHash(Relative));
    }

    /// <summary>Two paths that differ only in case share one ledger file where <see cref="PathContainment.Comparison"/> ignores case, and have one each where it does not.</summary>
    [Fact]
    public void Key_FoldsCaseOnlyWherePathsDo()
    {
        AgentsMdFile upper = this.FileAt("Home/AGENTS.md");
        AgentsMdFile lower = this.FileAt("home/AGENTS.md");
        this.Save(upper, "Text.\n");
        this.Save(lower, "Text.\n");
        AgentsMdLedger ledger = this.NewLedger();

        _ = ledger.Observe(upper);
        _ = ledger.Observe(lower);

        bool foldsCase = PathContainment.Comparison == StringComparison.OrdinalIgnoreCase;
        string[] files = Directory.GetFiles(this.LedgerDir, "*.json");
        Assert.Equal(foldsCase ? 1 : 2, files.Length);
        Assert.All(files, static path => Assert.Equal(32, Path.GetFileNameWithoutExtension(path).Length));
    }
}
