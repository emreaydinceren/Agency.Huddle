using Microsoft.Extensions.Logging;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Elicitation;

namespace Agency.Huddle.Tests.Acp.Sessions;

/// <summary>Pins <c>LastCatchUpStore</c>: the <c>{DataDir}/room-sessions/last-catch-up/{Name}/{roomId}.md|.json</c> folder (LR-R22, LR-E10, LR-E11, corrections C3 and C12).</summary>
public sealed class LastCatchUpStoreTests
{
    private static string Folder(TempDataDir dir, string name) =>
        Path.Combine(dir.Path, "room-sessions", "last-catch-up", name);

    private static Agency.Huddle.App.Acp.Sessions.LastCatchUpStore Create(TempDataDir dir, RecordingLogger<Agency.Huddle.App.Acp.Sessions.LastCatchUpStore>? logger = null) =>
        new(dir.Options(), logger ?? new RecordingLogger<Agency.Huddle.App.Acp.Sessions.LastCatchUpStore>());

    private static FreshStartNote Note(string roomId, string reason = "asked", int count = 12) =>
        new(roomId, reason, count, 3400, ReadFailed: false);

    /// <summary>A write then a read returns the exact text, with CRLF and LF both kept.</summary>
    [Fact]
    public void Write_ThenRead_ReturnsExactTextKeepingLineEndings()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        const string Text = "line one\r\nline two\nline three\r\n";

        store.Write("Nova", "room1", Text);

        Assert.Equal(Text, store.Read("Nova", "room1"));
        Assert.True(File.Exists(Path.Combine(Folder(dir, "Nova"), "room1.md")));
    }

    /// <summary>A second write for the same Name and Room replaces the first.</summary>
    [Fact]
    public void Write_Twice_ReplacesTheFirstText()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);

        store.Write("Nova", "room1", "first");
        store.Write("Nova", "room1", "second");

        Assert.Equal("second", store.Read("Nova", "room1"));
    }

    /// <summary>Reading a Name or Room nothing was written for is null.</summary>
    [Fact]
    public void Read_NothingWritten_ReturnsNull()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "text");

        Assert.Null(store.Read("Nova", "room2"));
        Assert.Null(store.Read("Orion", "room1"));
    }

    /// <summary>A note written then read back through <c>ReadNotes</c> equals the original record, and lands in <c>{roomId}.json</c> beside the <c>.md</c>.</summary>
    [Fact]
    public void WriteNote_ThenReadNotes_RoundTripsEqual()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        FreshStartNote note = Note("room1");

        store.WriteNote("Nova", note);

        (string Name, FreshStartNote Note) entry = Assert.Single(store.ReadNotes("room1"));
        Assert.Equal("Nova", entry.Name);
        Assert.Equal(note, entry.Note);
        Assert.True(File.Exists(Path.Combine(Folder(dir, "Nova"), "room1.json")));
    }

    /// <summary>ReadNotes returns one entry per Name that has a note for the Room, and none from other Rooms.</summary>
    [Fact]
    public void ReadNotes_SeveralNames_ReturnsOneEntryPerName()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.WriteNote("Nova", Note("room1", count: 1));
        store.WriteNote("Orion", Note("room1", count: 2));
        store.WriteNote("Orion", Note("room2", count: 3));

        List<(string Name, FreshStartNote Note)> notes = [.. store.ReadNotes("room1")];

        Assert.Equal(2, notes.Count);
        Assert.Equal(1, notes.Single(static n => n.Name == "Nova").Note.MessageCount);
        Assert.Equal(2, notes.Single(static n => n.Name == "Orion").Note.MessageCount);
    }

    /// <summary>A corrupt <c>.json</c> is ignored with one Warning while a good one is still returned.</summary>
    [Fact]
    public void ReadNotes_CorruptJson_IgnoredWithOneWarning()
    {
        using TempDataDir dir = new();
        RecordingLogger<Agency.Huddle.App.Acp.Sessions.LastCatchUpStore> logger = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir, logger);
        store.WriteNote("Nova", Note("room1"));
        Directory.CreateDirectory(Folder(dir, "Orion"));
        File.WriteAllText(Path.Combine(Folder(dir, "Orion"), "room1.json"), "{ not json");

        (string Name, FreshStartNote Note) entry = Assert.Single(store.ReadNotes("room1"));

        Assert.Equal("Nova", entry.Name);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>RemoveNote deletes the <c>.json</c> only and leaves the <c>.md</c>.</summary>
    [Fact]
    public void RemoveNote_LeavesTheReread()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "reread text");
        store.WriteNote("Nova", Note("room1"));

        store.RemoveNote("Nova", "room1");

        Assert.Empty(store.ReadNotes("room1"));
        Assert.False(File.Exists(Path.Combine(Folder(dir, "Nova"), "room1.json")));
        Assert.Equal("reread text", store.Read("Nova", "room1"));
    }

    /// <summary>Rename moves the folder, so a read under the old Name is null and under the new Name returns the text, note included.</summary>
    [Fact]
    public void Rename_MovesTheFolder()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "text");
        store.WriteNote("Nova", Note("room1"));

        store.Rename("Nova", "Orion");

        Assert.Null(store.Read("Nova", "room1"));
        Assert.Equal("text", store.Read("Orion", "room1"));
        Assert.Equal("Orion", Assert.Single(store.ReadNotes("room1")).Name);
        Assert.False(Directory.Exists(Folder(dir, "Nova")));
    }

    /// <summary>Rename onto a Name whose folder already exists replaces that folder; nothing of the old target survives.</summary>
    [Fact]
    public void Rename_OntoExistingTarget_OverwritesIt()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "from nova");
        store.Write("Orion", "room1", "old orion");
        store.Write("Orion", "room2", "old orion room2");

        store.Rename("Nova", "Orion");

        Assert.Null(store.Read("Nova", "room1"));
        Assert.Equal("from nova", store.Read("Orion", "room1"));
        Assert.Null(store.Read("Orion", "room2"));
    }

    /// <summary>A case-only rename (<c>Nova</c> to <c>nova</c>) keeps the data under the new spelling.</summary>
    [Fact]
    public void Rename_CaseOnly_KeepsTheData()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "text");

        store.Rename("Nova", "nova");

        Assert.Equal("text", store.Read("nova", "room1"));
        string[] folders = [.. Directory.GetDirectories(Path.Combine(dir.Path, "room-sessions", "last-catch-up")).Select(static d => Path.GetFileName(d))];
        Assert.Equal(["nova"], folders);
    }

    /// <summary>Remove deletes the Name's whole folder.</summary>
    [Fact]
    public void Remove_DeletesTheFolder()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        store.Write("Nova", "room1", "text");
        store.WriteNote("Nova", Note("room1"));
        store.Write("Orion", "room1", "keep");

        store.Remove("Nova");

        Assert.False(Directory.Exists(Folder(dir, "Nova")));
        Assert.Null(store.Read("Nova", "room1"));
        Assert.Equal("keep", store.Read("Orion", "room1"));
    }

    /// <summary>RemoveRoom deletes the Room's <c>.md</c> and <c>.json</c> under every Name and leaves other Rooms' files.</summary>
    [Fact]
    public void RemoveRoom_DeletesThatRoomUnderEveryName()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);
        foreach (string name in new[] { "Nova", "Orion" })
        {
            store.Write(name, "room1", "one");
            store.WriteNote(name, Note("room1"));
            store.Write(name, "room2", "two");
            store.WriteNote(name, Note("room2"));
        }

        store.RemoveRoom("room1");

        Assert.Null(store.Read("Nova", "room1"));
        Assert.Null(store.Read("Orion", "room1"));
        Assert.Empty(store.ReadNotes("room1"));
        Assert.False(File.Exists(Path.Combine(Folder(dir, "Nova"), "room1.json")));
        Assert.Equal("two", store.Read("Nova", "room2"));
        Assert.Equal("two", store.Read("Orion", "room2"));
        Assert.Equal(2, store.ReadNotes("room2").Count);
    }

    /// <summary>A room id or Name that is not valid is refused with <see cref="ArgumentException"/> and nothing is written.</summary>
    [Theory]
    [InlineData("Nova", "../x")]
    [InlineData("Nova", "a/b")]
    [InlineData("Nova", "")]
    [InlineData("../x", "room1")]
    [InlineData("a/b", "room1")]
    [InlineData("", "room1")]
    public void Write_InvalidNameOrRoomId_ThrowsAndWritesNothing(string name, string roomId)
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);

        Assert.ThrowsAny<ArgumentException>(() => store.Write(name, roomId, "text"));
        Assert.ThrowsAny<ArgumentException>(() => store.WriteNote(name, Note(roomId)));
        Assert.ThrowsAny<ArgumentException>(() => store.Read(name, roomId));

        Assert.DoesNotContain(Directory.GetFileSystemEntries(dir.Path, "*", SearchOption.AllDirectories), static p => p.EndsWith(".md", StringComparison.Ordinal) || p.EndsWith(".json", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(dir.Path, "..", "x")) && File.Exists(Path.Combine(dir.Path, "..", "x", "room1.md")));
    }

    /// <summary>RemoveNote, Rename, Remove and RemoveRoom refuse an invalid Name or room id too.</summary>
    [Fact]
    public void OtherMembers_InvalidNameOrRoomId_Throw()
    {
        using TempDataDir dir = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir);

        Assert.ThrowsAny<ArgumentException>(() => store.RemoveNote("../x", "room1"));
        Assert.ThrowsAny<ArgumentException>(() => store.RemoveNote("Nova", "a/b"));
        Assert.ThrowsAny<ArgumentException>(() => store.Rename("Nova", "../x"));
        Assert.ThrowsAny<ArgumentException>(() => store.Rename("a/b", "Nova"));
        Assert.ThrowsAny<ArgumentException>(() => store.Remove("../x"));
        Assert.ThrowsAny<ArgumentException>(() => store.RemoveRoom("a/b"));
        Assert.ThrowsAny<ArgumentException>(() => store.ReadNotes("../x"));
    }

    /// <summary>An IO failure on write (the target path is a directory) is logged as one Warning and does not throw.</summary>
    [Fact]
    public void Write_IoFailure_LogsWarningAndDoesNotThrow()
    {
        using TempDataDir dir = new();
        RecordingLogger<Agency.Huddle.App.Acp.Sessions.LastCatchUpStore> logger = new();
        Agency.Huddle.App.Acp.Sessions.LastCatchUpStore store = Create(dir, logger);
        Directory.CreateDirectory(Path.Combine(Folder(dir, "Nova"), "room1.md"));

        Exception? thrown = Record.Exception(() => store.Write("Nova", "room1", "text"));

        Assert.Null(thrown);
        Assert.Single(logger.AtExactly(LogLevel.Warning));
    }
}
