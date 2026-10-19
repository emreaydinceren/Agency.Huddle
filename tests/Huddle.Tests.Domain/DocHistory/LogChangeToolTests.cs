using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Profile;
using Agency.Huddle.Tests.Acp.Fakes;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.DocHistory;

/// <summary>Pins the <c>log_change</c> tool (DocHistory spec §6.3): the entry it writes, who it writes it as, and each refusal's text.</summary>
public sealed class LogChangeToolTests
{
    /// <summary>The tool's model-facing name is <c>log_change</c>, and its schema names the three arguments and no actor.</summary>
    [Fact]
    public void Schema_NamesPathKindAndSummary_AndNoActor()
    {
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LogChangeTool tool = Build(fixture, "Nova", out _);

        JsonObject properties = Assert.IsType<JsonObject>(tool.InputSchema["properties"]);

        Assert.Equal("log_change", tool.Name);
        Assert.Equal(["path", "kind", "summary"], properties.Select(property => property.Key));
    }

    /// <summary>A valid call writes one Agent entry under the Name the tool was built for and answers <c>Logged.</c>.</summary>
    [Fact]
    public async Task Invoke_Valid_WritesAnAgentEntry()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "findings.md"), "# Findings");
        LogChangeTool tool = Build(fixture, "Ruth", out DocHistoryStore store);

        string result = await tool.InvokeAsync(Args(Path.Combine(vault, "findings.md"), "Reviewed", "Checked every citation\nagainst the source"), ct);

        Assert.Equal("Logged.", result);
        DocHistoryEntry entry = Assert.Single(store.Read(fixture.Resolve(vault, "findings.md")));
        Assert.Equal(("Ruth", DocActorKind.Agent, DocChangeKind.Reviewed, "Checked every citation against the source"), (entry.Actor, entry.ActorKind, entry.Kind, entry.Summary));
    }

    /// <summary>A path outside every Library Root is refused with the spec's text and writes nothing.</summary>
    [Fact]
    public async Task Invoke_OutsideTheLibrary_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        _ = fixture.CreatePinnedRoot("Vault");
        string outside = Path.Combine(Path.GetTempPath(), "elsewhere", "x.md");
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync(Args(outside, "edited", "x"), ct);

        Assert.Equal($"'{outside}' is not in the Library. Only Library documents have a history.", result);
    }

    /// <summary>A file that is not Markdown is refused.</summary>
    [Fact]
    public async Task Invoke_NotMarkdown_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "notes.txt"), "x");
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync(Args(Path.Combine(vault, "notes.txt"), "edited", "x"), ct);

        Assert.Equal("Only Markdown documents have a history.", result);
    }

    /// <summary>A Markdown path that is not on disk is refused, so an Agent logs after the file is written.</summary>
    [Fact]
    public async Task Invoke_MissingFile_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        string missing = Path.Combine(vault, "later.md");
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync(Args(missing, "created", "x"), ct);

        Assert.Equal($"'{missing}' does not exist. Log a change after the file is written.", result);
    }

    /// <summary>An unknown kind is refused with the four allowed values.</summary>
    [Fact]
    public async Task Invoke_UnknownKind_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "a.md"), "x");
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync(Args(Path.Combine(vault, "a.md"), "deleted", "x"), ct);

        Assert.Equal("'kind' must be created, edited, reviewed or objected.", result);
    }

    /// <summary>A blank summary and a summary over the limit are both refused, and neither writes an entry.</summary>
    [Fact]
    public async Task Invoke_BlankOrLongSummary_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "a.md"), "x");
        LogChangeTool tool = Build(fixture, "Ruth", out DocHistoryStore store);
        string expected = "'summary' is required, one line of at most 200 characters.";

        string blank = await tool.InvokeAsync(Args(Path.Combine(vault, "a.md"), "edited", "  \n "), ct);
        string tooLong = await tool.InvokeAsync(Args(Path.Combine(vault, "a.md"), "edited", new string('x', 201)), ct);

        Assert.Equal([expected, expected], [blank, tooLong]);
        Assert.Empty(store.Read(fixture.Resolve(vault, "a.md")));
    }

    /// <summary>A missing <c>path</c> is reported as text and never thrown.</summary>
    [Fact]
    public async Task Invoke_MissingPath_ReturnsRequiredArgumentText()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync([], ct);

        Assert.Equal("'path' is a required argument.", result);
    }

    /// <summary>A number is not a kind: <c>1</c> is refused although it would parse as an enum value.</summary>
    [Fact]
    public async Task Invoke_NumericKind_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "a.md"), "x");
        LogChangeTool tool = Build(fixture, "Ruth", out DocHistoryStore store);

        string result = await tool.InvokeAsync(Args(Path.Combine(vault, "a.md"), "1", "x"), ct);

        Assert.Equal("'kind' must be created, edited, reviewed or objected.", result);
        Assert.Empty(store.Read(fixture.Resolve(vault, "a.md")));
    }

    /// <summary>An <c>actor</c> argument the schema does not have is ignored: the entry is under the bound Name.</summary>
    [Fact]
    public async Task Invoke_ExtraActorArgument_IsIgnored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string vault = fixture.CreatePinnedRoot("Vault");
        File.WriteAllText(Path.Combine(vault, "a.md"), "x");
        LogChangeTool tool = Build(fixture, "Nova", out DocHistoryStore store);
        JsonObject arguments = Args(Path.Combine(vault, "a.md"), "edited", "Tidied");
        arguments["actor"] = "Ruth";

        string result = await tool.InvokeAsync(arguments, ct);

        Assert.Equal("Logged.", result);
        Assert.Equal("Nova", Assert.Single(store.Read(fixture.Resolve(vault, "a.md"))).Actor);
    }

    /// <summary>A document in a read-only root is refused with the Library's own reason and nothing is written.</summary>
    [Fact]
    public async Task Invoke_ReadOnlyRoot_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using LibraryFileServiceFixture fixture = LibraryFileServiceFixture.Build();
        string folder = Path.Combine(fixture.DataDir, "SkillSources");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILLS.md"), "# Skills");
        LogChangeTool tool = Build(fixture, "Ruth", out _);

        string result = await tool.InvokeAsync(Args(Path.Combine(folder, "SKILLS.md"), "edited", "x"), ct);

        Assert.Equal(Agency.Huddle.App.Library.LibraryPath.ReadOnlyReason, result);
        Assert.False(Directory.Exists(Path.Combine(fixture.DataDir, "doc-history")));
    }

    /// <summary>Builds the tool over the fixture's Library with a recorder and store of its own.</summary>
    /// <param name="fixture">The Library fixture.</param>
    /// <param name="agentName">The Name the tool is bound to.</param>
    /// <param name="store">The store the tool writes to.</param>
    /// <returns>The tool.</returns>
    private static LogChangeTool Build(LibraryFileServiceFixture fixture, string agentName, out DocHistoryStore store)
    {
        store = new DocHistoryStore(fixture.BoundOptions, NullLogger<DocHistoryStore>.Instance);
        DocHistoryRecorder recorder = new(store, fixture.BoundOptions, NullLogger<DocHistoryRecorder>.Instance, new ConfiguredHumanIdentity("Emre"));
        return new LogChangeTool(recorder, fixture.Resolver, fixture.BoundOptions, agentName, new FakePromptSource());
    }

    /// <summary>The arguments of one call.</summary>
    /// <param name="path">The document's full path.</param>
    /// <param name="kind">The kind text.</param>
    /// <param name="summary">The summary text.</param>
    /// <returns>The arguments.</returns>
    private static JsonObject Args(string path, string kind, string summary) =>
        new() { ["path"] = path, ["kind"] = kind, ["summary"] = summary };
}
