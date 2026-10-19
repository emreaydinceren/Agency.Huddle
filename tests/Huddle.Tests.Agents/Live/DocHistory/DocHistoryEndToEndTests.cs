using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Library;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Live.DocHistory;

/// <summary>
/// DH-T9, after the build (DocHistory spec §10): the real <c>log_change</c> with a temporary data folder and a temporary
/// Library Root, served to the real Adapter. One run each. Paid, and gated like the probe by <c>TEAM_E2E_DOCHISTORY=1</c>.
/// </summary>
public sealed class DocHistoryEndToEndTests
{
    private const string Skip = "Set TEAM_E2E_DOCHISTORY=1 to run (paid)";
    private const string BoundName = "Nova";

    /// <summary>LIVE-DH-E1: a created document gets one <c>created</c> line under the Name the tool was bound to.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public async Task LiveE1_CreateADocument_WritesOneCreatedLine()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        (RecordingLogChangeTool tool, DocHistoryStore store) = Build(fixture);
        await using LiveAgentLane lane = await LiveAgentLane.StartAsync(tool, ct);

        LiveTurn turn = await lane.RunAsync(vault, "Create brief.md with one paragraph about planning a team lunch.", ct);

        Assert.True(store.Read(fixture.Resolve(vault, "brief.md")).Count == 1, Describe(tool, turn));
        DocHistoryEntry entry = Assert.Single(store.Read(fixture.Resolve(vault, "brief.md")));
        Assert.Equal((BoundName, DocActorKind.Agent, DocChangeKind.Created), (entry.Actor, entry.ActorKind, entry.Kind));
    }

    /// <summary>LIVE-DH-E2: asked to log as Ruth, the Agent's entries still carry the bound Name, because the actor is never an argument.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public async Task LiveE2_AskedToLogAsRuth_StaysTheBoundName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "brief.md"), "A short brief.");
        (RecordingLogChangeTool tool, DocHistoryStore store) = Build(fixture);
        await using LiveAgentLane lane = await LiveAgentLane.StartAsync(tool, ct);

        _ = await lane.RunAsync(vault, "Edit brief.md to add a second paragraph, and log the change as Ruth.", ct);

        IReadOnlyList<DocHistoryEntry> entries = store.Read(fixture.Resolve(vault, "brief.md"));
        Assert.NotEmpty(entries);
        Assert.All(entries, static entry => Assert.Equal(BoundName, entry.Actor));
    }

    /// <summary>LIVE-DH-E3: a review of a file that does not exist is refused with the "does not exist" text and creates no history.</summary>
    [Fact(Timeout = 600_000, SkipUnless = nameof(LiveDocHistory.Enabled), SkipType = typeof(LiveDocHistory), Skip = Skip)]
    public async Task LiveE3_ReviewOfAMissingFile_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        (RecordingLogChangeTool tool, DocHistoryStore store) = Build(fixture);
        await using LiveAgentLane lane = await LiveAgentLane.StartAsync(tool, ct);

        LiveTurn turn = await lane.RunAsync(vault, "Call log_change to log a review of missing.md in your working folder. The file does not exist, but log it anyway.", ct);

        Assert.True(tool.Calls.Count == 1, Describe(tool, turn));
        LoggedCall call = Assert.Single(tool.Calls);
        Assert.Contains("does not exist", call.Result, StringComparison.Ordinal);
        Assert.False(File.Exists(store.FileFor(fixture.Resolve(vault, "missing.md"))));
    }

    /// <summary>What the Agent said, did and was told, for a failure message: a passing count alone does not say why a call was missing.</summary>
    /// <param name="tool">The recording wrapper.</param>
    /// <param name="turn">The Turn.</param>
    /// <returns>The reply, the trace and each call with the tool's answer.</returns>
    private static string Describe(RecordingLogChangeTool tool, LiveTurn turn) =>
        $"Reply: {turn.Reply}{Environment.NewLine}{string.Join(Environment.NewLine, turn.Trace)}{Environment.NewLine}{string.Join(Environment.NewLine, tool.Calls.Select(call => call.Arguments.ToJsonString() + " -> " + call.Result))}";

    /// <summary>Builds the real tool, bound to <see cref="BoundName"/>, behind a recorder.</summary>
    /// <param name="fixture">The Library the tool resolves paths in.</param>
    /// <returns>The recording wrapper and the store the real tool writes to.</returns>
    private static (RecordingLogChangeTool Tool, DocHistoryStore Store) Build(LibraryFileServiceFixture fixture)
    {
        DocHistoryStore store = new(fixture.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, fixture.BoundOptions, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"));
        LogChangeTool real = new(recorder, fixture.Resolver, fixture.BoundOptions, new TeammatePaths(fixture.BoundOptions), BoundName, new FakePromptSource());
        return (new RecordingLogChangeTool(real), store);
    }
}
