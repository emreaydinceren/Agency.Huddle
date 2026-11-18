using System.Text.Json.Nodes;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.Logging.Abstractions;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>
/// A document's history records whether an entry was written by a goldfish (goldfish spec §6.8): the history line carries an
/// optional <c>goldfish</c> flag, <c>log_change</c> sets it while the caller is possessed, and the byline words it.
/// </summary>
public sealed class DocHistoryGoldfishTests
{
    /// <summary>An entry written as a goldfish reads back with the flag set, alongside an ordinary entry that reads back without it.</summary>
    [Fact]
    public void Append_GoldfishEntry_RoundTripsTheFlag()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        DocHistoryEntry ordinary = At(9, 0, "Ruth", DocChangeKind.Edited, "Fixed F1");
        DocHistoryEntry goldfish = At(9, 5, "Ruth", DocChangeKind.Reviewed, "Checked F2") with { Goldfish = true };

        store.Append(file, ordinary);
        store.Append(file, goldfish);

        IReadOnlyList<DocHistoryEntry> read = store.Read(file);
        Assert.False(read[0].Goldfish);
        Assert.True(read[1].Goldfish);
        Assert.Equal([ordinary, goldfish], read);
    }

    /// <summary>A goldfish entry's line ends with <c>"goldfish":true</c>, and an ordinary entry's line has no such field.</summary>
    [Fact]
    public void Append_WritesGoldfishOnlyWhenTrue()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");

        store.Append(file, At(11, 2, "Ruth", DocChangeKind.Edited, "Downgraded F1") with { Goldfish = true });
        store.Append(file, At(11, 3, "Ruth", DocChangeKind.Edited, "Downgraded F2"));

        string[] lines = File.ReadAllLines(store.FileFor(file));
        Assert.Equal(
            """{"v":1,"at":"2026-10-18T11:02:00+00:00","actor":"Ruth","actorKind":"agent","kind":"edited","summary":"Downgraded F1","goldfish":true}""",
            lines[0]);
        Assert.Equal(
            """{"v":1,"at":"2026-10-18T11:03:00+00:00","actor":"Ruth","actorKind":"agent","kind":"edited","summary":"Downgraded F2"}""",
            lines[1]);
    }

    /// <summary>A line written before the flag existed has no <c>goldfish</c> field and reads as not a goldfish, with the same format version.</summary>
    [Fact]
    public void Read_LineWithoutGoldfish_ReadsFalse()
    {
        using TempDataDir dataDir = new();
        DocHistoryStore store = new(dataDir.Options(), NullLogger<DocHistoryStore>.Instance);
        LibraryPath file = Document("findings.md");
        store.Append(file, At(9, 0, "Mira", DocChangeKind.Created, "Draft"));
        File.AppendAllText(store.FileFor(file), """{"v":1,"at":"2026-10-18T10:00:00+00:00","actor":"Ruth","actorKind":"agent","kind":"reviewed","summary":"Old line"}""" + "\n");

        DocHistoryEntry old = store.Read(file)[1];

        Assert.Equal("Old line", old.Summary);
        Assert.False(old.Goldfish);
    }

    /// <summary><c>log_change</c> from a possessed Agent writes the entry with the goldfish flag set.</summary>
    [Fact]
    public async Task LogChangeTool_Possessed_WritesAGoldfishEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "# Findings");
        (LogChangeTool tool, DocHistoryStore store) = await BuildAsync(fixture, harness, possessed: true, ct);

        string result = await tool.InvokeAsync(Args(Path.Combine(vault, "findings.md")), ct);

        Assert.Equal("Logged.", result);
        DocHistoryEntry entry = Assert.Single(store.Read(fixture.Resolve(vault, "findings.md")));
        Assert.Equal(("Ruth", true), (entry.Actor, entry.Goldfish));
    }

    /// <summary><c>log_change</c> from an Agent that is not possessed writes an ordinary entry under the same Name.</summary>
    [Fact]
    public async Task LogChangeTool_NotPossessed_WritesAnOrdinaryEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(ct);
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "# Findings");
        (LogChangeTool tool, DocHistoryStore store) = await BuildAsync(fixture, harness, possessed: false, ct);

        _ = await tool.InvokeAsync(Args(Path.Combine(vault, "findings.md")), ct);

        DocHistoryEntry entry = Assert.Single(store.Read(fixture.Resolve(vault, "findings.md")));
        Assert.Equal(("Ruth", false), (entry.Actor, entry.Goldfish));
    }

    /// <summary>A goldfish review goes to its own byline part and not to "Reviewed by", so the Human sees that no warm Agent vouched for it.</summary>
    [Fact]
    public void Byline_GoldfishReview_IsItsOwnPart()
    {
        DocByline? byline = DocHistoryView.Byline(
        [
            At(9, 0, "Mira", DocChangeKind.Created, "Draft"),
            At(10, 0, "Ruth", DocChangeKind.Reviewed, "Checked") with { Goldfish = true },
        ]);

        Assert.NotNull(byline);
        Assert.Equal(["Ruth"], byline.GoldfishReviewedBy);
        Assert.Empty(byline.ReviewedBy);
    }

    /// <summary>A goldfish edit goes to its own byline part and not to "Edited by".</summary>
    [Fact]
    public void Byline_GoldfishEdit_IsItsOwnPart()
    {
        DocByline? byline = DocHistoryView.Byline(
        [
            At(9, 0, "Mira", DocChangeKind.Created, "Draft"),
            At(10, 0, "Ruth", DocChangeKind.Edited, "Fixed F1") with { Goldfish = true },
        ]);

        Assert.NotNull(byline);
        Assert.Equal(["Ruth"], byline.GoldfishEditedBy);
        Assert.Empty(byline.EditedBy);
    }

    /// <summary>The byline labels read "Goldfish review by" and "Goldfish edit by" for a goldfish, and the ordinary labels otherwise.</summary>
    [Fact]
    public void BylineLabel_WordsGoldfishReviewAndEdit()
    {
        string[] labels =
        [
            DocHistoryView.BylineLabel(DocChangeKind.Reviewed, goldfish: true),
            DocHistoryView.BylineLabel(DocChangeKind.Edited, goldfish: true),
            DocHistoryView.BylineLabel(DocChangeKind.Reviewed, goldfish: false),
            DocHistoryView.BylineLabel(DocChangeKind.Edited, goldfish: false),
        ];

        Assert.Equal(["Goldfish review by", "Goldfish edit by", "Reviewed by", "Edited by"], labels);
    }

    /// <summary>A <c>log_change</c> tool for Ruth, whose Turn in one Room is possessed or not.</summary>
    private static async Task<(LogChangeTool Tool, DocHistoryStore Store)> BuildAsync(
        LibraryFileServiceFixture fixture, TaskToolHarness harness, bool possessed, CancellationToken ct)
    {
        string ruthId = (await harness.Directory.UpsertAgentUserAsync("Ruth", null, ct))?.Id ?? throw new InvalidOperationException("No Ruth.");
        harness.TurnActivity.Begin(ruthId, "room-1");
        GoldfishGuard guard = new(new FixedPossession(possessed ? "p1" : null), harness.TurnActivity, new FakePromptSource());
        DocHistoryStore store = new(fixture.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, fixture.BoundOptions, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"));
        LogChangeTool tool = new(
            recorder, fixture.Resolver, fixture.BoundOptions, new AgentPaths(fixture.BoundOptions), "Ruth", new FakePromptSource(),
            goldfish: guard, directory: harness.Directory);
        return (tool, store);
    }

    /// <summary>The arguments of one review call.</summary>
    private static JsonObject Args(string path) => new() { ["path"] = path, ["kind"] = "reviewed", ["summary"] = "Checked every citation" };

    /// <summary>A possession lookup that answers the same id for every Agent and Room.</summary>
    private sealed class FixedPossession(string? possessionId) : IPossessionLookup
    {
        /// <inheritdoc />
        public string? PossessionOf(string agentId, string roomId) => possessionId;
    }
}
