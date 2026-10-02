using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Acp.Sessions;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Acp.Fakes;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>
/// Pins how <see cref="RoomSession.BuildPrompt"/> words each Library document by how it reaches the
/// model (design §6.5): a Block says the file is included with the message, an Unavailable image says the
/// model cannot see it, and a Path line is exactly what it was before Prompt blocks existed.
/// </summary>
public sealed class BuildPromptLibraryDeliveryTests
{
    private const string PngPath = @"E:\Data\Design\checkout.png";

    private static WorkItem ItemWith(params LibraryDocumentItem[] documents) =>
        new("room-1", "Nova & You", "You", "hello there", [])
        {
            LibraryDocuments = new LibraryDocumentsReport(documents, NotListed: 0, MaxInlineBytes: 16384),
        };

    private static LibraryDocumentItem Document(
        LibraryDocumentDelivery delivery,
        string path = PngPath,
        string? text = null,
        bool truncated = false,
        bool tooLarge = false,
        AgentPromptBlock? block = null) =>
        new(path, "pinned root \"Design\"", "412 KB", text, truncated, 421888, tooLarge, delivery, block);

    private static string Lines(string prompt) =>
        string.Join('\n', prompt.Split('\n').Where(line => line.StartsWith("- ", StringComparison.Ordinal)));

    /// <summary>A Block says the file is included with the message, in the wording of <c>turn.libraryDocIncluded</c>.</summary>
    [Fact]
    public void BuildPrompt_BlockDelivery_SaysIncludedWithThisMessage()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Block, block: new AgentImageBlock("image/png", new byte[] { 1 })));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal(
            "- E:\\Data\\Design\\checkout.png (pinned root \"Design\", 412 KB): included with this message",
            Lines(prompt));
    }

    /// <summary>An image the model cannot be given says so, in the wording of <c>turn.libraryImageUnavailable</c>.</summary>
    [Fact]
    public void BuildPrompt_UnavailableDelivery_SaysAnImageYouCannotSee()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Unavailable));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal(
            "- E:\\Data\\Design\\checkout.png (pinned root \"Design\", 412 KB): an image you cannot see",
            Lines(prompt));
    }

    /// <summary>A Path line is exactly today's: path, location, size and nothing else.</summary>
    [Fact]
    public void BuildPrompt_PathDelivery_IsTodaysLine()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Path));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("- E:\\Data\\Design\\checkout.png (pinned root \"Design\", 412 KB)", Lines(prompt));
    }

    /// <summary>A block's line wins over the too-large line: an image of 2.5 MiB is over the editable limit and still travels with the message.</summary>
    [Fact]
    public void BuildPrompt_BlockOfALargeFile_SaysIncludedNotTooLarge()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Block, tooLarge: true, block: new AgentImageBlock("image/png", new byte[] { 1 })));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal(
            "- E:\\Data\\Design\\checkout.png (pinned root \"Design\", 412 KB): included with this message",
            Lines(prompt));
    }

    /// <summary>An unavailable image that is also large says it cannot be seen, not that it is too large to read.</summary>
    [Fact]
    public void BuildPrompt_UnavailableOfALargeFile_SaysCannotSee()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Unavailable, tooLarge: true));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal(
            "- E:\\Data\\Design\\checkout.png (pinned root \"Design\", 412 KB): an image you cannot see",
            Lines(prompt));
    }

    /// <summary>A Path image that is too large still says so, exactly as before.</summary>
    [Fact]
    public void BuildPrompt_PathOfALargeFile_StillSaysTooLarge()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Path, tooLarge: true));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal("- pinned root \"Design\": E:\\Data\\Design\\checkout.png (too large to include: 412 KB; open it with your file tools)", Lines(prompt));
    }

    /// <summary>A text resource is not also fenced inline, since its text rides in the block; the cut note, when it was cut, follows its line.</summary>
    [Fact]
    public void BuildPrompt_ResourceBlockCut_HasNoFenceAndKeepsTheCutNote()
    {
        WorkItem item = ItemWith(Document(
            LibraryDocumentDelivery.Block,
            path: @"E:\Data\notes.md",
            text: null,
            truncated: true,
            block: new AgentTextResourceBlock("file:///E:/Data/notes.md", "text/markdown", "# cut")));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.DoesNotContain("```", prompt, StringComparison.Ordinal);
        Assert.Contains("included with this message\n(cut at 16384 bytes; the file is 412 KB.)", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary>A resource block that was not cut has no cut note.</summary>
    [Fact]
    public void BuildPrompt_ResourceBlockNotCut_HasNoCutNote()
    {
        WorkItem item = ItemWith(Document(
            LibraryDocumentDelivery.Block,
            path: @"E:\Data\notes.md",
            block: new AgentTextResourceBlock("file:///E:/Data/notes.md", "text/markdown", "# t")));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.DoesNotContain("(cut at", prompt, StringComparison.Ordinal);
    }

    /// <summary>Two documents with different deliveries each get their own wording, in order.</summary>
    [Fact]
    public void BuildPrompt_MixedDeliveries_EachLineIsWordedByItsOwnDelivery()
    {
        WorkItem item = ItemWith(
            Document(LibraryDocumentDelivery.Block, @"E:\a.png", block: new AgentImageBlock("image/png", new byte[] { 1 })),
            Document(LibraryDocumentDelivery.Path, @"E:\b.png"),
            Document(LibraryDocumentDelivery.Unavailable, @"E:\c.png"));

        string prompt = RoomSession.BuildPrompt(item, new FakePromptSource());

        Assert.Equal(
            [
                "- E:\\a.png (pinned root \"Design\", 412 KB): included with this message",
                "- E:\\b.png (pinned root \"Design\", 412 KB)",
                "- E:\\c.png (pinned root \"Design\", 412 KB): an image you cannot see",
            ],
            Lines(prompt).Split('\n'));
    }

    /// <summary>An override of <c>turn.libraryDocIncluded</c> takes effect, so the wording stays configuration (ADR-0007).</summary>
    [Fact]
    public void BuildPrompt_IncludedKeyOverridden_UsesTheOverride()
    {
        FakePromptSource prompts = new();
        prompts.SetOverride("turn.libraryDocIncluded", "ATTACHED {{path}} {{location}} {{size}}");
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Block, block: new AgentImageBlock("image/png", new byte[] { 1 })));

        string prompt = RoomSession.BuildPrompt(item, prompts);

        Assert.Contains("ATTACHED E:\\Data\\Design\\checkout.png pinned root \"Design\" 412 KB", prompt, StringComparison.Ordinal); // contains-ok: prompt text, not markup
    }

    /// <summary><see cref="RoomSession.BuildPromptBlocks"/> returns the blocks of the Turn's documents in order, and nothing for Path and Unavailable.</summary>
    [Fact]
    public void BuildPromptBlocks_ReturnsBlocksInOrder()
    {
        AgentPromptBlock first = new AgentImageBlock("image/png", new byte[] { 1 });
        AgentPromptBlock second = new AgentTextResourceBlock("file:///E:/n.md", "text/markdown", "x");
        WorkItem item = ItemWith(
            Document(LibraryDocumentDelivery.Block, @"E:\a.png", block: first),
            Document(LibraryDocumentDelivery.Path, @"E:\b.png"),
            Document(LibraryDocumentDelivery.Unavailable, @"E:\c.png"),
            Document(LibraryDocumentDelivery.Block, @"E:\n.md", block: second));

        IReadOnlyList<AgentPromptBlock> blocks = RoomSession.BuildPromptBlocks(item);

        Assert.Equal([first, second], blocks);
    }

    /// <summary>A Turn with no Library documents has no blocks.</summary>
    [Fact]
    public void BuildPromptBlocks_NoLibraryDocuments_IsEmpty()
    {
        WorkItem item = new("room-1", "Room", "You", "hi", []);

        Assert.Empty(RoomSession.BuildPromptBlocks(item));
    }

    /// <summary>A Command Turn never carries a block, even if its work item were handed a report.</summary>
    [Fact]
    public void BuildPromptBlocks_CommandTurn_IsEmpty()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Block, block: new AgentImageBlock("image/png", new byte[] { 1 })))
            with { Kind = WorkItemKind.Command, Command = new AdapterCommandCall("compact", string.Empty) };

        Assert.Empty(RoomSession.BuildPromptBlocks(item));
    }

    /// <summary>A Greeting Turn never carries a block.</summary>
    [Fact]
    public void BuildPromptBlocks_GreetingTurn_IsEmpty()
    {
        WorkItem item = ItemWith(Document(LibraryDocumentDelivery.Block, block: new AgentImageBlock("image/png", new byte[] { 1 })))
            with { Kind = WorkItemKind.Greeting };

        Assert.Empty(RoomSession.BuildPromptBlocks(item));
    }
}
