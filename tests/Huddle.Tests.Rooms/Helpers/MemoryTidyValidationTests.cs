using System.Text.Json;
using Agency.Huddle.App.Helpers.MemoryTidy;

namespace Agency.Huddle.Tests.Helpers;

/// <summary><see cref="MemoryTidyValidation"/>, <see cref="MemoryTidyStep.TryParse"/> and the Verdict's JSON shape (Memory Tidy spec §6.10, "The whole Verdict").</summary>
public sealed class MemoryTidyValidationTests
{
    private static readonly MemoryTidyLimits Limits = new(MaxActions: 12, MaxDrops: 3, MaxAnswerChars: 40_000, DescriptionsLocked: false);

    private static MemoryTidyVerdict Verdict(params MemoryTidyAction[] actions) => new(actions);

    private static MemoryTidyAction Drop(string file) => new("drop", file, null, null, null, null, "gone");

    private static MemoryTidyAction Rewrite(string file, string text = "short") => new("rewrite", file, null, null, null, text, "tighter");

    /// <summary>A Verdict with no actions list is refused: a missing answer is not a quiet "nothing to tidy".</summary>
    [Fact]
    public void Validate_NoActionsList_NeedsOne()
    {
        Assert.Equal("the answer needs an actions list", MemoryTidyValidation.Validate(new MemoryTidyVerdict(null), Limits));
    }

    /// <summary>An empty actions list is valid: it is the answer for a folder with nothing to tidy.</summary>
    [Fact]
    public void Validate_EmptyList_IsValid()
    {
        Assert.Null(MemoryTidyValidation.Validate(Verdict(), Limits));
    }

    /// <summary>An action word outside the four, or none, is refused; the four are read ignoring case.</summary>
    /// <param name="word">The action word.</param>
    /// <param name="valid">Whether the word is one of the four.</param>
    [Theory]
    [InlineData("create", false)]
    [InlineData("move", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("DROP", true)]
    [InlineData("Drop", true)]
    public void Validate_ActionWord_MustBeOneOfFour(string? word, bool valid)
    {
        string? problem = MemoryTidyValidation.Validate(Verdict(new MemoryTidyAction(word, "a.md", null, null, null, null, "why")), Limits);

        Assert.Equal(valid ? null : "an action must be merge, rewrite, describe or drop", problem);
    }

    /// <summary>Each action needs its own fields and a reason, and the problem names the word and the fields.</summary>
    /// <param name="word">The action word.</param>
    /// <param name="expected">The problem text.</param>
    /// <param name="field">Which field to leave out: file, into, from, text, description or reason.</param>
    [Theory]
    [InlineData("merge", "a merge needs into, from, text and a reason", "into")]
    [InlineData("merge", "a merge needs into, from, text and a reason", "from")]
    [InlineData("merge", "a merge needs into, from, text and a reason", "text")]
    [InlineData("merge", "a merge needs into, from, text and a reason", "reason")]
    [InlineData("rewrite", "a rewrite needs file, text and a reason", "file")]
    [InlineData("rewrite", "a rewrite needs file, text and a reason", "text")]
    [InlineData("rewrite", "a rewrite needs file, text and a reason", "reason")]
    [InlineData("describe", "a describe needs file, description and a reason", "file")]
    [InlineData("describe", "a describe needs file, description and a reason", "description")]
    [InlineData("describe", "a describe needs file, description and a reason", "reason")]
    [InlineData("drop", "a drop needs file and a reason", "file")]
    [InlineData("drop", "a drop needs file and a reason", "reason")]
    public void Validate_MissingField_NamesTheFields(string word, string expected, string field)
    {
        MemoryTidyAction full = new(word, "a.md", "a.md", ["b.md"], "A description of more than twenty characters", "body", "why");
        MemoryTidyAction action = field switch
        {
            "file" => full with { File = null },
            "into" => full with { Into = null },
            "from" => full with { From = null },
            "text" => full with { Text = null },
            "description" => full with { Description = " " },
            _ => full with { Reason = null },
        };

        Assert.Equal(expected, MemoryTidyValidation.Validate(Verdict(action), Limits));
    }

    /// <summary>A blank text is refused whole, since the writer would otherwise write an empty body.</summary>
    /// <param name="word">merge or rewrite.</param>
    /// <param name="text">The blank text.</param>
    [Theory]
    [InlineData("merge", "")]
    [InlineData("merge", "  \n ")]
    [InlineData("rewrite", "")]
    [InlineData("rewrite", "\t")]
    public void Validate_BlankText_IsRefused(string word, string text)
    {
        MemoryTidyAction action = new(word, "a.md", "a.md", ["b.md"], null, text, "why");

        Assert.Equal($"a {word} needs a text that is not empty", MemoryTidyValidation.Validate(Verdict(action), Limits));
    }

    /// <summary>More actions than the cap is refused, with the count and the most, grouped.</summary>
    [Fact]
    public void Validate_TooManyActions_NamesBothNumbers()
    {
        MemoryTidyAction[] actions = [.. Enumerable.Range(0, 15).Select(static n => new MemoryTidyAction("describe", $"f{n}.md", null, null, "A description of more than twenty characters", null, "why"))];

        Assert.Equal("the answer holds 15 actions; the most is 12", MemoryTidyValidation.Validate(Verdict(actions), Limits));
    }

    /// <summary>More drops than the cap is refused, even when the action count is within its own cap.</summary>
    [Fact]
    public void Validate_TooManyDrops_IsRefused()
    {
        string? problem = MemoryTidyValidation.Validate(Verdict(Drop("a.md"), Drop("b.md"), Drop("c.md"), Drop("d.md")), Limits);

        Assert.Equal("the answer holds 4 drops; the most is 3", problem);
    }

    /// <summary>All the text together over the cap is refused, with the numbers grouped by thousands whatever the culture.</summary>
    [Fact]
    public void Validate_AnswerTooLong_NamesBothNumbers()
    {
        string text = new('x', 26_000);

        string? problem = MemoryTidyValidation.Validate(Verdict(Rewrite("a.md", text), Rewrite("b.md", text)), Limits);

        Assert.Equal("the answer holds 52,000 characters of text; the most is 40,000", problem);
    }

    /// <summary>A file named in two actions is refused whole, matched ignoring case.</summary>
    [Fact]
    public void Validate_FileInTwoActions_IsRefused()
    {
        string? problem = MemoryTidyValidation.Validate(Verdict(Rewrite("pricing.md"), Drop("Pricing.md")), Limits);

        Assert.Equal("Pricing.md is used in two actions", problem);
    }

    /// <summary>A merge counts its kept file and its folded files: one folded into another action, or its own kept file again, is a repeat.</summary>
    /// <param name="into">The kept file.</param>
    /// <param name="from">The folded files.</param>
    /// <param name="other">A second action's file, or null for none.</param>
    [Theory]
    [InlineData("a.md", new[] { "A.MD" }, null)]
    [InlineData("a.md", new[] { "b.md", "b.md" }, null)]
    [InlineData("a.md", new[] { "b.md" }, "b.md")]
    [InlineData("a.md", new[] { "b.md" }, "a.md")]
    public void Validate_MergeFilesCounted_RepeatIsRefused(string into, string[] from, string? other)
    {
        List<MemoryTidyAction> actions = [new("merge", null, into, from, null, "body", "same topic")];
        if (other is not null)
        {
            actions.Add(Drop(other));
        }

        string? problem = MemoryTidyValidation.Validate(new MemoryTidyVerdict(actions), Limits);

        Assert.EndsWith("is used in two actions", problem, StringComparison.Ordinal);
    }

    /// <summary>A valid mix of the four kinds passes.</summary>
    [Fact]
    public void Validate_FourKinds_Valid()
    {
        MemoryTidyVerdict verdict = Verdict(
            new MemoryTidyAction("merge", null, "a.md", ["b.md"], null, "body", "same"),
            Rewrite("c.md"),
            new MemoryTidyAction("describe", "d.md", null, null, "A description of more than twenty characters", null, "why"),
            Drop("e.md"));

        Assert.Null(MemoryTidyValidation.Validate(verdict, Limits));
    }

    /// <summary>A reason over 200 characters is cut to 197 and an ellipsis; a short one is kept as written.</summary>
    [Fact]
    public void TryParse_LongReason_IsCut()
    {
        MemoryTidyStep? step = MemoryTidyStep.TryParse(new MemoryTidyAction("drop", "a.md", null, null, null, null, new string('r', 250)), out string? problem);

        Assert.Null(problem);
        Assert.Equal(string.Concat(new string('r', 197), "..."), step?.Reason);
        Assert.Equal(200, step?.Reason.Length);
    }

    /// <summary>A step lists the files it reads with the kept file first: a merge's <c>into</c>, then its <c>from</c>.</summary>
    [Fact]
    public void Sources_Merge_KeptFileFirst()
    {
        MemoryTidyStep? merge = MemoryTidyStep.TryParse(new MemoryTidyAction("Merge", null, "a.md", ["b.md", "c.md"], null, "body", "same"), out _);
        MemoryTidyStep? drop = MemoryTidyStep.TryParse(Drop("e.md"), out _);

        Assert.Equal(["a.md", "b.md", "c.md"], merge?.Sources);
        Assert.Equal(["e.md"], drop?.Sources);
        Assert.IsType<MemoryTidyStep.Merge>(merge);
        Assert.IsType<MemoryTidyStep.Drop>(drop);
    }

    /// <summary>A reply with the property names in any case is read, and a description left out stays null.</summary>
    [Fact]
    public void Parse_AnyCaseNames_AreRead()
    {
        MemoryTidyVerdict? verdict = JsonSerializer.Deserialize("""{"Actions": [{"Action": "Rewrite", "File": "a.md", "Text": "t", "Reason": "r"}]}""", MemoryTidyJsonContext.Default.MemoryTidyVerdict);

        MemoryTidyAction action = Assert.Single(verdict?.Actions ?? []);
        Assert.Equal("Rewrite", action.Action);
        Assert.Null(action.Description);
        Assert.Null(MemoryTidyValidation.Validate(verdict!, Limits));
    }

    /// <summary>An object with no <c>actions</c> does not parse (the runner turns the exception into Unparseable), and an empty list does.</summary>
    [Fact]
    public void Parse_NoActionsProperty_Throws()
    {
        MemoryTidyVerdict? empty = JsonSerializer.Deserialize("""{"actions": []}""", MemoryTidyJsonContext.Default.MemoryTidyVerdict);

        _ = Assert.Throws<JsonException>(static () => JsonSerializer.Deserialize("""{"reason": "nothing"}""", MemoryTidyJsonContext.Default.MemoryTidyVerdict));
        Assert.Empty(empty?.Actions ?? [new MemoryTidyAction(null, null, null, null, null, null, null)]);
    }

    /// <summary>A prose answer holds no JSON object, so there is nothing to parse and the run is Unparseable.</summary>
    [Fact]
    public void Parse_ProseAnswer_Throws()
    {
        _ = Assert.ThrowsAny<JsonException>(static () => JsonSerializer.Deserialize("I merged the two pricing files and dropped the old one.", MemoryTidyJsonContext.Default.MemoryTidyVerdict));
    }
}
