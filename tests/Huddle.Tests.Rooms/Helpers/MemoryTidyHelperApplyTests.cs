using Agency.Huddle.App.Data;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Feed;
using Agency.Huddle.App.Helpers;
using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;
using static Agency.Huddle.Tests.Helpers.MemoryTidyApplyHarness;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>
/// <c>MemoryTidyHelper.ApplyAsync</c> (Memory Tidy spec §6.9 to §6.11 and §8.1): the order, the checks, the journal, the writer's outcomes, the
/// History and the day rows, over a real folder, the real writer and a real journal.
/// </summary>
public sealed class MemoryTidyHelperApplyTests
{
    private const string Good = "Pricing rules, read before quoting a price";
    private const string PriceBody = "Price is 5 euro on 2026-10-01 at https://shop.example/price.";
    private const string ShipBody = "Shipping is free over 50, ask ann@example.com.";
    private const string MergedText = "Price 5 euro on 2026-10-01 at https://shop.example/price. Shipping free over 50, ask ann@example.com.";

    /// <summary>The Helper is registered under its id with the name, description and defaults of §6.9, and does not cache Verdicts.</summary>
    [Fact]
    public void Helper_HasItsIdNameAndDefaults()
    {
        using MemoryTidyApplyHarness harness = new();
        MemoryTidyHelper helper = harness.Helper;

        Assert.Equal("MemoryTidy", helper.Id.Value);
        Assert.Equal("Memory Tidy", helper.DisplayName);
        Assert.StartsWith("Tidies each Agent's Memory and Huddle-wide Memory once a week: merges files on one topic, removes what is out of date and fixes descriptions.", helper.Description, StringComparison.Ordinal);
        Assert.EndsWith("and a copy of every file it changes is kept.", helper.Description, StringComparison.Ordinal);
        Assert.Equal(new HelperDefaults(true, "claude", "sonnet", "low", 20, 10, 1, 180), helper.Defaults);
        Assert.False(((IHelperAgent)helper).CachesVerdicts);
        Assert.Equal(["helper.memoryTidy.system", "helper.memoryTidy.folder"], helper.PromptKeys);
    }

    /// <summary>A merge writes the kept file with the new body and its own description, removes the folded file, copies both and records one History entry.</summary>
    [Fact]
    public async Task Merge_WritesTheKeptFile_RemovesTheOther_CopiesBoth_AndRecordsHistory()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("price.md", Good, PriceBody);
        harness.Put("ship.md", "Shipping rules, read before shipping", ShipBody);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Merge("price.md", ["ship.md"], MergedText));

        Assert.Equal("Tidied Nova's Memory: 1 merged", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.Equal(MemoryFileRules.Compose(Good, MergedText), File.ReadAllText(harness.PathOf("price.md")));
        Assert.False(File.Exists(harness.PathOf("ship.md")));
        Assert.Equal(PriceBody + "\n", MemoryTidyChecks.BodyOf(File.ReadAllText(Path.Combine(harness.BackupFolder, "price.md"))));
        Assert.True(File.Exists(Path.Combine(harness.BackupFolder, "ship.md")));
        DocHistoryEntry entry = Assert.Single(harness.HistoryOf("price.md"));
        Assert.Equal("Memory Tidy (Helper)", entry.Actor);
        Assert.Equal(DocActorKind.Helper, entry.ActorKind);
        Assert.Equal(DocChangeKind.Edited, entry.Kind);
        Assert.Equal("Merged ship.md into this file: same topic", entry.Summary);
    }

    /// <summary>
    /// Writing a file through the Helper posts no <c>DocumentChanged</c> Feed item, though the same History write by another Helper does:
    /// the pass posts one item of its own.
    /// </summary>
    [Fact]
    public async Task Apply_PostsNoDocumentChangedItemPerFile()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("price.md", Good, PriceBody + " Extra words here to shorten.");
        _ = await harness.ApplyAsync(harness.Input(), Rewrite("price.md", PriceBody));

        Assert.Single(harness.HistoryOf("price.md"));
        Assert.DoesNotContain(harness.Feed.Posts, static post => post.SourceType == FeedSourceType.DocumentChanged);
    }

    /// <summary>A rewrite shortens the file, keeps its description, names the removed tokens in the journal and records "Shortened".</summary>
    [Fact]
    public async Task Rewrite_Shortens_JournalsTheRemovedTokens_AndRecordsHistory()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("price.md", Good, "Price is 5 euro on 2026-10-01. Price is 5 euro. Price is 5 euro again, see https://shop.example/a.");

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Rewrite("price.md", "Price is 5 euro on 2026-10-01. Price is 5 euro, again."));

        Assert.Equal("Tidied Nova's Memory: 1 shortened", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.Equal(MemoryFileRules.Compose(Good, "Price is 5 euro on 2026-10-01. Price is 5 euro, again."), File.ReadAllText(harness.PathOf("price.md")));
        MemoryTidyEntry.StepApplied done = Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepApplied>());
        Assert.Equal(["https://shop.example/a"], done.RemovedTokens);
        Assert.False(done.CountsAsDrop);
        Assert.Equal(["Agents/Nova/price.md"], done.Copies);
        Assert.Equal("Shortened: repeats itself", Assert.Single(harness.HistoryOf("price.md")).Summary);
    }

    /// <summary>A describe writes a new description where none exists and keeps the body byte for byte; History says "New description".</summary>
    [Fact]
    public async Task Describe_WhereNoneWorks_WritesItAndKeepsTheBody()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("ship.md", null, ShipBody);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Describe("ship.md", "Shipping rules, read before shipping"));

        Assert.Equal("Tidied Nova's Memory: 1 described", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.Equal(MemoryFileRules.Compose("Shipping rules, read before shipping", ShipBody), File.ReadAllText(harness.PathOf("ship.md")));
        Assert.Equal("New description: no description", Assert.Single(harness.HistoryOf("ship.md")).Summary);
    }

    /// <summary>A drop removes the file after copying it, writes no History entry for it and counts as dropped.</summary>
    [Fact]
    public async Task Drop_RemovesTheFile_AfterCopyingIt()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("old.md", Good, PriceBody);

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Drop("old.md"));

        Assert.Equal("Tidied Nova's Memory: 1 dropped", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.False(File.Exists(harness.PathOf("old.md")));
        Assert.True(File.Exists(Path.Combine(harness.BackupFolder, "old.md")));
        Assert.Empty(harness.Feed.Posts);
        MemoryTidyEntry.StepApplied done = Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepApplied>());
        Assert.Equal(["old.md"], done.Removed);
        Assert.Empty(done.Written);
    }

    /// <summary>The summary counts each kind and the refusals: <c>2 merged, 1 shortened, 1 dropped, 1 refused</c>.</summary>
    [Fact]
    public async Task Summary_CountsEachKind()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1.");
        harness.Put("b.md", Good, "Beta is 2.");
        harness.Put("c.md", Good, "Gamma is 3.");
        harness.Put("d.md", Good, "Delta is 4.");
        harness.Put("e.md", Good, "Epsilon says the same thing twice. Epsilon says the same thing twice.");
        harness.Put("f.md", Good, "Zeta is old.");

        HelperApplied applied = await harness.ApplyAsync(
            harness.Input(),
            Merge("a.md", ["b.md"], "Alpha 1 Beta 2"),
            Merge("c.md", ["d.md"], "Gamma 3 Delta 4"),
            Rewrite("e.md", "Epsilon says the same thing twice, once."),
            Drop("f.md"),
            Drop("nothere.md"));

        Assert.Equal("Tidied Nova's Memory: 2 merged, 1 shortened, 1 dropped, 1 refused", Assert.IsType<HelperApplied.Changed>(applied).Summary);
    }

    /// <summary>
    /// Every check refuses its own step and the others still run: A-1 a file not shown, A-2 a merge of one file with itself, A-3 a longer
    /// rewrite, A-4 a new number, A-5 a merge that loses a date, A-6 a heavy rewrite past the cap, A-7 a describe of a passing description.
    /// </summary>
    [Fact]
    public async Task EveryCheck_RefusesItsStep_AndTheOthersStillRun()
    {
        using MemoryTidyApplyHarness harness = new(static team => team.Memory.Tidy.MaxDropsPerFolder = 1);
        harness.Put("a.md", Good, "Alpha is 1. Alpha is repeated here for length, padding padding.");
        harness.Put("b.md", Good, "Beta is 2.");
        harness.Put("c.md", Good, "Gamma was 2026-10-01 and is 3.");
        harness.Put("d.md", Good, "Delta is 4 at https://d.example/x");
        harness.Put("e.md", Good, new string('e', 100));
        harness.Put("f.md", Good, new string('f', 100));
        harness.Put("g.md", Good, "Golf stays as it is.");
        harness.Put("h.md", Good, "Hotel is 5.");
        harness.Put("ok.md", Good, "Echo is repeated, echo is repeated, echo is repeated.");

        HelperApplied applied = await harness.ApplyAsync(
            harness.Input(),
            Rewrite("ghost.md", "x"),
            Merge("h.md", [], "Hotel 5"),
            Rewrite("a.md", "Alpha is 1. Alpha is repeated here for length, padding padding. And more words."),
            Rewrite("b.md", "Beta is 9."),
            Merge("c.md", ["d.md"], "Gamma is 3 and Delta is 4 at https://d.example/x"),
            Rewrite("e.md", "e"),
            Rewrite("f.md", "f"),
            Describe("g.md", "Golf rules, read before playing"),
            Rewrite("ok.md", "Echo is repeated, echo is repeated."));

        List<MemoryTidyEntry.StepRefused> refused = [.. harness.Entries().OfType<MemoryTidyEntry.StepRefused>()];
        Assert.Equal(
            [
                "it names ghost.md, which it was not shown",
                "a merge needs different files",
                "the new text is longer than what it replaces",
                "the new text holds 9, which is not in the files it replaces",
                "the merged text leaves out 2026-10-01 from c.md",
                "it removes most of f.md, and this pass may drop no more files",
                "the description of g.md is fine as it is",
            ],
            refused.Select(static entry => entry.Refusal));
        Assert.Equal("Tidied Nova's Memory: 1 shortened, 1 dropped, 7 refused", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        Assert.Equal(MemoryFileRules.Compose(Good, "Echo is repeated, echo is repeated."), File.ReadAllText(harness.PathOf("ok.md")));
        Assert.Equal(MemoryFileRules.Compose(Good, "e"), File.ReadAllText(harness.PathOf("e.md")));
    }

    /// <summary>A step that every check passes but the writer refuses (A-8: a description, a size cap, a body that is only frontmatter) is refused after its start, and the file is left as it was.</summary>
    [Fact]
    public async Task WriterRefusals_AreJournaledAfterTheStart()
    {
        using MemoryTidyApplyHarness harness = new(static team => team.Memory.MaxFileBytes = 300);
        harness.Put("tiny.md", "tiny", "Tiny body is long enough to shorten, with some words.");
        harness.Put("big.md", Good, new string('b', 400));
        harness.Put("bare.md", Good, "Bare body longer than echo.");
        string before = File.ReadAllText(harness.PathOf("tiny.md"));

        _ = await harness.ApplyAsync(
            harness.Input(),
            Rewrite("tiny.md", "Tiny body is long enough to shorten, some words.", "short"),
            Rewrite("big.md", new string('b', 399)),
            Rewrite("bare.md", "---\ndescription: x\n---"));

        List<MemoryTidyEntry> entries = harness.Entries();
        List<MemoryTidyEntry.StepRefused> refused = [.. entries.OfType<MemoryTidyEntry.StepRefused>()];
        Assert.Equal(3, refused.Count);
        Assert.Contains("it is shorter than 20 characters", refused[0].Refusal, StringComparison.Ordinal);
        Assert.Contains("over the 300 byte limit", refused[1].Refusal, StringComparison.Ordinal);
        Assert.Contains("only a frontmatter block", refused[2].Refusal, StringComparison.Ordinal);
        Assert.Equal(3, entries.OfType<MemoryTidyEntry.StepStarted>().Count());
        Assert.Equal(before, File.ReadAllText(harness.PathOf("tiny.md")));
    }

    /// <summary>A file that changed after it was shown is skipped (A-9) and the other steps still run; no copy is made for it.</summary>
    [Fact]
    public async Task StaleFile_IsSkipped_AndTheOthersStillRun()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("stale.md", Good, "Stale body with some words, to be shortened.");
        harness.Put("fresh.md", Good, "Fresh body with some words, to be shortened.");
        MemoryTidyInput input = harness.Input();
        File.AppendAllText(harness.PathOf("stale.md"), "Agent wrote this meanwhile.\n");

        HelperApplied applied = await harness.ApplyAsync(input, Rewrite("stale.md", "Stale body with some words."), Rewrite("fresh.md", "Fresh body with some words."));

        Assert.Equal("Tidied Nova's Memory: 1 shortened, 1 skipped", Assert.IsType<HelperApplied.Changed>(applied).Summary);
        MemoryTidyEntry.StepSkipped skipped = Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepSkipped>());
        Assert.Equal("stale.md", skipped.File);
        Assert.False(File.Exists(Path.Combine(harness.BackupFolder, "stale.md")));
    }

    /// <summary>A copy that cannot be written (A-10) faults its step, leaves the file, logs, and the other steps still run; the result is <c>Refused</c>.</summary>
    [Fact]
    public async Task CopyFailure_FaultsTheStep_AndReturnsRefused()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("blocked.md", Good, "Blocked body with some words, to be shortened.");
        harness.Put("fine.md", Good, "Fine body with some words, to be shortened.");
        _ = Directory.CreateDirectory(Path.Combine(harness.BackupFolder, "blocked.md"));
        string before = File.ReadAllText(harness.PathOf("blocked.md"));

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Rewrite("blocked.md", "Blocked body."), Rewrite("fine.md", "Fine body."));

        Assert.IsType<HelperApplied.Refused>(applied);
        Assert.Equal(before, File.ReadAllText(harness.PathOf("blocked.md")));
        Assert.Equal(MemoryFileRules.Compose(Good, "Fine body."), File.ReadAllText(harness.PathOf("fine.md")));
        Assert.Equal("blocked.md", Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepFaulted>()).File);
    }

    /// <summary>When every step is refused the result is <c>NoChange</c>, never <c>Refused</c>: the Helper worked and was turned down.</summary>
    [Fact]
    public async Task EveryStepRefused_IsNoChange()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1.");

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Rewrite("ghost.md", "x"), Rewrite("a.md", "Alpha is 1. Longer text."));

        Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.Equal(2, harness.Entries().OfType<MemoryTidyEntry.StepRefused>().Count());
    }

    /// <summary>A Helper turned off since the call started drops the Verdict and writes no journal entry, so the folder's marker is not stored.</summary>
    [Fact]
    public async Task TurnedOffMeanwhile_Drops_AndWritesNoJournal()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1. Alpha is 1 and a bit more.");
        MemoryTidyInput input = harness.Input();
        harness.SettingsStore.Save(MemoryTidyHelper.HelperId, new HelperSettingsRow(false, null, null, null, 20));

        HelperApplied applied = await harness.ApplyAsync(input, Rewrite("a.md", "Alpha is 1."));

        Assert.Equal("the Helper was turned off", Assert.IsType<HelperApplied.Dropped>(applied).Why);
        Assert.Equal(string.Empty, harness.JournalText());
        Assert.Equal(MemoryFileRules.Compose(Good, "Alpha is 1. Alpha is 1 and a bit more."), File.ReadAllText(harness.PathOf("a.md")));
    }

    /// <summary>A Verdict that fails the whole-Verdict checks is refused whole, with no journal entry.</summary>
    [Fact]
    public async Task InvalidVerdict_IsRefusedWhole()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1.");

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Drop("a.md"), Rewrite("a.md", "x"));

        Assert.Equal("a.md is used in two actions", Assert.IsType<HelperApplied.Refused>(applied).Message);
        Assert.Equal(string.Empty, harness.JournalText());
    }

    /// <summary>The <c>huddle</c> key is applied to Huddle-wide Memory and never to an Agent's own folder, whatever folder the input was built over.</summary>
    [Fact]
    public async Task HuddleKey_NeverWritesAnAgentsFolder()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1.");
        MemoryTidyInput input = harness.Input() with { FolderKey = "huddle" };

        _ = await harness.Helper.ApplyAsync("huddle", input, new MemoryTidyVerdict([Drop("a.md")]), TestContext.Current.CancellationToken);

        Assert.True(File.Exists(harness.PathOf("a.md")));
    }

    /// <summary>The journal has <c>FolderJudged</c>, then <c>StepStarted</c> before and the outcome after each step, and no entry holds a step's text or description.</summary>
    [Fact]
    public async Task Journal_HoldsStartThenOutcome_AndNeverTheText()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", null, "Alpha says things, and says things again, and again.");
        harness.Put("b.md", Good, "Beta says things, and says things again, and again.");

        _ = await harness.ApplyAsync(
            harness.Input(),
            Describe("a.md", "Zebra rules, read before grazing"),
            Rewrite("b.md", "Quokka says things, once."));

        List<MemoryTidyEntry> entries = harness.Entries();
        Assert.IsType<MemoryTidyEntry.FolderJudged>(entries[0]);
        Assert.Equal(
            ["StepStarted", "StepApplied", "StepStarted", "StepApplied"],
            entries.Skip(1).Select(static entry => entry.GetType().Name));
        MemoryTidyEntry.StepStarted started = Assert.IsType<MemoryTidyEntry.StepStarted>(entries[1]);
        Assert.Equal(1, started.Step);
        Assert.Equal("describe", started.Kind);
        Assert.Equal("a.md", Assert.Single(started.Files).Name);
        Assert.Equal(1, Assert.IsType<MemoryTidyEntry.StepApplied>(entries[2]).Step);
        Assert.Equal(2, Assert.IsType<MemoryTidyEntry.StepStarted>(entries[3]).Step);
        Assert.DoesNotContain("Zebra", harness.JournalText(), StringComparison.Ordinal);
        Assert.DoesNotContain("Quokka", harness.JournalText(), StringComparison.Ordinal);
    }

    /// <summary>A heavy rewrite counts as a drop in the list, one past the cap is refused, and a cap of zero refuses any heavy rewrite.</summary>
    [Fact]
    public async Task HeavyRewrite_CountsAsADrop_AndOnePastTheCapIsRefused()
    {
        using MemoryTidyApplyHarness capped = new(static team => team.Memory.Tidy.MaxDropsPerFolder = 1);
        capped.Put("a.md", Good, new string('a', 100));
        capped.Put("b.md", Good, new string('b', 100));

        HelperApplied first = await capped.ApplyAsync(capped.Input(), Rewrite("a.md", "a"), Rewrite("b.md", "b"));

        Assert.Equal("Tidied Nova's Memory: 1 dropped, 1 refused", Assert.IsType<HelperApplied.Changed>(first).Summary);
        Assert.True(Assert.Single(capped.Entries().OfType<MemoryTidyEntry.StepApplied>()).CountsAsDrop);

        using MemoryTidyApplyHarness none = new(static team => team.Memory.Tidy.MaxDropsPerFolder = 0);
        none.Put("a.md", Good, new string('a', 100));

        HelperApplied zero = await none.ApplyAsync(none.Input(), Rewrite("a.md", "a"));

        Assert.IsType<HelperApplied.NoChange>(zero);
        Assert.Contains("this pass may drop no more files", Assert.Single(none.Entries().OfType<MemoryTidyEntry.StepRefused>()).Refusal, StringComparison.Ordinal);
    }

    /// <summary>A rewrite or merge that gives a description for a passing one is applied and keeps the file's own, and says so in the journal.</summary>
    [Fact]
    public async Task DescriptionForAPassingOne_IsAppliedButKeepsTheOld()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha says things, and says things again, and again.");

        _ = await harness.ApplyAsync(harness.Input(), Rewrite("a.md", "Alpha says things.", "A brand new description for alpha"));

        Assert.Equal(MemoryFileRules.Compose(Good, "Alpha says things."), File.ReadAllText(harness.PathOf("a.md")));
        Assert.Equal("it kept its own description", Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepApplied>()).Note);
    }

    /// <summary>A merge whose kept file's description is broken takes the new one.</summary>
    [Fact]
    public async Task MergeWithABrokenDescription_TakesTheNewOne()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", "tiny", "Alpha is 1.");
        harness.Put("b.md", Good, "Beta is 2.");

        _ = await harness.ApplyAsync(harness.Input(), Merge("a.md", ["b.md"], "Alpha 1 Beta 2", "Alpha and beta, read before counting"));

        Assert.Equal(MemoryFileRules.Compose("Alpha and beta, read before counting", "Alpha 1 Beta 2"), File.ReadAllText(harness.PathOf("a.md")));
    }

    /// <summary>With descriptions locked (a Huddle-wide folder's rule) a describe is refused and a rewrite of a file with no description is refused.</summary>
    [Fact]
    public async Task LockedFolder_WritesNoDescription()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("none.md", null, "Alpha says things, and says things again, and again.");
        harness.Put("other.md", null, "Beta says things, and says things again, and again.");
        MemoryTidyInput input = harness.Input();
        input = input with { Limits = input.Limits with { DescriptionsLocked = true } };

        HelperApplied applied = await harness.ApplyAsync(input, Describe("none.md", "Alpha rules, read before counting"), Rewrite("other.md", "Beta says things, and says things again."));

        Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.All(harness.Entries().OfType<MemoryTidyEntry.StepRefused>(), static entry => Assert.Equal("descriptions do not change in this folder", entry.Refusal));
        Assert.Equal(2, harness.Entries().OfType<MemoryTidyEntry.StepRefused>().Count());
    }

    /// <summary>Text that echoes the file's frontmatter is checked raw: a date inside the <c>---</c> block is a new token and the step is refused.</summary>
    [Fact]
    public async Task EchoedFrontmatter_IsCheckedAsRawText()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha says things, and says things again, and again, and more.");
        string before = File.ReadAllText(harness.PathOf("a.md"));

        HelperApplied applied = await harness.ApplyAsync(harness.Input(), Rewrite("a.md", "---\nupdated: 2031-05-05\n---\nAlpha says things."));

        Assert.IsType<HelperApplied.NoChange>(applied);
        Assert.Equal(before, File.ReadAllText(harness.PathOf("a.md")));
        Assert.Contains("2031-05-05", Assert.Single(harness.Entries().OfType<MemoryTidyEntry.StepRefused>()).Refusal, StringComparison.Ordinal);
    }

    /// <summary>The day rows follow the steps: a merge takes the latest day of its sources, a rewrite keeps its day with the new version, a removed file loses its row.</summary>
    [Fact]
    public async Task DayRows_FollowTheSteps()
    {
        using MemoryTidyApplyHarness harness = new();
        harness.Put("a.md", Good, "Alpha is 1.", new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        harness.Put("b.md", Good, "Beta is 2.", new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc));
        harness.Put("c.md", Good, "Gamma says things, and says things again, and again.", new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));

        _ = await harness.ApplyAsync(harness.Input(), Merge("a.md", ["b.md"], "Alpha 1 Beta 2"), Rewrite("c.md", "Gamma says things."));

        IReadOnlyList<MemoryTidyFileRow> rows = harness.Store.GetFiles(Key);
        Assert.Equal(new DateOnly(2026, 11, 8), rows.Single(static row => row.File == "a.md").WrittenDay);
        Assert.Equal(new DateOnly(2026, 11, 3), rows.Single(static row => row.File == "c.md").WrittenDay);
        Assert.DoesNotContain(rows, static row => row.File == "b.md");
        Assert.Equal(MemoryVersion.Of(File.ReadAllBytes(harness.PathOf("c.md"))), rows.Single(static row => row.File == "c.md").Version);
    }
}
