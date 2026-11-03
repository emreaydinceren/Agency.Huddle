using Agency.Huddle.App;
using Agency.Huddle.App.SecondBrain;
using Agency.Huddle.Tests.Elicitation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.SecondBrain;

/// <summary>Pins the start-up scan for files the Second Brain layout retired (Spec §6.8, correction C7).</summary>
public sealed class RetiredFileScanTests : IDisposable
{
    private static readonly string[] Retired =
    [
        "Agents/Nova/work/Instructions.md",
        "Teams/Home/Process.md",
        "Teams/Instructions.md",
        "Uploads",
        "prompts.json",
    ];

    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "retired-scan-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the temp data folder.</summary>
    public RetiredFileScanTests() => Directory.CreateDirectory(this.dataDir);

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(this.dataDir))
        {
            Directory.Delete(this.dataDir, recursive: true);
        }
    }

    /// <summary>All five retired items are found, as relative paths with <c>/</c> separators in ordinal order.</summary>
    [Fact]
    public void Find_RetiredItems_ReturnsRelativePathsInOrdinalOrder()
    {
        this.SeedRetired();

        IReadOnlyList<string> found = RetiredFileScan.Find(this.dataDir);

        Assert.Equal(Retired, found);
    }

    /// <summary>Neighbouring files that are not retired are not reported.</summary>
    [Fact]
    public void Find_NeighbourFiles_AreNotReported()
    {
        this.Touch("Teams/Home/AGENTS.md");
        this.Touch("Teams/Home/notes.md");
        this.Touch("Agents/Nova/notes/Instructions.md");
        this.Touch("Agents/Nova/Nova.md");

        IReadOnlyList<string> found = RetiredFileScan.Find(this.dataDir);

        Assert.Empty(found);
    }

    /// <summary>A clean data folder yields nothing.</summary>
    [Fact]
    public void Find_CleanFolder_ReturnsNone()
    {
        IReadOnlyList<string> found = RetiredFileScan.Find(this.dataDir);

        Assert.Empty(found);
    }

    /// <summary>Starting the service logs the settled text (T9) once per retired path, at Warning.</summary>
    [Fact]
    public async Task StartAsync_RetiredItems_LogsOneWarningPerPath()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        this.SeedRetired();
        RecordingLogger<RetiredFileScanService> logger = new();
        RetiredFileScanService service = new(Options.Create(new TeamOptions { DataDir = this.dataDir }), logger);

        await service.StartAsync(ct);
        await service.StopAsync(ct);

        string[] expected =
        [
            .. Retired.Select(static path => path + " is no longer read by Huddle. The Second Brain layout replaced it; see Huddle.SecondBrain.md."),
        ];
        Assert.Equal(expected, logger.Messages);
        Assert.Equal(Retired.Length, logger.AtExactly(LogLevel.Warning).Count);
    }

    /// <summary>A clean folder logs nothing.</summary>
    [Fact]
    public async Task StartAsync_CleanFolder_LogsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        RecordingLogger<RetiredFileScanService> logger = new();
        RetiredFileScanService service = new(Options.Create(new TeamOptions { DataDir = this.dataDir }), logger);

        await service.StartAsync(ct);

        Assert.Empty(logger.Entries);
    }

    private void SeedRetired()
    {
        this.Touch("Teams/Home/Process.md");
        this.Touch("Teams/Instructions.md");
        this.Touch("Agents/Nova/work/Instructions.md");
        this.Touch("prompts.json");
        Directory.CreateDirectory(Path.Combine(this.dataDir, "Uploads"));
    }

    private void Touch(string relative)
    {
        string full = Path.Combine(this.dataDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full) ?? this.dataDir);
        File.WriteAllText(full, "x");
    }
}
