using Agency.Huddle.App.Sidebar;
using Agency.Huddle.Contracts;

namespace Agency.Huddle.Tests.Sidebar;

/// <summary>Pins <see cref="RoomMentions.Collect"/>, the one function both Room panels read (Spec: Tasks and Documents in this Room).</summary>
public sealed class RoomMentionsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a Message with the given snapshot references, or none recorded when null.</summary>
    private static ChatMessage Message(int number, string text, IReadOnlyList<MessageReference>? references)
    {
        return new ChatMessage($"m{number}", RoomMentionsTests.Start.AddMinutes(number), "u1", "Ann", text)
        {
            References = references,
        };
    }

    /// <summary>Builds a Task reference snapshot.</summary>
    private static MessageReference TaskRef(string id)
    {
        return new MessageReference(ReferenceKind.Task, id, id);
    }

    /// <summary>The distinct Tasks come back once each, the most recently mentioned first.</summary>
    [Fact]
    public void Collect_Tasks_DistinctByKey_MostRecentFirst()
    {
        List<ChatMessage> messages =
        [
            RoomMentionsTests.Message(1, "a", [RoomMentionsTests.TaskRef("TSK-1")]),
            RoomMentionsTests.Message(2, "b", [RoomMentionsTests.TaskRef("TSK-2")]),
            RoomMentionsTests.Message(3, "c", [RoomMentionsTests.TaskRef("TSK-3")]),
            RoomMentionsTests.Message(4, "d", [RoomMentionsTests.TaskRef("TSK-1")]),
        ];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.Task);

        Assert.Equal(["TSK-1", "TSK-3", "TSK-2"], result.Select(mention => mention.Key).ToArray());
    }

    /// <summary>The latest Message id and the number of Messages (not occurrences) are kept per key.</summary>
    [Fact]
    public void Collect_KeepsTheLatestMessageIdAndCount()
    {
        List<ChatMessage> messages =
        [
            RoomMentionsTests.Message(1, "a", [RoomMentionsTests.TaskRef("TSK-1")]),
            RoomMentionsTests.Message(2, "b", [RoomMentionsTests.TaskRef("TSK-2")]),
            RoomMentionsTests.Message(3, "c", [RoomMentionsTests.TaskRef("TSK-1"), RoomMentionsTests.TaskRef("TSK-1")]),
        ];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.Task);

        RoomMention first = Assert.Single(result, mention => string.Equals(mention.Key, "TSK-1", StringComparison.Ordinal));
        Assert.Equal("m3", first.LatestMessageId);
        Assert.Equal(2, first.Count);
        Assert.Equal("TSK-1", result[0].Key);
        RoomMention second = Assert.Single(result, mention => string.Equals(mention.Key, "TSK-2", StringComparison.Ordinal));
        Assert.Equal("m2", second.LatestMessageId);
        Assert.Equal(1, second.Count);
    }

    /// <summary>A Message recorded before snapshots existed (References null) is read with the scanner.</summary>
    [Fact]
    public void Collect_OldMessageWithoutReferences_FallsBackToTheScanner()
    {
        List<ChatMessage> messages = [RoomMentionsTests.Message(1, "please look at TSK-9 today", null)];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.Task);

        RoomMention mention = Assert.Single(result);
        Assert.Equal("TSK-0009", mention.Key);
        Assert.Equal("TSK-0009", mention.Label);
        Assert.Equal("m1", mention.LatestMessageId);
        Assert.Equal(1, mention.Count);
    }

    /// <summary>An empty References list means "none", so the text is not scanned.</summary>
    [Fact]
    public void Collect_MessageWithEmptyReferences_DoesNotScan()
    {
        List<ChatMessage> messages = [RoomMentionsTests.Message(1, "see TSK-9", [])];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.Task);

        Assert.Empty(result);
    }

    /// <summary>A file's label is the snapshot's Label, not the file name of the key.</summary>
    [Fact]
    public void Collect_Files_UsesLabelFromSnapshot()
    {
        MessageReference file = new(ReferenceKind.LibraryFile, @"C:\Docs\plan.md", "The Plan");
        List<ChatMessage> messages = [RoomMentionsTests.Message(1, "x", [file])];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.LibraryFile);

        RoomMention mention = Assert.Single(result);
        Assert.Equal(@"C:\Docs\plan.md", mention.Key);
        Assert.Equal("The Plan", mention.Label);
    }

    /// <summary>References of another kind are ignored.</summary>
    [Fact]
    public void Collect_OtherKinds_AreIgnored()
    {
        List<ChatMessage> messages =
        [
            RoomMentionsTests.Message(1, "x", [new MessageReference(ReferenceKind.Room, "r1", "General"), RoomMentionsTests.TaskRef("TSK-1")]),
        ];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.LibraryFile);

        Assert.Empty(result);
    }

    /// <summary>A scanned file's label is the text after the last separator, whichever OS the path was written for.</summary>
    /// <param name="path">The path as written in the Message.</param>
    [Theory]
    [InlineData(@"C:\notes\plan.md")]
    [InlineData("/home/u/notes/plan.md")]
    public void Collect_OldMessageFile_LabelIsTheFileName(string path)
    {
        List<ChatMessage> messages = [RoomMentionsTests.Message(1, $"open `{path}` please", null)];

        IReadOnlyList<RoomMention> result = RoomMentions.Collect(messages, ReferenceKind.LibraryFile);

        RoomMention mention = Assert.Single(result);
        Assert.Equal("plan.md", mention.Label);
    }

    /// <summary>No Messages, no mentions.</summary>
    [Fact]
    public void Collect_Empty_IsEmpty()
    {
        IReadOnlyList<RoomMention> result = RoomMentions.Collect([], ReferenceKind.Task);

        Assert.Empty(result);
    }
}
