namespace Agency.Huddle.Tests.Memory;

using System.Text;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Acp.Tools;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;

/// <summary>
/// Pins the <c>memory</c> App Tool's handling of <c>reported.md</c> in the caller's own folder (Memory Tidy spec §6.3): the file is
/// created on the first append, the tool sets the date, one line per call, old lines are dropped and the oldest go at the cap.
/// Nothing of this applies to <c>reported.md</c> in a Team folder or to another file.
/// </summary>
public sealed class MemoryToolReportedTests
{
    private const string DefaultDescription = "What I have already told the Human, by date and Room. Read before reporting so nothing is said twice.";

    private const string Head = "---\ndescription: " + DefaultDescription + "\n---\n\n";

    private const string Line = "- 2026-11-16 · Nova and Sam · launch budget is 12% over plan";

    /// <summary>An append to a missing reported.md creates it with the fixed description and the one line, dated today.</summary>
    [Fact]
    public async Task Append_MissingFile_CreatesItWithTheFixedDescription()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        string result = await Run(harness, "Nova and Sam · launch budget is 12% over plan", ct);

        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(harness.MemoryDir(), "reported.md"), ct);
        Assert.Equal(Appended(bytes), result);
        Assert.Equal(Head + Line + "\n", Encoding.UTF8.GetString(bytes));
        Assert.Equal(DefaultDescription, MemoryFileRules.DescriptionOf(Encoding.UTF8.GetString(bytes)));
    }

    /// <summary>A Prompt override that fails the description rule falls back to the default text and logs one Warning per tool instance.</summary>
    [Fact]
    public async Task Append_OverriddenBadDescription_FallsBackAndLogsOnce()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.Prompts.SetOverride("systemPrompt.memoryReportedDescription", "too short");
        MemoryTool tool = harness.ToolFor();
        string path = Path.Combine(harness.MemoryDir(), "reported.md");

        _ = await tool.InvokeAsync(MemoryToolHarness.Args("append", ("file", "reported.md"), ("text", "Nova · one")), ct);
        string first = await File.ReadAllTextAsync(path, ct);
        File.Delete(path);
        _ = await tool.InvokeAsync(MemoryToolHarness.Args("append", ("file", "reported.md"), ("text", "Nova · two")), ct);

        Assert.Equal(DefaultDescription, MemoryFileRules.DescriptionOf(first));
        Assert.Equal(DefaultDescription, MemoryFileRules.DescriptionOf(await File.ReadAllTextAsync(path, ct)));
        Assert.Single(harness.Logger.AtExactly(LogLevel.Warning), static entry => entry.Message.Contains("systemPrompt.memoryReportedDescription", StringComparison.Ordinal));
    }

    /// <summary>A good override of the description is used as given.</summary>
    [Fact]
    public async Task Append_OverriddenGoodDescription_IsUsed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.Prompts.SetOverride("systemPrompt.memoryReportedDescription", "What I told the Human, by day. Read before you report.");

        _ = await Run(harness, "Nova · one", ct);

        string text = await File.ReadAllTextAsync(Path.Combine(harness.MemoryDir(), "reported.md"), ct);
        Assert.Equal("What I told the Human, by day. Read before you report.", MemoryFileRules.DescriptionOf(text));
        Assert.Empty(harness.Logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>The date is the clock's local date whatever the text starts with: nothing, a marker, a date, both, or a date in the future.</summary>
    [Theory]
    [InlineData("Nova and Sam · launch budget is 12% over plan")]
    [InlineData("- Nova and Sam · launch budget is 12% over plan")]
    [InlineData("2026-01-02 · Nova and Sam · launch budget is 12% over plan")]
    [InlineData("- 2026-01-02 · Nova and Sam · launch budget is 12% over plan")]
    [InlineData("- 2099-12-31 · Nova and Sam · launch budget is 12% over plan")]
    [InlineData("- 2026-11-16 · Nova and Sam · launch budget is 12% over plan\n")]
    public async Task Append_AnyLeadingDate_IsReplacedByTheClocksLocalDate(string agentText)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", Head + "- 2026-11-15 · Nova · earlier\n");

        _ = await Run(harness, agentText, ct);

        Assert.Equal(Head + "- 2026-11-15 · Nova · earlier\n" + Line + "\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>The date follows the tool's clock: another day gives another date.</summary>
    [Fact]
    public async Task Append_ClockMoves_TheNewLineCarriesTheNewDate()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.Clock.UtcNow = new DateTimeOffset(2026, 12, 3, 8, 0, 0, TimeSpan.Zero);

        _ = await Run(harness, "Nova · one", ct);

        string text = await File.ReadAllTextAsync(Path.Combine(harness.MemoryDir(), "reported.md"), ct);
        Assert.Equal(Head + "- 2026-12-03 · Nova · one\n", text);
    }

    /// <summary>Several lines are refused with the one-line sentence and nothing is written, whether the file exists or not.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Append_SeveralLines_IsRefusedAndWritesNothing(bool fileExists)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string path = Path.Combine(harness.MemoryDir(), "reported.md");
        if (fileExists)
        {
            _ = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", Head + "- 2026-11-15 · Nova · earlier\n");
        }

        string result = await Run(harness, "Nova · one\nNova · two", ct);

        Assert.Equal("One line for each thing you told the Human.", result);
        Assert.Equal(fileExists, File.Exists(path));
        Assert.Equal(fileExists ? Head + "- 2026-11-15 · Nova · earlier\n" : null, fileExists ? await File.ReadAllTextAsync(path, ct) : null);
    }

    /// <summary>Lines older than KeepDays are dropped in the same write and the reply counts them; one is said in the singular.</summary>
    [Theory]
    [InlineData(3, "; dropped 3 lines older than 14 days")]
    [InlineData(1, "; dropped 1 line older than 14 days")]
    public async Task Append_OldLines_AreDroppedAndCounted(int old, string suffix)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        StringBuilder existing = new(Head);
        for (int i = 0; i < old; i++)
        {
            _ = existing.Append("- 2026-10-0").Append(i + 1).Append(" · Nova · old\n");
        }

        _ = existing.Append("- 2026-11-02 · Nova · boundary day\n");
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", existing.ToString());

        string result = await Run(harness, "Nova and Sam · launch budget is 12% over plan", ct);

        Assert.Equal(Head + "- 2026-11-02 · Nova · boundary day\n" + Line + "\n", await File.ReadAllTextAsync(path, ct));
        Assert.Equal(Appended(await File.ReadAllBytesAsync(path, ct), suffix), result);
    }

    /// <summary>KeepDays zero turns the age drop off through the option.</summary>
    [Fact]
    public async Task Append_KeepDaysZero_DropsNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new(static options => options.Memory.Reported.KeepDays = 0);
        string old = Head + "- 2020-01-01 · Nova · ancient\n";
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", old);

        string result = await Run(harness, "Nova and Sam · launch budget is 12% over plan", ct);

        Assert.DoesNotContain("dropped", result, StringComparison.Ordinal);
        Assert.Equal(old + Line + "\n", await File.ReadAllTextAsync(path, ct));
    }

    /// <summary>At the cap the oldest dated lines go until the new one fits, an unreadable line stays, and the reply says how many went.</summary>
    [Fact]
    public async Task Append_AtTheCap_DropsTheOldestLinesUntilTheNewOneFits()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        string[] lines = [.. Enumerable.Range(10, 5).Select(static day => $"- 2026-11-{day} · Nova · item {day}\n")];
        string note = "The Human's own note\n";
        string existing = Head + lines[2] + note + lines[0] + lines[1] + lines[3] + lines[4];
        int cap = Encoding.UTF8.GetByteCount(Head + lines[2] + note + lines[3] + lines[4] + Line + "\n");
        using MemoryToolHarness harness = new(options => options.Memory.MaxFileBytes = cap);
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", existing);

        string result = await Run(harness, "Nova and Sam · launch budget is 12% over plan", ct);

        Assert.Equal(Head + lines[2] + note + lines[3] + lines[4] + Line + "\n", await File.ReadAllTextAsync(path, ct));
        Assert.Equal(Appended(await File.ReadAllBytesAsync(path, ct), "; dropped 2 of the oldest lines to make room"), result);
    }

    /// <summary>One line that alone is over the cap is refused with the too-large sentence and the file is left as it was.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Append_LineAloneOverTheCap_IsRefused(bool fileExists)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        int cap = Encoding.UTF8.GetByteCount(Head) + 10;
        using MemoryToolHarness harness = new(options => options.Memory.MaxFileBytes = cap);
        string path = Path.Combine(harness.MemoryDir(), "reported.md");
        string existing = Head + "- 2026-11-15 · Nova · earlier\n";
        if (fileExists)
        {
            _ = MemoryToolHarness.Put(harness.MemoryDir(), "reported.md", Head);
            existing = Head;
        }

        string result = await Run(harness, "Nova and Sam · launch budget is 12% over plan", ct);

        int size = Encoding.UTF8.GetByteCount(Head + Line + "\n");
        Assert.Equal($"reported.md would be {size} bytes, over the limit of {cap}. Split it into topics, each in its own file.", result);
        Assert.Equal(fileExists, File.Exists(path));
        Assert.Equal(fileExists ? existing : null, fileExists ? await File.ReadAllTextAsync(path, ct) : null);
    }

    /// <summary>A reported.md in a Team's folder is an ordinary topic file: the text goes in as given, nothing is dropped, and a missing file is refused.</summary>
    [Fact]
    public async Task Append_TeamFolder_IsAnOrdinaryFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.Catalog.Teams = [new TeamSummary("Growth", [], ["Leo"], true) { Manager = "Ada" }];
        string teamDir = harness.TeamMemoryDir("Growth");
        string existing = Head + "- 2020-01-01 · Ada · ancient\n";
        string path = MemoryToolHarness.Put(teamDir, "reported.md", existing);

        string missing = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("append", ("folder", "Growth"), ("file", "other.md"), ("text", "x")), ct);
        string result = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("append", ("folder", "Growth"), ("file", "reported.md"), ("text", "- 2026-01-02 · Ada · two\nand a second line")), ct);

        Assert.Equal(existing + "- 2026-01-02 · Ada · two\nand a second line\n", await File.ReadAllTextAsync(path, ct));
        Assert.Equal(Appended(await File.ReadAllBytesAsync(path, ct)), result);
        Assert.DoesNotContain("appended", missing, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(teamDir, "other.md")));
    }

    /// <summary>A reported.md in Huddle-wide Memory is an ordinary file too: a missing one is refused as before.</summary>
    [Fact]
    public async Task Append_HuddleWideFolder_IsAnOrdinaryFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.MakeChiefOfStaff("Nova");

        string result = await harness.RunAsync(MemoryToolHarness.Args("append", ("folder", "memory"), ("file", "reported.md"), ("text", "Nova · one")), ct);

        Assert.DoesNotContain("appended", result, StringComparison.Ordinal);
    }

    /// <summary>Another file in the caller's own folder keeps today's behaviour: the text as given, no date, nothing dropped, a missing file refused.</summary>
    [Fact]
    public async Task Append_OtherFile_IsUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        string existing = Head + "- 2020-01-01 · Nova · ancient\n";
        string path = MemoryToolHarness.Put(harness.MemoryDir(), "notes.md", existing);

        string missing = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "other.md"), ("text", "x")), ct);
        string result = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "notes.md"), ("text", "- 2026-01-02 · one\ntwo")), ct);

        Assert.DoesNotContain("dropped", result, StringComparison.Ordinal);
        Assert.Equal(existing + "- 2026-01-02 · one\ntwo\n", await File.ReadAllTextAsync(path, ct));
        Assert.False(File.Exists(Path.Combine(harness.MemoryDir(), "other.md")));
        Assert.DoesNotContain("appended", missing, StringComparison.Ordinal);
    }

    /// <summary>The name is matched ignoring case, so REPORTED.md is the reported file in the caller's own folder.</summary>
    [Fact]
    public async Task Append_NameInAnotherCase_IsTheReportedFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        _ = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "reported.md"), ("text", "Nova · first")), ct);

        _ = await harness.RunAsync(MemoryToolHarness.Args("append", ("file", "REPORTED.md"), ("text", "- 2026-01-02 · Nova · one")), ct);

        string text = await File.ReadAllTextAsync(Path.Combine(harness.MemoryDir(), "reported.md"), ct);
        Assert.Equal(Head + "- 2026-11-16 · Nova · first\n- 2026-11-16 · Nova · one\n", text);
        Assert.Single(Directory.GetFiles(harness.MemoryDir(), "*.md"));
    }

    /// <summary>A write of reported.md treats it as any file: the text is kept as given.</summary>
    [Fact]
    public async Task Write_ReportedFile_IsTreatedAsAnyFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();

        _ = await harness.RunAsync(MemoryToolHarness.Args("write", ("file", "reported.md"), ("description", DefaultDescription), ("text", "- 2020-01-01 · Nova · as written\ntwo")), ct);

        string text = await File.ReadAllTextAsync(Path.Combine(harness.MemoryDir(), "reported.md"), ct);
        Assert.Equal(Head + "- 2020-01-01 · Nova · as written\ntwo\n", text);
    }

    /// <summary>The reply of a successful append to reported.md (spec §7): the verb, the version of the bytes now on disk, the file's description and any suffix.</summary>
    /// <param name="bytes">The file's bytes after the append.</param>
    /// <param name="suffix">The drop suffix, or empty.</param>
    private static string Appended(byte[] bytes, string suffix = "") =>
        $"appended to reported.md (version {MemoryVersion.Of(bytes)}); description: {DefaultDescription}{suffix}";

    private static Task<string> Run(MemoryToolHarness harness, string text, CancellationToken cancellationToken) =>
        harness.RunAsync(MemoryToolHarness.Args("append", ("file", "reported.md"), ("text", text)), cancellationToken);
}
