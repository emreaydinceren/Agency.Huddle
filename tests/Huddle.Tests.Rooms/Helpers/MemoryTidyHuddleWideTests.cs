using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Memory;
using Agency.Huddle.Tests.Teams;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using static Agency.Huddle.Tests.Helpers.MemoryTidyApplyHarness;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// Huddle-wide Memory in Memory Tidy (Memory Tidy spec §6.5, §6.14 and §8.1 "Huddle-wide"): the folder resolves, the pass judges it after the Agents with
/// its copies under <c>Huddle</c>, descriptions are locked, a merge is remembered for 30 days, and the review fixes on the checks and the Prompt.
/// </summary>
public sealed class MemoryTidyHuddleWideTests
{
    private const string Good = "Pricing rules, read before quoting a price";
    private const string PriceBody = "Price is 5 euro on 2026-10-01 at https://shop.example/price.";
    private const string ShipBody = "Shipping is free over 50, ask ann@example.com.";
    private const string MergedText = "Price 5 euro on 2026-10-01 at https://shop.example/price. Shipping free over 50, ask ann@example.com.";
    private const string LockedRefusal = "descriptions do not change in this folder";

    /// <summary>Huddle-wide Memory resolves to <c>Teams/memory</c>, labelled with the reserved name and marked Huddle-wide.</summary>
    [Fact]
    public void TryResolveHuddleWide_ResolvesTheReservedFolder()
    {
        using MemoryTidyApplyHarness harness = new(huddle: true);
        MemoryPaths paths = new(new AgentPaths(harness.Options), new TeamMemoryAccess(new FakeTeamCatalog(), static _ => false), harness.Options);

        bool resolved = paths.TryResolveHuddleWide(out MemoryFolder? folder);

        Assert.True(resolved);
        Assert.NotNull(folder);
        Assert.Equal("memory", folder.Label);
        Assert.True(folder.HuddleWide);
        Assert.Equal(harness.Folder.Path, folder.Path);
    }

    /// <summary>A describe in Huddle-wide Memory is refused, even for a file with no description, and the file is left as it was.</summary>
    [Fact]
    public async Task Describe_InHuddleWideMemory_IsRefused()
    {
        using MemoryTidyApplyHarness harness = new(huddle: true);
        string path = harness.Put("none.md", null, ShipBody);
        string before = File.ReadAllText(path);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Describe("none.md", "Shipping rules, read before shipping"));

        _ = Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.Equal(LockedRefusal, Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepRefused>()).Refusal);
        Assert.Equal(before, File.ReadAllText(path));
    }

    /// <summary>A merge whose kept file has a broken description is refused, because it would have to write one.</summary>
    [Fact]
    public async Task Merge_KeptFileWithBrokenDescription_IsRefused()
    {
        using MemoryTidyApplyHarness harness = new(huddle: true);
        harness.Put("price.md", null, PriceBody);
        harness.Put("ship.md", "Shipping rules, read before shipping", ShipBody);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Merge("price.md", ["ship.md"], MergedText, "Pricing and shipping, read before quoting"));

        _ = Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.Equal(LockedRefusal, Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepRefused>()).Refusal);
        Assert.True(File.Exists(harness.PathOf("ship.md")));
        Assert.Null(harness.Merges.FindInto("ship.md"));
    }

    /// <summary>
    /// A merge whose kept file has a passing description keeps that description, changes no description in the folder, puts its copies under
    /// <c>Huddle</c>, and is remembered for the file it removed.
    /// </summary>
    [Fact]
    public async Task Merge_KeptFileWithPassingDescription_KeepsItAndRecordsTheMerge()
    {
        using MemoryTidyApplyHarness harness = new(huddle: true);
        harness.Put("price.md", Good, PriceBody);
        harness.Put("ship.md", "Shipping rules, read before shipping", ShipBody);
        harness.Put("other.md", "Other rules, read before anything else", "Nothing to change here.");

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Merge("price.md", ["ship.md"], MergedText, "A brand new description, read it"));

        _ = Assert.IsType<HelperApplied.Changed>(applied);
        Assert.Equal(Good, MemoryFileRules.DescriptionOf(File.ReadAllText(harness.PathOf("price.md"))));
        Assert.Equal("Other rules, read before anything else", MemoryFileRules.DescriptionOf(File.ReadAllText(harness.PathOf("other.md"))));
        Assert.False(File.Exists(harness.PathOf("ship.md")));
        Assert.True(File.Exists(Path.Combine(harness.PassFolder, "Huddle", "ship.md")));
        MemoryTidyEntry.StepApplied done = Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepApplied>());
        Assert.All(done.Copies, static copy => Assert.StartsWith("Huddle/", copy, StringComparison.Ordinal));
        Assert.Equal("price.md", harness.Merges.FindInto("SHIP.md"));
        Assert.Null(harness.Merges.FindInto("price.md"));
    }

    /// <summary>A dropped Huddle-wide file leaves no merge record, so it is announced as removed.</summary>
    [Fact]
    public async Task Drop_InHuddleWideMemory_RecordsNoMerge()
    {
        using MemoryTidyApplyHarness harness = new(huddle: true);
        harness.Put("old.md", Good, PriceBody);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Drop("old.md"));

        _ = Assert.IsType<HelperApplied.Changed>(applied);
        Assert.False(File.Exists(harness.PathOf("old.md")));
        Assert.Null(harness.Merges.FindInto("old.md"));
    }

    /// <summary>A record is believed for 30 days to the minute and not after, the lookup ignores case, and a write deletes the rows past their days.</summary>
    [Fact]
    public void MergeStore_KeepsARecordThirtyDays_IgnoresCase_AndPrunesOnWrite()
    {
        using TempDataDir dir = new();
        IOptions<TeamOptions> options = dir.Options();
        ManualTimeProvider clock = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        MemoryTidyMergeStore store = new(options, clock);
        store.Record("old.md", "new.md");

        clock.Advance(TimeSpan.FromDays(30));
        string? atThirtyDays = store.FindInto("OLD.md");
        clock.Advance(TimeSpan.FromMinutes(1));
        string? past = store.FindInto("old.md");
        store.Record("other.md", "new.md");

        Assert.Equal("new.md", atThirtyDays);
        Assert.Null(past);
        using SqliteConnection connection = new($"Data Source={Path.Combine(dir.Path, "team.db")};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT file FROM memory_tidy_merges;";
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("other.md", reader.GetString(0));
        Assert.False(reader.Read());
    }

    /// <summary>
    /// The pass judges Huddle-wide Memory after the Agents' folders: its copies sit under <c>Huddle</c>, its state is kept under <c>huddle</c>, the
    /// list has the <c>Huddle-wide Memory</c> heading and the prompt locks descriptions.
    /// </summary>
    [Fact]
    public async Task RunAsync_JudgesHuddleWideMemoryAfterTheAgents()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using TidyRig rig = new(static team => team.Memory.Tidy.MinFiles = 3, "Nova");
        for (int index = 0; index < 3; index++)
        {
            _ = rig.Seed("Nova", $"t{index}.md", MemoryFileRules.Compose($"Topic {index}, read before using", $"Body {index} of Nova."));
            rig.SeedHuddle($"t{index}.md", MemoryFileRules.Compose($"Topic {index}, read before using", $"Body {index} of Huddle."));
        }

        List<string> prompts = [];
        rig.Process.Handler = (request, _) =>
        {
            prompts.Add(request.Prompt);
            return Task.FromResult(new HelperTurn(request.Prompt.Contains("Huddle.", StringComparison.Ordinal) ? """{"actions":[{"action":"drop","file":"t0.md","reason":"out of date"}]}""" : """{"actions":[]}""", ModelOffered: true, ToolCallsRefused: 0, Cost: null));
        };

        MemoryTidyPassEnd end = await rig.Pass.RunAsync(MemoryTidyStart.TidyNow, ct);

        Assert.Equal(MemoryTidyPassEnd.Done, end);
        Assert.Equal(2, rig.Process.Calls);
        Assert.Contains("every Agent (Huddle-wide Memory)", prompts[1], StringComparison.Ordinal);
        Assert.Contains(MemoryTidyPrompt.LockedDescriptions, prompts[1], StringComparison.Ordinal);
        Assert.NotNull(rig.State.GetState("huddle")?.Fingerprint);
        Assert.False(File.Exists(Path.Combine(rig.HuddleDir, "t0.md")));
        string pass = Assert.Single(Directory.GetDirectories(rig.Backups.RootPath));
        Assert.True(File.Exists(Path.Combine(pass, "Huddle", "t0.md")));
        Assert.Contains("## Huddle-wide Memory", await File.ReadAllTextAsync(MemoryTidyList.PathIn(pass), ct), StringComparison.Ordinal);
    }

    /// <summary>A refusal quotes a token cut to 80 characters with <c>...</c>, so a 5,000-character link does not fill it.</summary>
    [Fact]
    public void Refusals_QuoteAtMost80CharactersOfAToken()
    {
        string link = "https://shop.example/" + new string('a', 5000);
        MemoryTidyInput input = InputOf(Shown("a.md", link), Shown("b.md", string.Join(' ', Enumerable.Repeat("words", 1200))));

        MemoryTidyCheck lost = MemoryTidyChecks.Run(new MemoryTidyStep.Merge("a.md", ["b.md"], null, string.Join(' ', Enumerable.Repeat("words", 1200)), "r"), input, 0);
        MemoryTidyCheck added = MemoryTidyChecks.Run(new MemoryTidyStep.Describe("a.md", "See " + link + " for the details", "r"), InputOf(Shown("a.md", "Plain body text.", null)), 0);

        Assert.Equal("the merged text leaves out " + link[..80] + "... from a.md", lost.Refusal);
        Assert.Equal("the new text holds " + link[..80] + "..., which is not in the files it replaces", added.Refusal);
    }

    /// <summary>A token of 80 characters or fewer is quoted whole.</summary>
    [Fact]
    public void Quoted_ShortToken_IsWhole()
    {
        string link = "https://shop.example/" + new string('a', 59);

        MemoryTidyToken token = new(MemoryTidyTokenKind.Link, link);

        Assert.Equal(80, link.Length);
        Assert.Equal(link, token.Quoted);
    }

    /// <summary>The list's removed-tokens line, from the journal, holds a long link cut to 80 characters.</summary>
    [Fact]
    public async Task Rewrite_RemovingALongLink_JournalsItCut()
    {
        using MemoryTidyApplyHarness harness = new();
        string link = "https://shop.example/" + new string('a', 5000);
        harness.Put("price.md", Good, "Price is the same as always. Price is the same as always, " + link);

        _ = await harness.ApplyAsync(harness.Input(), Rewrite("price.md", "Price is the same as always."));

        string token = Assert.Single(Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepApplied>()).RemovedTokens);
        Assert.Equal(link[..80] + "...", token);
    }

    /// <summary>A name that matches one file exactly is that file, even when another differs from it only by case.</summary>
    [Fact]
    public void A1_ExactMatchWins_OverACaseVariant()
    {
        MemoryTidyInput input = InputOf(Shown("Price.md", PriceBody), Shown("price.md", ShipBody));

        MemoryTidyCheck check = MemoryTidyChecks.Run(new MemoryTidyStep.Drop("price.md", "old"), input, 0);

        Assert.Null(check.Refusal);
        Assert.Equal("price.md", Assert.Single(check.Files).File);
        Assert.Contains("Shipping", check.Files[0].Text, StringComparison.Ordinal);
    }

    /// <summary>Two files that differ only by case and a name that is neither: the step could mean either, so it is refused as not shown.</summary>
    [Fact]
    public void A1_TwoCaseVariantsAndNoExactMatch_IsRefused()
    {
        MemoryTidyInput input = InputOf(Shown("Price.md", PriceBody), Shown("PRICE.md", ShipBody));

        MemoryTidyCheck check = MemoryTidyChecks.Run(new MemoryTidyStep.Drop("price.md", "old"), input, 0);

        Assert.Equal("it names price.md, which it was not shown", check.Refusal);
    }

    /// <summary>A description holding the text of another placeholder comes out as written, in the index and in the file, and the files are listed once.</summary>
    [Fact]
    public void Render_ValueHoldingAPlaceholder_IsNotRescanned()
    {
        MemoryTidyInput input = new(
            "agent:Ada",
            "the Agent Ada",
            new DateOnly(2026, 11, 22),
            ["a.md · Uses {{files}} here · written 2026-10-01"],
            [new MemoryTidyFile("a.md", "v", "---\ndescription: Uses {{files}} here\n---\nBody {{today}}.\n", new DateOnly(2026, 10, 1))],
            new MemoryTidyLimits(12, 3, 40_000, false),
            new MemoryTidyPassContext("2026-11-22", "backup", new NoJournal()));

        string text = MemoryTidyPrompt.Render(input);

        Assert.Equal(1, text.Split("<file name=", StringSplitOptions.None).Length - 1);
        Assert.Contains("a.md · Uses {{files}} here · written 2026-10-01", text, StringComparison.Ordinal);
        Assert.Contains("Body {{today}}.", text, StringComparison.Ordinal);
    }

    private static MemoryTidyFile Shown(string name, string body, string? description = Good) =>
        new(name, "v-" + name, description is null ? body + "\n" : MemoryFileRules.Compose(description, body), new DateOnly(2026, 10, 1));

    private static MemoryTidyInput InputOf(params MemoryTidyFile[] files) =>
        new("agent:Nova", "the Agent Nova", new DateOnly(2026, 11, 22), [], files, new MemoryTidyLimits(12, 3, 40000, false), new MemoryTidyPassContext("p", "b", new NoJournal()));

    private sealed class NoJournal : IMemoryTidyJournal
    {
        public void Append(MemoryTidyEntry entry)
        {
        }
    }
}
