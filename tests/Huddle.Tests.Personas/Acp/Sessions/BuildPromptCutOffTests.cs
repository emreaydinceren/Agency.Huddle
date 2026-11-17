using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Where the cut-off note sits in a Turn's prompt (Turn Queue spec, section 5.7): before the Message, and only on a cut-off second try.</summary>
public sealed class BuildPromptCutOffTests
{
    private const string Note =
        "Huddle stopped while you were answering the Message below, and this is a second try. " +
        "Your conversation may show what you had started: a tool call that had not returned may have finished, " +
        "may have stopped partway, or may still be running. Anything you did before it stopped " +
        "(a file, a Task, a post in this or another Room) may already be done: check before you do it again.";

    private const string Line = "[Room: Room (id: room-1)] Human: hello";

    private static WorkItem Item(Redelivery? redelivery) => new("room-1", "Room", "Human", "hello", [], Redelivery: redelivery);

    /// <summary>A cut-off Turn's prompt is the note, a blank line, then the ordinary prompt.</summary>
    [Fact]
    public void CutOff_PutsTheNoteAndABlankLineBeforeTheMessage()
    {
        string prompt = TurnPromptFramer.BuildPrompt(Item(Redelivery.CutOff), new FakePromptSource());

        Assert.Equal(Note + "\n\n" + Line, prompt);
    }

    /// <summary>A waiting re-delivery and an ordinary Turn are byte-identical to a Turn built without the property.</summary>
    [Fact]
    public void WaitingAndOrdinary_AreUnchanged()
    {
        FakePromptSource prompts = new();

        Assert.Equal(Line, TurnPromptFramer.BuildPrompt(Item(Redelivery.Waiting), prompts));
        Assert.Equal(Line, TurnPromptFramer.BuildPrompt(Item(null), prompts));
    }

    /// <summary>The note's default text is the catalog's and names no tool prefix.</summary>
    [Fact]
    public void Catalog_HoldsTheNoteWithoutAToolPrefix()
    {
        string text = PromptCatalog.Get("turn.cutOffNote").Default;

        Assert.Equal(Note, text);
        Assert.DoesNotContain("mcp__team__", text, StringComparison.Ordinal);
    }
}
