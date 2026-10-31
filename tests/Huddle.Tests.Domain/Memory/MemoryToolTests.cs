namespace Agency.Huddle.Tests.Memory;

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Memory;
using Agency.Huddle.Tests.Elicitation;

/// <summary>
/// Pins the <c>memory</c> App Tool over the caller's own folder (Instructions spec §5.1 to §5.4, §5.7): each command, each
/// refusal, the version token and the folder gate.
/// </summary>
public sealed class MemoryToolTests
{
    private const string GoodDescription = "Teachers, the front office, and who to email about what. Read for school mail.";

    private const string DescriptionAdvice = "Give one line of 20 to 200 characters (about 100 to 150 is best) that names the specific people, projects, places or events inside and says when to read the file, such as \"Read for...\" or \"Check before...\". Put no facts and nothing sensitive in it.";

    /// <summary>An empty folder lists as nothing yet, with the folder path and the folders this caller may use.</summary>
    [Fact]
    public async Task List_EmptyFolder_SaysNothingYetAndNamesTheFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(MemoryToolHarness.Args("list"), ct);

        Assert.Equal($"Nothing yet.\nfolder: {harness.MemoryDir()}\nfolders you may use: me", result);
    }

    /// <summary>The list is one <c>file - description</c> line per file, ordered by name, and is not capped at the prompt's 100.</summary>
    [Fact]
    public async Task List_ShowsEveryFileWithItsDescription_Uncapped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string folder = harness.MemoryDir();
        MemoryToolHarness.Put(folder, "dentist.md", "---\ndescription: Dentist visits and insurance. Read before booking.\n---\n\nBody.");
        MemoryToolHarness.Put(folder, "old-fact.md", "The Human prefers C#.");
        for (int i = 0; i < 120; i++)
        {
            MemoryToolHarness.Put(folder, $"topic-{i:D3}.md", $"Fact number {i}.");
        }

        string result = await harness.RunAsync(MemoryToolHarness.Args("list"), ct);

        List<string> lines = ["dentist.md - Dentist visits and insurance. Read before booking.", "old-fact.md - The Human prefers C#."];
        lines.AddRange(Enumerable.Range(0, 120).Select(static i => $"topic-{i:D3}.md - Fact number {i}."));
        Assert.Equal($"{string.Join('\n', lines)}\nfolder: {folder}\nfolders you may use: me", result);
    }

    /// <summary>A new file is written with the description as frontmatter, a blank line and the text; the result carries the version of what is on disk (U-3).</summary>
    [Fact]
    public async Task Write_NewFile_ComposesFrontmatterAndReturnsTheVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "school-contacts.md"), ("description", GoodDescription), ("text", "# School contacts\n\n- Ms Patel teaches Year 4.")),
            ct);

        string path = Path.Combine(harness.MemoryDir(), "school-contacts.md");
        byte[] bytes = await File.ReadAllBytesAsync(path, ct);
        Assert.Equal($"---\ndescription: {GoodDescription}\n---\n\n# School contacts\n\n- Ms Patel teaches Year 4.\n", Encoding.UTF8.GetString(bytes));
        Assert.Equal($"written school-contacts.md (version {MemoryVersion.Of(bytes)})", result);
    }

    /// <summary>A description with a colon is quoted so the Library reads it back whole, and the next prompt's index line shows it.</summary>
    [Fact]
    public async Task Write_DescriptionWithAColon_IsQuotedAndReadsBack()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        const string Description = "Mum's phone: 555 0100. Read before ringing the family.";

        _ = await harness.RunAsync(MemoryToolHarness.Args("write", ("file", "family.md"), ("description", Description), ("text", "x")), ct);

        string listed = await harness.RunAsync(MemoryToolHarness.Args("list"), ct);
        Assert.Equal($"family.md - {Description}\nfolder: {harness.MemoryDir()}\nfolders you may use: me", listed);
    }

    /// <summary>A write without an acceptable description is refused with the rule (U-8).</summary>
    /// <param name="description">The description as passed; <c>null</c> passes none.</param>
    /// <param name="problem">What the refusal must say is wrong.</param>
    [Theory]
    [InlineData(null, "it is missing.")]
    [InlineData("", "it is missing.")]
    [InlineData("   ", "it is missing.")]
    [InlineData("Too short.", "it is shorter than 20 characters.")]
    [InlineData("First line is long enough.\nSecond line.", "it spans several lines.")]
    [InlineData("School contacts", "it only repeats the file name.")]
    [InlineData("school-contacts.md", "it only repeats the file name.")]
    public async Task Write_BadDescription_IsRefusedWithTheRule(string? description, string problem)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        JsonObject arguments = MemoryToolHarness.Args("write", ("file", "school-contacts.md"), ("text", "x"));
        if (description is not null)
        {
            arguments["description"] = description;
        }

        string result = await harness.RunAsync(arguments, ct);

        Assert.Equal($"The description is not acceptable: {problem} {DescriptionAdvice}", result);
        Assert.False(File.Exists(Path.Combine(harness.MemoryDir(), "school-contacts.md")));
    }

    /// <summary>A description over 200 characters is refused.</summary>
    [Fact]
    public async Task Write_DescriptionOver200_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "long.md"), ("description", new string('d', 201)), ("text", "x")),
            ct);

        Assert.Equal($"The description is not acceptable: it is longer than 200 characters. {DescriptionAdvice}", result);
    }

    /// <summary>Anything but one plain <c>.md</c> name is refused, whatever the command (U-7).</summary>
    /// <param name="file">The file name as passed.</param>
    [Theory]
    [InlineData("../../Teams/Dev/memory/x.md")]
    [InlineData("..\\x.md")]
    [InlineData("C:\\x.md")]
    [InlineData("/etc/x.md")]
    [InlineData("sub/x.md")]
    [InlineData("CON.md")]
    [InlineData("con.md")]
    [InlineData("NUL.md")]
    [InlineData("COM1.md")]
    [InlineData("lpt9.md")]
    [InlineData("x.txt")]
    [InlineData("x")]
    [InlineData(".md")]
    [InlineData("x .md")]
    [InlineData("x..md")]
    [InlineData("Instructions.md")]
    [InlineData("instructions.MD")]
    [InlineData("_hidden.md")]
    [InlineData("memory.md")]
    [InlineData("")]
    public async Task AnyCommand_BadFileName_IsRefused(string file)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        foreach (string command in new[] { "read", "write", "append", "replace", "delete" })
        {
            string result = await harness.RunAsync(
                MemoryToolHarness.Args(command, ("file", file), ("description", GoodDescription), ("text", "x"), ("old", "a"), ("new", "b"), ("version", "abc")),
                ct);

            Assert.Equal("A memory file name is one name ending in .md, with no folders.", result);
        }

        Assert.False(Directory.Exists(harness.MemoryDir()) && Directory.EnumerateFiles(harness.MemoryDir()).Any());
    }

    /// <summary>A read returns the whole file, frontmatter included, then its version.</summary>
    [Fact]
    public async Task Read_ReturnsTheTextAndItsVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "---\ndescription: A topic file. Read for anything.\n---\n\nBody.\n");

        string result = await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "topic.md")), ct);

        string version = MemoryVersion.Of(await File.ReadAllBytesAsync(path, ct));
        Assert.Equal($"---\ndescription: A topic file. Read for anything.\n---\n\nBody.\n\nversion: {version}", result);
    }

    /// <summary>A read, append, replace or delete of a missing file says it is not there.</summary>
    /// <param name="command">The command.</param>
    [Theory]
    [InlineData("read")]
    [InlineData("append")]
    [InlineData("replace")]
    [InlineData("delete")]
    public async Task MissingFile_IsReportedNotFound(string command)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(
            MemoryToolHarness.Args(command, ("file", "nothing.md"), ("text", "x"), ("old", "a"), ("new", "b"), ("version", "abc")),
            ct);

        Assert.Equal("nothing.md does not exist in that folder. Use the list command to see what is there.", result);
    }

    /// <summary>A write over an existing file without its version is told to read it first.</summary>
    [Fact]
    public async Task Write_ExistingFileWithoutVersion_IsToldToReadFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Original.");

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "New.")),
            ct);

        Assert.Equal("topic.md already exists. Read it first with the read command, then write it with the version it gives.", result);
        Assert.Equal("Original.", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>A write with the version a read returned replaces the file and returns a new version.</summary>
    [Fact]
    public async Task Write_ExistingFileWithTheReadVersion_Replaces()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Original.");
        string version = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "topic.md")), ct));

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "New."), ("version", version)),
            ct);

        string expected = $"---\ndescription: {GoodDescription}\n---\n\nNew.\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(path, ct));
        Assert.Equal($"written topic.md (version {MemoryVersion.Of(Encoding.UTF8.GetBytes(expected))})", result);
    }

    /// <summary>A write with a stale version is refused with the current text and token so one more call can merge (U-4, U-9).</summary>
    [Fact]
    public async Task Write_StaleVersion_ReturnsTheCurrentTextAndToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "First.");
        string stale = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "topic.md")), ct));
        await File.WriteAllTextAsync(path, "Edited by the Human.", ct);

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "Mine."), ("version", stale)),
            ct);

        string current = MemoryVersion.Of(await File.ReadAllBytesAsync(path, ct));
        Assert.Equal(
            "topic.md changed since you read it. Its current text and version follow: merge your change into it and write again."
            + $"\n\nEdited by the Human.\n\nversion: {current}",
            result);
        Assert.Equal("Edited by the Human.", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>A write that names a version for a file that is gone is told it was deleted.</summary>
    [Fact]
    public async Task Write_VersionForAMissingFile_IsToldItWasDeleted()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "gone.md"), ("description", GoodDescription), ("text", "x"), ("version", "abc123abc123")),
            ct);

        Assert.Equal("gone.md was deleted since you read it. To create it again, write it without a version.", result);
        Assert.False(File.Exists(Path.Combine(harness.MemoryDir(), "gone.md")));
    }

    /// <summary>Two writes at the same version race through the gate: one is written and the other is refused with a conflict (U-4).</summary>
    [Fact]
    public async Task Write_TwoWritesAtTheSameVersion_OneWinsAndOneConflicts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Original.");
        string version = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "topic.md")), ct));

        Task<string> first = harness.RunAsync(MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "From Room A."), ("version", version)), ct);
        Task<string> second = harness.RunAsync(MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "From Room B."), ("version", version)), ct);
        string[] results = await Task.WhenAll(first, second);

        Assert.Single(results, static result => result.StartsWith("written topic.md", StringComparison.Ordinal));
        Assert.Single(results, static result => result.Contains("changed since you read it", StringComparison.Ordinal));
    }

    /// <summary>Appends need no version, so two appended at once both land.</summary>
    [Fact]
    public async Task Append_TwoAtOnce_BothLand()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "---\ndescription: A running log of small things. Read for recent notes.\n---\n\nStart.\n");

        _ = await Task.WhenAll(
            harness.RunAsync(MemoryToolHarness.Args("append", ("file", "topic.md"), ("text", "- one")), ct),
            harness.RunAsync(MemoryToolHarness.Args("append", ("file", "topic.md"), ("text", "- two")), ct));

        string text = await File.ReadAllTextAsync(path, ct);
        Assert.Contains("- one\n", text, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
        Assert.Contains("- two\n", text, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>An append adds a new line at the end, supplies a missing final line break, and returns the version and the current description.</summary>
    [Fact]
    public async Task Append_AddsALineAndReturnsTheDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "---\ndescription: A running log of small things. Read for recent notes.\n---\n\nStart");

        string result = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "topic.md"), ("text", "- next")), ct);

        string expected = "---\ndescription: A running log of small things. Read for recent notes.\n---\n\nStart\n- next\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(path, ct));
        Assert.Equal(
            $"appended to topic.md (version {MemoryVersion.Of(Encoding.UTF8.GetBytes(expected))}); description: A running log of small things. Read for recent notes.",
            result);
    }

    /// <summary>A replace swaps text that occurs once, leaves the description alone, and returns the description (U-2).</summary>
    [Fact]
    public async Task Replace_TextOccurringOnce_IsSwapped()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(
            harness.MemoryDir(),
            "school-contacts.md",
            $"---\ndescription: {GoodDescription}\n---\n\n- Lily is in Year 3.\n- Ms Patel teaches Year 4.\n");

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("replace", ("file", "school-contacts.md"), ("old", "Lily is in Year 3"), ("new", "Lily is in Year 4")),
            ct);

        string expected = $"---\ndescription: {GoodDescription}\n---\n\n- Lily is in Year 4.\n- Ms Patel teaches Year 4.\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(path, ct));
        Assert.Equal($"replaced in school-contacts.md (version {MemoryVersion.Of(Encoding.UTF8.GetBytes(expected))}); description: {GoodDescription}", result);
    }

    /// <summary>A replace of text that occurs twice, or not at all, is refused with the count and changes nothing.</summary>
    /// <param name="old">The text to replace.</param>
    /// <param name="count">How many times it occurs.</param>
    [Theory]
    [InlineData("Year", 2)]
    [InlineData("Year 9", 0)]
    public async Task Replace_TextNotOccurringExactlyOnce_IsRefused(string old, int count)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Year 3.\nYear 4.\n");

        string result = await harness.RunAsync(MemoryToolHarness.Args("replace", ("file", "topic.md"), ("old", old), ("new", "x")), ct);

        Assert.Equal($"The text to replace must occur exactly once in topic.md, and it occurs {count} times. Give a longer piece of text that occurs once.", result);
        Assert.Equal("Year 3.\nYear 4.\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>An older file with no frontmatter reads and replaces fine, and keeps having none (U-11).</summary>
    [Fact]
    public async Task Replace_OlderFileWithoutFrontmatter_StaysWithout()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "code-language.md", "The Human prefers C# for all code.\n");

        string result = await harness.RunAsync(
            MemoryToolHarness.Args("replace", ("file", "code-language.md"), ("old", "C#"), ("new", "F#")),
            ct);

        Assert.Equal("The Human prefers F# for all code.\n", await File.ReadAllTextAsync(path, ct));
        Assert.EndsWith("description: The Human prefers F# for all code.", result, StringComparison.Ordinal); // contains-ok: prompt or tool-result text, not markup
    }

    /// <summary>A file the Human saved with Windows line breaks is still matched by an <c>old</c> written with plain ones.</summary>
    [Fact]
    public async Task Replace_FileWithWindowsLineBreaks_StillMatches()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Line one.\r\nLine two.\r\n");

        _ = await harness.RunAsync(
            MemoryToolHarness.Args("replace", ("file", "topic.md"), ("old", "Line one.\nLine two."), ("new", "Line one.\nLine 2.")),
            ct);

        Assert.Equal("Line one.\r\nLine 2.\r\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>A delete needs the version, and goes to the Recycle Bin, not the void (U-13).</summary>
    [Fact]
    public async Task Delete_WithTheVersion_SendsTheFileToTheRecycleBin()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "old-school.md", "Obsolete.");
        string version = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "old-school.md")), ct));

        string withoutVersion = await harness.RunAsync(MemoryToolHarness.Args("delete", ("file", "old-school.md")), ct);
        string stale = await harness.RunAsync(MemoryToolHarness.Args("delete", ("file", "old-school.md"), ("version", "000000000000")), ct);
        string result = await harness.RunAsync(MemoryToolHarness.Args("delete", ("file", "old-school.md"), ("version", version)), ct);

        Assert.Equal("Read old-school.md first, then pass its version to delete it.", withoutVersion);
        Assert.Equal(
            $"old-school.md changed since you read it. Its current text and version follow: merge your change into it and write again.\n\nObsolete.\n\nversion: {version}",
            stale);
        Assert.Equal("deleted old-school.md", result);
        Assert.Equal([path], harness.Bin.Sent);
    }

    /// <summary>A Recycle Bin that refuses leaves the file and says it could not be changed.</summary>
    [Fact]
    public async Task Delete_RecycleBinRefuses_ReportsItAndLogsWhy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        MemoryToolHarness.Put(harness.MemoryDir(), "old-school.md", "Obsolete.");
        harness.Bin.FailureError = "The Recycle Bin is full.";
        string version = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "old-school.md")), ct));

        string result = await harness.RunAsync(MemoryToolHarness.Args("delete", ("file", "old-school.md"), ("version", version)), ct);

        Assert.Equal("old-school.md could not be read or changed because something else is using it. Nothing was lost; try again.", result);
        LogEntry warning = Assert.Single(harness.Logger.AtExactly(LogLevel.Warning));
        Assert.Equal($"Could not send the memory file '{Path.Combine(harness.MemoryDir(), "old-school.md")}' to the Recycle Bin: The Recycle Bin is full.", warning.Message);
    }

    /// <summary>A result over the size cap is refused, whichever command makes it, and says to split the topic.</summary>
    [Fact]
    public async Task Write_OverTheSizeCap_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new(options => options.Memory.MaxFileBytes = 200);
        MemoryToolHarness.Put(harness.MemoryDir(), "small.md", new string('a', 150));

        string write = await harness.RunAsync(MemoryToolHarness.Args("write", ("file", "big.md"), ("description", GoodDescription), ("text", new string('x', 300))), ct);
        string append = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "small.md"), ("text", new string('y', 100))), ct);
        string replace = await harness.RunAsync(MemoryToolHarness.Args("replace", ("file", "small.md"), ("old", new string('a', 150)), ("new", new string('z', 400))), ct);

        int writeSize = Encoding.UTF8.GetByteCount($"---\ndescription: {GoodDescription}\n---\n\n") + 301;
        Assert.Equal($"big.md would be {writeSize} bytes, over the limit of 200. Split it into topics, each in its own file.", write);
        Assert.Equal("small.md would be 252 bytes, over the limit of 200. Split it into topics, each in its own file.", append);
        Assert.Equal("small.md would be 400 bytes, over the limit of 200. Split it into topics, each in its own file.", replace);
        Assert.False(File.Exists(Path.Combine(harness.MemoryDir(), "big.md")));
        Assert.Equal(new string('a', 150), await File.ReadAllTextAsync(Path.Combine(harness.MemoryDir(), "small.md"), ct));
    }

    /// <summary>
    /// Windows only: when the atomic write reports it could not replace the file, the call is refused with the whole refusal text and
    /// nothing is lost. A handle that allows reading but not deleting blocks the rename only on Windows.
    /// </summary>
    [Fact]
    public async Task Write_WhenTheAtomicWriteFails_IsRefused()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An exclusive lock is only enforced across handles on Windows.");
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Original.");
        string version = MemoryToolHarness.VersionOf(await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "topic.md")), ct));

        using FileStream held = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        string result = await harness.RunAsync(
            MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "New."), ("version", version)),
            ct);

        Assert.Equal("topic.md could not be read or changed because something else is using it. Nothing was lost; try again.", result);
        Assert.Equal("Original.", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>Windows only: a file another process holds exclusively cannot be read, and the read says so rather than throwing.</summary>
    [Fact]
    public async Task Read_WhenTheFileCannotBeOpened_SaysItCouldNotBeRead()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "An exclusive lock is only enforced across handles on Windows.");
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "locked.md", "Secret.");

        using FileStream held = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        string result = await harness.RunAsync(MemoryToolHarness.Args("read", ("file", "locked.md")), ct);

        Assert.Equal("locked.md could not be read or changed because something else is using it. Nothing was lost; try again.", result);
        LogEntry warning = Assert.Single(harness.Logger.AtExactly(LogLevel.Warning));
        Assert.Equal($"Could not read the memory file '{path}'.", warning.Message);
    }
    /// <summary>A missing or unknown command, or one missing its arguments, is refused with the list of commands.</summary>
    /// <param name="command">The command, or null for none.</param>
    /// <param name="problem">What the refusal says is wrong.</param>
    [Theory]
    [InlineData(null, "A memory call needs a 'command'.")]
    [InlineData("frobnicate", "Unknown command 'frobnicate'.")]
    [InlineData("read", "The read command needs 'file'.")]
    [InlineData("append", "The append command needs 'text'.")]
    [InlineData("replace", "The replace command needs 'old'.")]
    public async Task BadCommand_IsRefusedWithTheCommands(string? command, string problem)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        JsonObject arguments = [];
        if (command is not null)
        {
            arguments["command"] = command;
        }

        if (command is "append" or "replace")
        {
            arguments["file"] = "topic.md";
        }

        string result = await harness.RunAsync(arguments, ct);

        Assert.Equal($"{problem} The commands are list, read, write, append, replace and delete.", result);
    }

    /// <summary>A replace with no <c>new</c> is refused naming it, while an empty <c>new</c> is allowed because it deletes the matched text.</summary>
    [Fact]
    public async Task Replace_NewMissingIsRefused_ButEmptyNewIsAllowed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "topic.md", "Keep this. Drop this.\n");

        string missing = await harness.RunAsync(MemoryToolHarness.Args("replace", ("file", "topic.md"), ("old", " Drop this.")), ct);
        string empty = await harness.RunAsync(MemoryToolHarness.Args("replace", ("file", "topic.md"), ("old", " Drop this."), ("new", string.Empty)), ct);

        Assert.Equal("The replace command needs 'new'. The commands are list, read, write, append, replace and delete.", missing);
        Assert.Equal($"replaced in topic.md (version {MemoryVersion.Of("Keep this.\n"u8.ToArray())}); description: Keep this.", empty);
        Assert.Equal("Keep this.\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>A mutating command logs Information with the caller, folder, file, command and the new token.</summary>
    [Fact]
    public async Task Write_LogsInformationWithTheNewToken()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await harness.RunAsync(MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "x")), ct);

        LogEntry entry = Assert.Single(harness.Logger.AtExactly(LogLevel.Information));
        string token = MemoryToolHarness.VersionOf(result.Replace("(version ", "version: ", StringComparison.Ordinal).TrimEnd(')'));
        Assert.Equal($"Memory write by 'Nova' in 'me' on 'topic.md', now version {token}.", entry.Message);
    }

    /// <summary>The folder argument <c>me</c> and its absence are the same folder, and its spelling is not case-sensitive.</summary>
    /// <param name="folder">The folder argument, or null for none.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("me")]
    [InlineData("ME")]
    public async Task Folder_MeIsTheDefault(string? folder)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        JsonObject arguments = MemoryToolHarness.Args("write", ("file", "topic.md"), ("description", GoodDescription), ("text", "x"));
        if (folder is not null)
        {
            arguments["folder"] = folder;
        }

        _ = await harness.RunAsync(arguments, ct);

        Assert.True(File.Exists(Path.Combine(harness.MemoryDir(), "topic.md")));
    }
}
