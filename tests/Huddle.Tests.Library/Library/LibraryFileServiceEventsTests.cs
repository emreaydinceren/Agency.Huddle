using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the events <see cref="LibraryFileService"/> raises (S-A1, S-A2): a Human save, create, rename and move each raise once with their paths, and a refused one raises nothing.</summary>
public sealed class LibraryFileServiceEventsTests
{
    /// <summary>A successful save of a Markdown document raises HumanSaved once with the saved path.</summary>
    [Fact]
    public async Task WriteTextAsync_Markdown_RaisesHumanSavedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryEvents events = new();
        List<LibraryPath> saved = [];
        events.HumanSaved += saved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "updated", content.Format, ct);

        Assert.NotNull(result.Value);
        Assert.Equal(path.FullPath, Assert.Single(saved).FullPath);
    }

    /// <summary>A refused save raises nothing.</summary>
    [Fact]
    public async Task WriteTextAsync_Refused_RaisesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build(teamOptions => teamOptions.Library.MaxEditableBytes = 4);
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "abc");
        LibraryEvents events = new();
        List<LibraryPath> saved = [];
        events.HumanSaved += saved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);
        File.WriteAllText(Path.Combine(vault, "note.md"), "grew past the cap after it was read");

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "far longer than the cap", content.Format, ct);

        Assert.Null(result.Value);
        Assert.Empty(saved);
    }

    /// <summary>A HumanSaved subscriber that throws neither fails the save nor stops the next subscriber; the file is on disk.</summary>
    [Fact]
    public async Task WriteTextAsync_ThrowingSubscriber_SaveSucceedsAndNextSubscriberRuns()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "note.md"), "hello");
        LibraryEvents events = new();
        List<LibraryPath> saved = [];
        events.HumanSaved += _ => throw new InvalidOperationException("subscriber failed");
        events.HumanSaved += saved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryPath path = fixture.Resolve(vault, "note.md");
        LibraryDocumentContent content = await service.ReadAsync(path, ct);
        Assert.NotNull(content.Format);

        LibraryResult<LibraryEntry> result = await service.WriteTextAsync(path, "updated", content.Format, ct);

        Assert.NotNull(result.Value);
        Assert.Equal("updated", File.ReadAllText(Path.Combine(vault, "note.md")));
        Assert.Equal(path.FullPath, Assert.Single(saved).FullPath);
    }

    /// <summary>Creating a note raises HumanCreated once with the created path.</summary>
    [Fact]
    public async Task CreateFileAsync_RaisesHumanCreatedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        LibraryEvents events = new();
        List<LibraryPath> created = [];
        events.HumanCreated += created.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);

        LibraryResult<LibraryPath> result = await service.CreateFileAsync(fixture.Resolve(vault, string.Empty), "plan", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(result.Value.FullPath, Assert.Single(created).FullPath);
    }

    /// <summary>Renaming a document raises Moved once from the old path to the new one.</summary>
    [Fact]
    public async Task RenameAsync_RaisesMovedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "x");
        LibraryEvents events = new();
        List<LibraryItemMoved> moved = [];
        events.Moved += moved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryPath path = fixture.Resolve(vault, "findings.md");

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(path, "final-findings.md", ct);

        Assert.NotNull(result.Value);
        LibraryItemMoved seen = Assert.Single(moved);
        Assert.Equal((path.FullPath, result.Value.NewPath.FullPath), (seen.From.FullPath, seen.To.FullPath));
    }

    /// <summary>Moving a folder raises Moved once from the folder to its new place.</summary>
    [Fact]
    public async Task MoveAsync_Folder_RaisesMovedOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        Directory.CreateDirectory(Path.Combine(vault, "Research"));
        Directory.CreateDirectory(Path.Combine(vault, "Archive"));
        File.WriteAllText(Path.Combine(vault, "Research", "findings.md"), "x");
        LibraryEvents events = new();
        List<LibraryItemMoved> moved = [];
        events.Moved += moved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);
        LibraryPath folder = fixture.Resolve(vault, "Research");

        LibraryResult<LibraryMoveResult> result = await service.MoveAsync(folder, fixture.Resolve(vault, "Archive"), ct);

        Assert.NotNull(result.Value);
        LibraryItemMoved seen = Assert.Single(moved);
        Assert.Equal((folder.FullPath, result.Value.NewPath.FullPath), (seen.From.FullPath, seen.To.FullPath));
    }

    /// <summary>A refused rename raises nothing.</summary>
    [Fact]
    public async Task RenameAsync_Refused_RaisesNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "x");
        File.WriteAllText(Path.Combine(vault, "taken.md"), "y");
        LibraryEvents events = new();
        List<LibraryItemMoved> moved = [];
        events.Moved += moved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(fixture.Resolve(vault, "findings.md"), "taken.md", ct);

        Assert.Null(result.Value);
        Assert.Empty(moved);
    }

    /// <summary>The link rewrite after a rename is not a Human save: it raises Moved once and HumanSaved never.</summary>
    [Fact]
    public async Task RenameAsync_RewritesLinks_RaisesNoHumanSavedForTheLinkingNote()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "plan.md"), "Plan.");
        File.WriteAllText(Path.Combine(vault, "a.md"), "See [[plan]].");
        fixture.Reload();
        LibraryEvents events = new();
        List<LibraryPath> saved = [];
        List<LibraryItemMoved> moved = [];
        events.HumanSaved += saved.Add;
        events.Moved += moved.Add;
        LibraryFileService service = fixture.CreateServiceWithEvents(events);

        LibraryResult<LibraryMoveResult> result = await service.RenameAsync(fixture.Resolve(vault, "plan.md"), "roadmap.md", ct);

        Assert.NotNull(result.Value);
        Assert.Equal(["a.md"], result.Value.RewrittenNotes);
        Assert.Empty(saved);
        Assert.Single(moved);
    }
}
