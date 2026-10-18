using System.Text.RegularExpressions;
using Agency.Huddle.App.Skills.Ui;
using Agency.Huddle.SkillPacks.Checking;
using Agency.Huddle.SkillPacks.Resolving;

namespace Agency.Huddle.Tests.Ui.SkillPacks;

/// <summary>Pins the guidance text of Settings › Skills (Skill Packs spec §6.9.12): the intros' shape, a sentence for every
/// control, every check code composing with its hint and the code last, and no "simply", "just" or "easy".</summary>
public sealed partial class GuidanceTextTests
{
    /// <summary>The three intros with the word each must define first.</summary>
    public static TheoryData<string, string> Intros => new()
    {
        { SkillGuide.SkillsIntro, "A Skill is" },
        { SkillGuide.PacksIntro, "A Pack is" },
        { SkillGuide.SourcesIntro, "A Source is" },
    };

    /// <summary>Each intro is three to five sentences, defines its word in the first, says what Huddle never does, and has
    /// no link and no <c>.md</c> path other than SKILLS.md, which the Human sees in the Library.</summary>
    [Theory]
    [MemberData(nameof(Intros))]
    public void Intro_HasTheShapeTheSpecSets(string intro, string definition)
    {
        ArgumentNullException.ThrowIfNull(intro);
        int sentences = SentenceEnd().Count(intro);

        Assert.InRange(sentences, 3, 5);
        Assert.StartsWith(definition, intro, StringComparison.Ordinal); // contains-ok: the rule is that the first words define the term
        Assert.Contains("never", intro, StringComparison.Ordinal); // contains-ok: the rule is that the intro says what Huddle never does, in any words
        Assert.DoesNotContain("http", intro, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(MarkdownPath(), intro.Replace("SKILLS.md", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>Every control in the button table has a sentence ending in a full stop.</summary>
    [Fact]
    public void ButtonSentences_EveryControlHasOne()
    {
        Assert.NotEmpty(SkillGuide.ButtonSentences);
        Assert.All(SkillGuide.ButtonSentences, static pair => Assert.EndsWith(".", pair.Value, StringComparison.Ordinal)); // contains-ok: the rule is one whole sentence each
        Assert.Throws<KeyNotFoundException>(static () => SkillGuide.For("No such control"));
    }

    /// <summary>Every code in <see cref="CheckCatalog.All"/> composes with its fix hint and ends with the code in brackets.</summary>
    [Fact]
    public void ProblemText_EveryCode_ComposesWithItsHintAndTheCodeLast()
    {
        foreach (CheckInfo check in CheckCatalog.All)
        {
            string text = SkillProblemText.Compose(new Finding(check.Severity, check.Code, "packs/x.skillpack.json", "Something is wrong"));

            Assert.False(string.IsNullOrWhiteSpace(check.FixHint), $"{check.Code} has no fix hint");
            Assert.Contains(check.FixHint.Trim().TrimEnd('.'), text, StringComparison.Ordinal); // contains-ok: all 46 codes; the exact form is pinned by ProblemText_Compose_ExactText
            Assert.EndsWith($"({check.Code})", text, StringComparison.Ordinal); // contains-ok: the rule is that the code comes last
        }
    }

    /// <summary>The warnings that block nothing say so, and a per-computer path with no entry names the computer.</summary>
    [Fact]
    public void ProblemText_AddsTheTwoUiSentences()
    {
        string warning = SkillProblemText.Compose(Finding.Warning("S008", "skills/x", "Scripts"));
        string perComputer = SkillProblemText.Compose(Finding.Error("R009", "SKILLS.md", "No path"), "DESK-01");

        Assert.Contains("Nothing is blocked. Open Details to see the files.", warning, StringComparison.Ordinal); // contains-ok: the UI addition, beside a hint pinned elsewhere
        Assert.Contains("There is no entry for DESK-01.", perComputer, StringComparison.Ordinal); // contains-ok: the UI addition, beside a hint pinned elsewhere
        Assert.EndsWith("(R009)", perComputer, StringComparison.Ordinal); // contains-ok: the code stays last after the addition
    }

    /// <summary>The composed text is the message, the catalog's hint and the code in brackets, in that order, word for word.</summary>
    [Fact]
    public void ProblemText_Compose_ExactText()
    {
        string text = SkillProblemText.Compose(Finding.Error("P005", "packs/x.skillpack.json", "appliesTo lists accountant"));

        Assert.Equal($"appliesTo lists accountant. {CheckCatalog.HintFor("P005")} (P005)", text);
    }

    /// <summary>No intro, state sentence or button sentence says "simply", "just" or "easy".</summary>
    [Fact]
    public void NoText_SaysSimplyJustOrEasy()
    {
        IEnumerable<string> texts = [
            SkillGuide.SkillsIntro, SkillGuide.PacksIntro, SkillGuide.SourcesIntro,
            .. Enum.GetValues<ItemState>().Select(static state => SkillStateText.For(state).Sentence),
            SkillStateText.Hidden.Sentence, SkillStateText.ModifiedLocally.Sentence,
            .. SkillGuide.ButtonSentences.Values];

        Assert.All(texts, static text => Assert.DoesNotMatch(Hedge(), text));
    }

    [GeneratedRegex(@"[.!?](\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"\b\w+\.md\b", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownPath();

    [GeneratedRegex(@"\b(simply|just|easy|easily)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Hedge();
}
