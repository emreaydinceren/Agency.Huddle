using Agency.Huddle.App.Helpers.MemoryTidy;
using Agency.Huddle.App.Memory;

namespace Agency.Huddle.Tests.Helpers;

/// <summary>Checks A-1 to A-7 of Memory Tidy spec §6.10 (§8.1, "MemoryTidyChecks"), each alone, as pure functions over a step and an input.</summary>
public sealed class MemoryTidyChecksTests
{
    private const string Good = "Pricing rules, read before quoting a price";
    private const string PriceBody = "Price is 5 euro on 2026-10-01 at https://shop.example/price.";
    private const string ShipBody = "Shipping is free over 50, ask ann@example.com.";
    private const string MergedText = "Price 5 euro on 2026-10-01 at https://shop.example/price. Shipping free over 50, ask ann@example.com.";

    /// <summary>A name that matches a shown file ignoring case is accepted and the file is the one the input spells.</summary>
    [Fact]
    public void A1_NameMatchesIgnoringCase_IsAccepted()
    {
        MemoryTidyCheck check = Run(new MemoryTidyStep.Drop("PRICE.md", "old"), Input(Shown("price.md", PriceBody)));

        Assert.Null(check.Refusal);
        Assert.Equal("price.md", Assert.Single(check.Files).File);
    }

    /// <summary>A file that was not shown, <c>reported.md</c>, <c>MEMORY.md</c>, a path and <c>..</c> are all refused with the named text.</summary>
    /// <param name="name">The name the step uses.</param>
    [Theory]
    [InlineData("other.md")]
    [InlineData("reported.md")]
    [InlineData("MEMORY.md")]
    [InlineData("../price.md")]
    [InlineData("sub/price.md")]
    [InlineData("..")]
    public void A1_NamedFileNotShown_IsRefused(string name)
    {
        MemoryTidyCheck check = Run(new MemoryTidyStep.Drop(name, "old"), Input(Shown("price.md", PriceBody)));

        Assert.Equal($"it names {name}, which it was not shown", check.Refusal);
    }

    /// <summary>A merge whose <c>into</c> is among its <c>from</c> files, or whose <c>from</c> is empty, needs different files.</summary>
    [Fact]
    public void A2_MergeNeedsDifferentFiles()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody), Shown("ship.md", ShipBody));

        MemoryTidyCheck same = Run(new MemoryTidyStep.Merge("price.md", ["PRICE.md"], null, MergedText, "r"), input);
        MemoryTidyCheck none = Run(new MemoryTidyStep.Merge("price.md", [], null, MergedText, "r"), input);

        Assert.Equal("a merge needs different files", same.Refusal);
        Assert.Equal("a merge needs different files", none.Refusal);
    }

    /// <summary>A rewrite as long as the old body is accepted; one byte longer is refused.</summary>
    [Fact]
    public void A3_RewriteLongerThanTheBody_IsRefused()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody));

        MemoryTidyCheck equal = Run(new MemoryTidyStep.Rewrite("price.md", null, PriceBody, "r"), input);
        MemoryTidyCheck longer = Run(new MemoryTidyStep.Rewrite("price.md", null, PriceBody + "!", "r"), input);

        Assert.Null(equal.Refusal);
        Assert.Equal("the new text is longer than what it replaces", longer.Refusal);
    }

    /// <summary>Size is counted in UTF-8 bytes of the body: fewer characters of two-byte letters can still be longer.</summary>
    [Fact]
    public void A3_SizeIsInBytes_NotCharacters()
    {
        MemoryTidyInput input = Input(Shown("note.md", "abcdef"));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Rewrite("note.md", null, "ééé" + "é", "r"), input);

        Assert.Equal("the new text is longer than what it replaces", check.Refusal);
    }

    /// <summary>The old file's frontmatter is not part of its body, so a body of the same size is accepted however long the frontmatter is.</summary>
    [Fact]
    public void A3_FrontmatterIsNotCountedInTheOldBody()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody, "Pricing rules, read before quoting a price, with a long description added"));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Rewrite("price.md", null, PriceBody + " More.", "r"), input);

        Assert.Equal("the new text is longer than what it replaces", check.Refusal);
    }

    /// <summary>A merge shorter than its largest source is refused; one longer than all of them together is refused.</summary>
    [Fact]
    public void A3_MergeSizeBothWays()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody), Shown("ship.md", ShipBody));

        MemoryTidyCheck shorter = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], null, "Price 5 euro on 2026-10-01.", "r"), input);
        MemoryTidyCheck longer = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], null, MergedText + new string('x', 200), "r"), input);

        Assert.Equal("the merged text is shorter than one of the files it merges", shorter.Refusal);
        Assert.Equal("the new text is longer than what it replaces", longer.Refusal);
    }

    /// <summary>A new date, number, link or address in the text is refused, naming the token.</summary>
    /// <param name="added">What the rewrite adds.</param>
    /// <param name="token">The token named.</param>
    [Theory]
    [InlineData(" 2027-01-01", "2027-01-01")]
    [InlineData(" 77", "77")]
    [InlineData(" https://evil.example/x", "https://evil.example/x")]
    [InlineData(" bob@evil.example", "bob@evil.example")]
    public void A4_NewTokenInTheText_IsRefused(string added, string token)
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody + " " + new string('z', 120)));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Rewrite("price.md", null, PriceBody + added, "r"), input);

        Assert.Equal($"the new text holds {token}, which is not in the files it replaces", check.Refusal);
    }

    /// <summary>A new token in the description is refused like one in the body; a token from a source's own description is allowed.</summary>
    [Fact]
    public void A4_NewTokenInTheDescription_IsRefused()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody, null), Shown("ship.md", ShipBody, "Shipping rules for 2026 orders"));
        MemoryTidyInput own = Input(Shown("price.md", PriceBody + new string('z', 40), "Pricing rules for 2026 quotes, read first"));

        MemoryTidyCheck fresh = Run(new MemoryTidyStep.Describe("price.md", "Prices valid until 2031, read before quoting", "r"), input);
        MemoryTidyCheck known = Run(new MemoryTidyStep.Rewrite("price.md", null, "Price in 2026", "r"), own);

        Assert.Equal("the new text holds 2031, which is not in the files it replaces", fresh.Refusal);
        Assert.Null(known.Refusal);
    }

    /// <summary>Ordinary new words are fine in a description: there is no rule on words (D-14).</summary>
    [Fact]
    public void A4_OrdinaryNewWordsInADescription_AreAccepted()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody, null));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Describe("price.md", "Price list, read before quoting a customer", "r"), input);

        Assert.Null(check.Refusal);
        Assert.Equal("Price list, read before quoting a customer", check.Description);
    }

    /// <summary>A date inside a <c>---</c> block in the text is checked as raw text: it is a new token.</summary>
    [Fact]
    public void A4_EchoedFrontmatterIsCheckedRaw()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody + " " + new string('z', 120)));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Rewrite("price.md", null, "---\nupdated: 2031-01-01\n---\n" + PriceBody, "r"), input);

        Assert.Equal("the new text holds 2031-01-01, which is not in the files it replaces", check.Refusal);
    }

    /// <summary>A merge that leaves out a source's date, number, link or address is refused and names the source.</summary>
    /// <param name="text">The merged text, missing one token.</param>
    /// <param name="token">The token missing.</param>
    /// <param name="file">The source that holds it.</param>
    [Theory]
    [InlineData("Price 5 euro at https://shop.example/price. Shipping free over 50, ask ann@example.com. Padding padding", "2026-10-01", "price.md")]
    [InlineData("Price on 2026-10-01 at https://shop.example/price. Shipping free over 50, ask ann@example.com. Padding", "5", "price.md")]
    [InlineData("Price 5 euro on 2026-10-01. Shipping free over 50, ask ann@example.com. Padding padding padding", "https://shop.example/price", "price.md")]
    [InlineData("Price 5 euro on 2026-10-01 at https://shop.example/price. Shipping free over 50, ask someone. Padding", "ann@example.com", "ship.md")]
    public void A5_MergeLeavingOutAToken_IsRefused(string text, string token, string file)
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody), Shown("ship.md", ShipBody));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], null, text, "r"), input);

        Assert.Equal($"the merged text leaves out {token} from {file}", check.Refusal);
    }

    /// <summary>A merge that keeps every token is accepted, and a date in a folded file's own frontmatter is not read.</summary>
    [Fact]
    public void A5_MergeKeepingEveryToken_IsAccepted_AndFrontmatterIsNotRead()
    {
        MemoryTidyFile folded = Shown("ship.md", ShipBody) with { Text = "---\ndescription: Shipping rules, read before shipping\nupdated: 2025-01-01\n---\n\n" + ShipBody + "\n" };

        MemoryTidyCheck check = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], null, MergedText, "r"), Input(Shown("price.md", PriceBody), folded));

        Assert.Null(check.Refusal);
        Assert.False(check.CountsAsDrop);
    }

    /// <summary>A rewrite under half the old body counts as a drop, and a rewrite of half or more does not.</summary>
    [Fact]
    public void A6_HeavyRewrite_CountsAsADrop()
    {
        MemoryTidyInput input = Input(Shown("long.md", new string('a', 100)));

        MemoryTidyCheck heavy = Run(new MemoryTidyStep.Rewrite("long.md", null, new string('a', 49), "r"), input);
        MemoryTidyCheck half = Run(new MemoryTidyStep.Rewrite("long.md", null, new string('a', 50), "r"), input);

        Assert.True(heavy.CountsAsDrop);
        Assert.False(half.CountsAsDrop);
    }

    /// <summary>One past the cap is refused with the named text, counting what earlier steps already took; a drop counts too.</summary>
    [Fact]
    public void A6_OnePastTheCap_IsRefused()
    {
        MemoryTidyInput input = Input(maxDrops: 2, files: Shown("long.md", new string('a', 100)));
        MemoryTidyStep heavy = new MemoryTidyStep.Rewrite("long.md", null, "aa", "r");

        MemoryTidyCheck last = MemoryTidyChecks.Run(heavy, input, 1);
        MemoryTidyCheck over = MemoryTidyChecks.Run(heavy, input, 2);
        MemoryTidyCheck drop = MemoryTidyChecks.Run(new MemoryTidyStep.Drop("long.md", "r"), input, 2);

        Assert.Null(last.Refusal);
        Assert.Equal("it removes most of long.md, and this pass may drop no more files", over.Refusal);
        Assert.Equal("it removes most of long.md, and this pass may drop no more files", drop.Refusal);
    }

    /// <summary>A cap of zero refuses any heavy rewrite but not a gentle one.</summary>
    [Fact]
    public void A6_CapOfZero_RefusesAnyHeavyRewrite()
    {
        MemoryTidyInput input = Input(maxDrops: 0, files: Shown("long.md", new string('a', 100)));

        MemoryTidyCheck heavy = MemoryTidyChecks.Run(new MemoryTidyStep.Rewrite("long.md", null, "aa", "r"), input, 0);
        MemoryTidyCheck gentle = MemoryTidyChecks.Run(new MemoryTidyStep.Rewrite("long.md", null, new string('a', 90), "r"), input, 0);

        Assert.NotNull(heavy.Refusal);
        Assert.Null(gentle.Refusal);
    }

    /// <summary>A describe is accepted only where the description is missing or broken, and a description that passes is refused.</summary>
    [Fact]
    public void A7_Describe_OnlyWhereNoneWorks()
    {
        MemoryTidyInput input = Input(Shown("ok.md", PriceBody), Shown("none.md", ShipBody, null), Shown("short.md", "Plain body text.", "tiny"));

        MemoryTidyCheck passes = Run(new MemoryTidyStep.Describe("ok.md", "Pricing rules, read before quoting again", "r"), input);
        MemoryTidyCheck missing = Run(new MemoryTidyStep.Describe("none.md", "Shipping rules, read before shipping", "r"), input);
        MemoryTidyCheck broken = Run(new MemoryTidyStep.Describe("short.md", "Plain notes, read before writing", "r"), input);

        Assert.Equal("the description of ok.md is fine as it is", passes.Refusal);
        Assert.Null(missing.Refusal);
        Assert.Equal("Shipping rules, read before shipping", missing.Description);
        Assert.Null(broken.Refusal);
    }

    /// <summary>A rewrite or a merge that gives a description for a file whose own passes is applied, keeps the old one and says so.</summary>
    [Fact]
    public void A7_DescriptionForAPassingOne_IsKeptWithANote()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody), Shown("ship.md", ShipBody));

        MemoryTidyCheck rewrite = Run(new MemoryTidyStep.Rewrite("price.md", "Pricing rules, read before quoting again", PriceBody, "r"), input);
        MemoryTidyCheck merge = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], "Pricing and shipping, read before quoting", MergedText, "r"), input);

        Assert.Null(rewrite.Refusal);
        Assert.Null(rewrite.Description);
        Assert.Equal("it kept its own description", rewrite.Note);
        Assert.Null(merge.Refusal);
        Assert.Null(merge.Description);
        Assert.Equal("it kept its own description", merge.Note);
    }

    /// <summary>A merge whose kept file has a broken description takes the new one.</summary>
    [Fact]
    public void A7_MergeWithABrokenDescription_TakesTheNewOne()
    {
        MemoryTidyInput input = Input(Shown("price.md", PriceBody, "tiny"), Shown("ship.md", ShipBody));

        MemoryTidyCheck check = Run(new MemoryTidyStep.Merge("price.md", ["ship.md"], "Pricing and shipping, read before quoting", MergedText, "r"), input);

        Assert.Null(check.Refusal);
        Assert.Equal("Pricing and shipping, read before quoting", check.Description);
        Assert.Null(check.Note);
    }

    /// <summary>With descriptions locked a describe is refused, and a rewrite or merge whose kept file has a missing or broken description is refused too.</summary>
    [Fact]
    public void A7_LockedFolder_WritesNoDescription()
    {
        MemoryTidyInput input = Input(locked: true, files: [Shown("price.md", PriceBody), Shown("none.md", ShipBody, null)]);

        MemoryTidyCheck describe = Run(new MemoryTidyStep.Describe("none.md", "Shipping rules, read before shipping", "r"), input);
        MemoryTidyCheck rewrite = Run(new MemoryTidyStep.Rewrite("none.md", null, ShipBody, "r"), input);
        MemoryTidyCheck merge = Run(new MemoryTidyStep.Merge("none.md", ["price.md"], null, MergedText, "r"), input);
        MemoryTidyCheck fine = Run(new MemoryTidyStep.Rewrite("price.md", "Another one, read before quoting", PriceBody, "r"), input);

        Assert.Equal("descriptions do not change in this folder", describe.Refusal);
        Assert.Equal("descriptions do not change in this folder", rewrite.Refusal);
        Assert.Equal("descriptions do not change in this folder", merge.Refusal);
        Assert.Null(fine.Refusal);
        Assert.Null(fine.Description);
    }

    private static MemoryTidyCheck Run(MemoryTidyStep step, MemoryTidyInput input) => MemoryTidyChecks.Run(step, input, 0);

    private static MemoryTidyFile Shown(string name, string body, string? description = Good)
    {
        string text = description is null ? body + "\n" : MemoryFileRules.Compose(description, body);
        return new MemoryTidyFile(name, "v-" + name, text, new DateOnly(2026, 10, 1));
    }

    private static MemoryTidyInput Input(params MemoryTidyFile[] files) => Input(3, false, files);

    private static MemoryTidyInput Input(int maxDrops = 3, bool locked = false, params MemoryTidyFile[] files) =>
        new("agent:Nova", "the Agent Nova", new DateOnly(2026, 11, 22), [], files, new MemoryTidyLimits(12, maxDrops, 40000, locked), new MemoryTidyPassContext("p", "b", new NoJournal()));

    private sealed class NoJournal : IMemoryTidyJournal
    {
        public void Append(MemoryTidyEntry entry)
        {
        }
    }
}
