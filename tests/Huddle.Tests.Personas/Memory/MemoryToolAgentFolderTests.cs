namespace Agency.Huddle.Tests.Memory;

using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;

/// <summary>
/// Pins the <c>agent:&lt;Name&gt;</c> value of the <c>memory</c> tool's <c>folder</c> argument (Memory Tidy spec §6.4): the Chief of Staff
/// reads one manager's <c>reported.md</c> and nothing else, and the value never becomes a folder anything can be written to.
/// </summary>
public sealed class MemoryToolAgentFolderTests
{
    private const string Reported = "---\ndescription: What I have already told the Human, by date and Room.\n---\n\n- 2026-11-16 · Ada · budget is over plan\n";

    private const string Nothing = " has told the Human nothing directly.";

    private static MemoryToolHarness Build(Action<TeamOptions>? configure = null, bool links = false)
    {
        MemoryToolHarness harness = new(configure, links);
        harness.MakeChiefOfStaff("Chief");
        harness.Catalog.Teams =
        [
            new TeamSummary("Growth", [], ["Leo"], true) { Manager = "Ada" },
            new TeamSummary("Ops", [], ["Mia"], true) { Manager = "Bo" },
        ];
        return harness;
    }

    private static JsonObject ReadReported(string folder) => MemoryToolHarness.Args("read", ("folder", folder), ("file", "reported.md"));

    /// <summary>The Chief of Staff reads a manager's reported.md and gets its text and version.</summary>
    [Fact]
    public async Task Read_ChiefOfStaffOfAManager_ReturnsTheFileAndItsVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        string path = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Ada"), ct);

        Assert.Equal(Reported + "\nversion: " + MemoryVersion.Of(await File.ReadAllBytesAsync(path, ct)), result);
    }

    /// <summary>A manager with a folder but no reported.md has told the Human nothing directly.</summary>
    [Fact]
    public async Task Read_ManagerWithoutTheFile_AnswersNothingDirectly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        Directory.CreateDirectory(harness.MemoryDir("Ada"));

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Ada"), ct);

        Assert.Equal("Ada" + Nothing, result);
    }

    /// <summary>A manager with no folder at all is answered as one with no file.</summary>
    [Fact]
    public async Task Read_ManagerWithoutAFolder_AnswersNothingDirectly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Bo"), ct);

        Assert.Equal("Bo" + Nothing, result);
    }

    /// <summary>An unknown Name, a member and a removed Agent are no manager: the sentence lists today's managers.</summary>
    [Theory]
    [InlineData("Zed")]
    [InlineData("Leo")]
    [InlineData("Old")]
    public async Task Read_NameThatIsNoManager_ListsTheManagers(string name)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        _ = MemoryToolHarness.Put(harness.MemoryDir(name), "reported.md", Reported);

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:" + name), ct);

        Assert.Equal($"No Team's manager is named {name}, so there is nothing to read. Managers today: Ada, Bo.", result);
    }

    /// <summary>With no Team at all the sentence still reads, and says there are no managers.</summary>
    [Fact]
    public async Task Read_NoManagersAtAll_SaysNone()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        harness.Catalog.Teams = [];

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Ada"), ct);

        Assert.Equal("No Team's manager is named Ada, so there is nothing to read. Managers today: none.", result);
    }

    /// <summary>Every command but read and list is refused, and nothing is written.</summary>
    [Theory]
    [InlineData("write")]
    [InlineData("append")]
    [InlineData("replace")]
    [InlineData("delete")]
    public async Task Mutation_InAnAgentFolder_IsRefusedAndWritesNothing(string command)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        string path = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        string result = await harness.RunAsAsync(
            "Chief",
            MemoryToolHarness.Args(command, ("folder", "agent:Ada"), ("file", "reported.md"), ("description", "A new description that is long enough."), ("text", "x"), ("old", "Ada"), ("new", "Eve"), ("version", "v")),
            ct);

        Assert.Equal("In another Agent's folder you can only read reported.md.", result);
        Assert.Equal(Reported, await File.ReadAllTextAsync(path, ct));
        Assert.False(File.Exists(Path.Combine(harness.MemoryDir("Ada"), "x.md")));
    }

    /// <summary>Any file but reported.md is refused, even one that exists.</summary>
    [Theory]
    [InlineData("notes.md")]
    [InlineData("MEMORY.md")]
    [InlineData("../reported.md")]
    public async Task Read_OtherFile_IsRefused(string file)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "notes.md", Reported);

        string result = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("read", ("folder", "agent:Ada"), ("file", file)), ct);

        Assert.Equal("In another Agent's folder you can only read reported.md.", result);
    }

    /// <summary>List answers with the one file and its description, or with the nothing-told sentence.</summary>
    [Fact]
    public async Task List_ManagerFolder_AnswersTheOneFileOrNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "other.md", Reported);

        string listed = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("list", ("folder", "agent:Ada")), ct);
        string none = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("list", ("folder", "agent:Bo")), ct);

        Assert.Equal("reported.md - What I have already told the Human, by date and Room.", listed);
        Assert.Equal("Bo" + Nothing, none);
    }

    /// <summary>A member and a manager are refused the value, as for any folder they may not use, with a Warning logged.</summary>
    [Theory]
    [InlineData("Leo")]
    [InlineData("Ada")]
    public async Task Read_CallerIsNotTheChiefOfStaff_IsRefusedTheFolder(string caller)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        string result = await harness.RunAsAsync(caller, ReadReported("agent:Ada"), ct);

        Assert.Equal("'agent:Ada' is not a Memory folder you can use. Use one of: me, memory, Growth.", result);
        Assert.NotEmpty(harness.Logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>A traversal or a path after agent: names no manager and reads nothing.</summary>
    [Theory]
    [InlineData("agent:../x")]
    [InlineData("agent:Ada/work")]
    [InlineData("agent:..\\Ada")]
    [InlineData("agent:C:\\Windows")]
    public async Task Read_TraversalOrPath_NamesNoManager(string folder)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        string result = await harness.RunAsAsync("Chief", ReadReported(folder), ct);

        Assert.Equal($"No Team's manager is named {folder["agent:".Length..]}, so there is nothing to read. Managers today: Ada, Bo.", result);
    }

    /// <summary>The prefix and the Name match ignoring case, and the answer carries the catalog's spelling.</summary>
    [Fact]
    public async Task Read_PrefixAndNameInAnyCase_Match()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        string path = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);
        Directory.CreateDirectory(harness.MemoryDir("Bo"));

        string read = await harness.RunAsAsync("Chief", ReadReported("AGENT:aDa"), ct);
        string none = await harness.RunAsAsync("Chief", ReadReported("Agent:bo"), ct);

        Assert.Equal(Reported + "\nversion: " + MemoryVersion.Of(await File.ReadAllBytesAsync(path, ct)), read);
        Assert.Equal("Bo" + Nothing, none);
    }

    /// <summary>A Team file that spells the manager in lower case finds the folder in its own spelling.</summary>
    [Fact]
    public void Resolve_ManagerSpelledInLowerCase_UsesTheDirectoryOwnSpelling()
    {
        using MemoryToolHarness harness = Build();
        harness.Catalog.Teams = [new TeamSummary("Growth", [], [], true) { Manager = "ada" }];
        string expected = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        bool found = harness.Paths().TryResolveReportedOf("Chief", "ADA", out string? manager, out string? path);

        Assert.True(found);
        Assert.Equal("ada", manager);
        Assert.Equal(expected, path);
        Assert.NotNull(path);
        Assert.Equal("Ada", Path.GetRelativePath(harness.AgentsRoot, path).Split(Path.DirectorySeparatorChar)[0]);
    }

    /// <summary>A manager spelled <c>..</c> or holding a separator finds no folder and is answered as having told nothing.</summary>
    [Theory]
    [InlineData("..")]
    [InlineData("../Ada")]
    [InlineData("Ada/work")]
    public async Task Read_HostileManagerNameInATeamFile_FindsNothing(string spelling)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();
        harness.Catalog.Teams = [new TeamSummary("Growth", [], [], true) { Manager = spelling }];
        _ = MemoryToolHarness.Put(harness.MemoryDir("Ada"), "reported.md", Reported);

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:" + spelling), ct);
        bool found = harness.Paths().TryResolveReportedOf("Chief", spelling, out _, out string? path);

        Assert.Equal(spelling + Nothing, result);
        Assert.False(found);
        Assert.Null(path);
    }

    /// <summary>A Work Dir setting that leaves the Agent's folder puts the file outside the one allowed place, so nothing is read.</summary>
    [Fact]
    public async Task Read_WorkDirLeavingTheAgentFolder_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build(options => options.Acp.WorkDir = "..");
        string outside = Path.Combine(harness.AgentsRoot, "memory");
        _ = MemoryToolHarness.Put(outside, "reported.md", Reported);

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Ada"), ct);
        bool found = harness.Paths().TryResolveReportedOf("Chief", "Ada", out _, out _);

        Assert.Equal("Ada" + Nothing, result);
        Assert.False(found);
    }

    /// <summary>A junction or link on the way (the Agent's folder, or its memory folder) is refused.</summary>
    [Theory]
    [InlineData("agent")]
    [InlineData("memory")]
    public async Task Read_LinkOnTheWay_IsRefused(string linked)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build(links: true);
        string target = Path.Combine(harness.AgentsRoot, "target");
        _ = MemoryToolHarness.Put(linked == "agent" ? Path.Combine(target, "work", "memory") : target, "reported.md", Reported);
        string link = linked == "agent" ? Path.Combine(harness.AgentsRoot, "Ada") : harness.MemoryDir("Ada");
        Directory.CreateDirectory(Path.GetDirectoryName(link) ?? harness.AgentsRoot);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip("This environment cannot create directory links: " + ex.Message);
        }

        string result = await harness.RunAsAsync("Chief", ReadReported("agent:Ada"), ct);
        bool found = harness.Paths().TryResolveReportedOf("Chief", "Ada", out _, out _);

        Assert.Equal("Ada" + Nothing, result);
        Assert.False(found);
    }

    /// <summary>ResolveFolder and TryResolve answer null for an agent: value, for every file name, for the Chief of Staff too.</summary>
    [Theory]
    [InlineData("reported.md")]
    [InlineData("notes.md")]
    [InlineData("MEMORY.md")]
    [InlineData("")]
    public void ResolveFolderAndTryResolve_AgentValue_AreNull(string file)
    {
        using MemoryToolHarness harness = Build();
        MemoryPaths paths = harness.Paths();

        Assert.Null(paths.ResolveFolder("Chief", "agent:Ada"));
        Assert.Null(paths.ResolveFolder("Ada", "agent:Ada"));
        Assert.False(paths.TryResolve("Chief", "agent:Ada", file, out string? path));
        Assert.Null(path);
    }

    /// <summary>The usable-folders sentence lists agent:Manager for each manager, for the Chief of Staff only.</summary>
    [Fact]
    public async Task List_OwnFolder_OffersTheAgentFoldersToTheChiefOfStaffOnly()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = Build();

        string chief = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("list"), ct);
        string member = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("list"), ct);
        string manager = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("list"), ct);

        Assert.Equal("folders you may use: me, memory, Growth, Ops, agent:Ada, agent:Bo", chief.Split('\n')[^1]);
        Assert.Equal("folders you may use: me, memory, Growth", member.Split('\n')[^1]);
        Assert.Equal("folders you may use: me, memory, Growth", manager.Split('\n')[^1]);
    }
}
