using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>The prompt of a Folded Turn (Turn Queue spec, section 6.4): the header and one line per Message, with one Message left byte-identical.</summary>
public sealed class BuildPromptFoldedTests
{
    private const string Label = "[Room: Room (id: room-1)]";

    private static FoldedMessage Folded(string id, string text) => new(id, "Human", text, null);

    private static WorkItem Item(params FoldedMessage[] folded) =>
        new("room-1", "Room", "Human", folded[0].Text, [], TriggerMessageId: folded[0].Id, Foldable: true, Folded: folded);

    private static MessagePosted Posted(IReadOnlyList<MemberInfo> members, string senderId = "h") =>
        new("room-1", "Room", new ChatMessage("m1", DateTimeOffset.UnixEpoch, senderId, "Human", "hi"), true, [], members, 0, 0);

    /// <summary>Two Messages render the header with the count and one line each.</summary>
    [Fact]
    public void TwoMessages_Golden()
    {
        string prompt = TurnPromptFramer.BuildPrompt(Item(Folded("a", "one"), Folded("b", "two")), new FakePromptSource());

        Assert.Equal(Label + " Since your last reply the Human sent 2 Messages. Answer them together, once:\nHuman: one\nHuman: two", prompt);
    }

    /// <summary>Three Messages render in order, oldest first.</summary>
    [Fact]
    public void ThreeMessages_Golden()
    {
        string prompt = TurnPromptFramer.BuildPrompt(Item(Folded("a", "x"), Folded("b", "y"), Folded("c", "z")), new FakePromptSource());

        Assert.Equal(Label + " Since your last reply the Human sent 3 Messages. Answer them together, once:\nHuman: x\nHuman: y\nHuman: z", prompt);
    }

    /// <summary>A fold of one Message is byte-identical to a plain Turn, even when <c>turn.message</c> is overridden (E-6).</summary>
    [Fact]
    public void OneMessage_IsByteIdentical_UnderAnOverride()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.message", "{{roomLabel}} <{{sender}}> {{text}}!");
        WorkItem plain = new("room-1", "Room", "Human", "one", [], TriggerMessageId: "a");

        string folded = TurnPromptFramer.BuildPrompt(Item(Folded("a", "one")), prompts);

        Assert.Equal(TurnPromptFramer.BuildPrompt(plain, prompts), folded);
        Assert.Equal(Label + " <Human> one!", folded);
    }

    /// <summary>The Catch-up of the folded items goes before the folded block, which stays last.</summary>
    [Fact]
    public void CatchUp_GoesBeforeTheFoldedBlock()
    {
        WorkItem item = Item(Folded("a", "one"), Folded("b", "two")) with { MissedMessages = [new CaughtUpMessage("Ana", "missed-line")], OwnPostLines = ["own-line"] };

        string prompt = TurnPromptFramer.BuildPrompt(item, new FakePromptSource());

        const string tail = "Answer them together, once:\nHuman: one\nHuman: two";
        Assert.Equal(tail, prompt[^tail.Length..]);
        Assert.True(prompt.IndexOf("missed-line", StringComparison.Ordinal) < prompt.IndexOf("own-line", StringComparison.Ordinal));
        Assert.True(prompt.IndexOf("own-line", StringComparison.Ordinal) < prompt.IndexOf("Since your last reply", StringComparison.Ordinal));
    }

    /// <summary>A folded prompt never starts with a slash, whatever the first Message says.</summary>
    [Fact]
    public void FoldedPrompt_NeverStartsWithASlash()
    {
        string prompt = TurnPromptFramer.BuildPrompt(Item(Folded("a", "/compact"), Folded("b", "/status")), new FakePromptSource());

        Assert.Equal("[", prompt[..1]);
    }

    /// <summary>The new prompts name no tool prefix and carry their placeholders.</summary>
    [Fact]
    public void Catalog_HoldsTheFoldedPrompts()
    {
        PromptDefinition header = PromptCatalog.Get("turn.foldedHeader");
        PromptDefinition line = PromptCatalog.Get("turn.foldedLine");

        Assert.Equal("{{roomLabel}} Since your last reply the Human sent {{count}} Messages. Answer them together, once:", header.Default);
        Assert.Equal("{{sender}}: {{text}}", line.Default);
        Assert.DoesNotContain("mcp__team__", header.Default, StringComparison.Ordinal);
        Assert.DoesNotContain("mcp__team__", line.Default, StringComparison.Ordinal);
    }

    /// <summary>Only the Human's ordinary Message in a one-to-one Room is Foldable, and the option switches it off.</summary>
    [Fact]
    public void IsFoldable_OneToOneHumanOnly_AndTheSwitch()
    {
        MemberInfo human = new("h", "Human", UserKind.Human);
        MemberInfo nova = new("n", "Nova", UserKind.Agent);
        MemberInfo ana = new("a", "Ana", UserKind.Agent);

        Assert.True(PersonaRunner.IsFoldable(Posted([human, nova]), true));
        Assert.False(PersonaRunner.IsFoldable(Posted([human, nova]), false));
        Assert.False(PersonaRunner.IsFoldable(Posted([human, nova, ana]), true));
        Assert.False(PersonaRunner.IsFoldable(Posted([human, nova], "n"), true));
    }
}
