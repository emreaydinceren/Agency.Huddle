using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins where the hook blocks sit in a Turn's prompt (Spec section 6.7, the <c>BuildFramedPrompt</c> row): each block in
/// the order given and followed by one blank line, before the Library documents, and never on a Greeting or a Command.
/// </summary>
public sealed class BuildPromptHookBlocksTests
{
    private static WorkItem Message(IReadOnlyList<string>? hookBlocks) =>
        new("room-1", "Room", "Human", "hello", [], HookBlocks: hookBlocks);

    /// <summary>Two hook blocks come out in the order given, each followed by a blank line, ahead of the Message line.</summary>
    [Fact]
    public void HookBlocks_ComeInOrder_EachFollowedByABlankLine()
    {
        WorkItem item = BuildPromptHookBlocksTests.Message(["HOOK-A", "HOOK-B"]);

        string prompt = TurnPromptFramer.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("HOOK-A\n\nHOOK-B\n\n[Room: Room (id: room-1)] Human: hello", prompt);
    }

    /// <summary>A hook block sits before the Library documents block, which is still rendered after it.</summary>
    [Fact]
    public void HookBlocks_ComeBeforeLibraryDocuments()
    {
        LibraryDocumentItem document = new("E:\\Data\\notes.md", "pinned root \"Docs\"", "1 KB", null, false, 1024, false, LibraryDocumentDelivery.Path, null);
        WorkItem item = new(
            "room-1",
            "Room",
            "Human",
            "hello",
            [],
            LibraryDocuments: new LibraryDocumentsReport([document], NotListed: 0, MaxInlineBytes: 16384),
            HookBlocks: ["HOOK-A"]);

        string prompt = TurnPromptFramer.BuildPrompt(item, new FakePromptSource());

        // contains-ok: the library block's rendered text is not pinned here; the test asserts only that the hook block leads the prompt.
        Assert.StartsWith("HOOK-A\n\n", prompt, StringComparison.Ordinal);
        Assert.True(prompt.IndexOf("E:\\Data\\notes.md", StringComparison.Ordinal) > "HOOK-A\n\n".Length, prompt);
    }

    /// <summary>A null hook list leaves the prompt exactly as a Turn built without the property.</summary>
    [Fact]
    public void HookBlocks_Null_IsByteIdenticalToNoBlocks()
    {
        WorkItem withoutProperty = new("room-1", "Room", "Human", "hello", []);

        string withNull = TurnPromptFramer.BuildPrompt(BuildPromptHookBlocksTests.Message(null), new FakePromptSource());

        Assert.Equal(TurnPromptFramer.BuildPrompt(withoutProperty, new FakePromptSource()), withNull);
        Assert.Equal("[Room: Room (id: room-1)] Human: hello", withNull);
    }

    /// <summary>A Greeting is built exactly as before, even when hook blocks are present on it.</summary>
    [Fact]
    public void HookBlocks_NotOnAGreeting()
    {
        FakePromptSource prompts = new();
        WorkItem plain = new("room-1", "Room", string.Empty, string.Empty, [], WorkItemKind.Greeting);
        WorkItem hooked = new("room-1", "Room", string.Empty, string.Empty, [], WorkItemKind.Greeting, HookBlocks: ["HOOK-A"]);

        string prompt = TurnPromptFramer.BuildPrompt(hooked, prompts);

        Assert.Equal(TurnPromptFramer.BuildPrompt(plain, prompts), prompt);
        Assert.DoesNotContain("HOOK-A", prompt, StringComparison.Ordinal);
    }

    /// <summary>A Command is the bare command and nothing else, even when hook blocks are present on it.</summary>
    [Fact]
    public void HookBlocks_NotOnACommand()
    {
        WorkItem item = new(
            "room-1",
            "Room",
            "You",
            "@nova /compact",
            [],
            WorkItemKind.Command,
            Command: new AdapterCommandCall("compact", string.Empty),
            HookBlocks: ["HOOK-A"]);

        string prompt = TurnPromptFramer.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("/compact", prompt);
    }
}
