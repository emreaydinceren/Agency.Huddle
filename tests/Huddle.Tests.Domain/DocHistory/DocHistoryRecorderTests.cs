using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Agency.Huddle.App;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Profile;
using static Agency.Huddle.Tests.DocHistory.DocHistoryTestPaths;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins <see cref="DocHistoryRecorder"/> (DocHistory spec §6.4): who each entry is written as, when, and when nothing is written.</summary>
public sealed class DocHistoryRecorderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 11, 2, 0, TimeSpan.Zero);

    /// <summary>A Human save is an <c>Edited</c> entry under the Human's live Name, with an empty summary, at the injected time.</summary>
    [Fact]
    public void HumanSaved_WritesEditedUnderTheHumansName()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());

        recorder.HumanSaved(Document("findings.md"));

        Assert.Equal([new DocHistoryEntry(Now, "Emre", DocActorKind.Human, DocChangeKind.Edited, string.Empty)], store.Read(Document("findings.md")));
    }

    /// <summary>A Human create is a <c>Created</c> entry.</summary>
    [Fact]
    public void HumanCreated_WritesCreated()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());

        recorder.HumanCreated(Document("findings.md"));

        Assert.Equal(DocChangeKind.Created, Assert.Single(store.Read(Document("findings.md"))).Kind);
    }

    /// <summary>A file that is not Markdown has no history, and a folder has none either.</summary>
    [Fact]
    public void HumanSaved_NotMarkdown_WritesNothing()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());

        recorder.HumanSaved(Document("notes.txt"));
        recorder.HumanSaved(Folder("Research.md"));

        Assert.Empty(store.Read(Document("notes.txt")));
        Assert.Empty(store.Read(Document("Research.md")));
    }

    /// <summary>With History switched off, a Human save writes nothing.</summary>
    [Fact]
    public void HumanSaved_Disabled_WritesNothing()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(Options.Create(new TeamOptions { DataDir = dataDir.Path, DocHistory = { Enabled = false } }));

        recorder.HumanSaved(Document("findings.md"));

        Assert.Empty(store.Read(Document("findings.md")));
    }

    /// <summary>An Agent's entry carries the Name it was given and its own kind and summary.</summary>
    [Fact]
    public void AgentLogged_WritesUnderTheAgentsName()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());

        bool logged = recorder.AgentLogged(Document("findings.md"), "Ruth", DocChangeKind.Reviewed, "Verified 41 citations");

        Assert.True(logged);
        Assert.Equal([new DocHistoryEntry(Now, "Ruth", DocActorKind.Agent, DocChangeKind.Reviewed, "Verified 41 citations")], store.Read(Document("findings.md")));
    }

    /// <summary>A save whose history cannot be written is logged and does not throw: the document matters more than its history.</summary>
    [Fact]
    public void HumanSaved_WriteFails_DoesNotThrow()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());
        Directory.CreateDirectory(store.FileFor(Document("findings.md")));

        recorder.HumanSaved(Document("findings.md"));

        Assert.True(Directory.Exists(store.FileFor(Document("findings.md"))));
    }

    /// <summary>An Agent's entry that cannot be written is reported as not logged.</summary>
    [Fact]
    public void AgentLogged_WriteFails_ReturnsFalse()
    {
        using TempDataDir dataDir = new();
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(dataDir.Options());
        Directory.CreateDirectory(store.FileFor(Document("findings.md")));

        Assert.False(recorder.AgentLogged(Document("findings.md"), "Ruth", DocChangeKind.Reviewed, "x"));
    }

    /// <summary>A move carries the history even with History off, so turning it back on finds the history where the document is.</summary>
    [Fact]
    public void Moved_CarriesHistoryEvenWhenDisabled()
    {
        using TempDataDir dataDir = new();
        TeamOptions options = new() { DataDir = dataDir.Path };
        (DocHistoryRecorder recorder, DocHistoryStore store) = Build(Options.Create(options));
        recorder.HumanSaved(Document("a.md"));

        recorder.Moved(Document("a.md"), Document("b.md"));
        options.DocHistory.Enabled = false;
        recorder.Moved(Document("b.md"), Document("c.md"));

        Assert.Empty(store.Read(Document("b.md")));
        Assert.Single(store.Read(Document("c.md")));
    }

    /// <summary>Builds a recorder with a fixed clock and the Human named Emre.</summary>
    /// <param name="options">The bound options.</param>
    /// <returns>The recorder and its store.</returns>
    private static (DocHistoryRecorder Recorder, DocHistoryStore Store) Build(IOptions<TeamOptions> options)
    {
        DocHistoryStore store = new(options, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, options, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"), new FakeTimeProvider(Now));
        return (recorder, store);
    }
}
