namespace Agency.Huddle.Tests.Memory;

using System.Text.Json.Nodes;
using System.Text;
using Microsoft.Extensions.Logging;
using Agency.Huddle.App.Memory;
using Agency.Huddle.App.Teams;

/// <summary>
/// Pins the <c>memory</c> App Tool over Team and Project folders (Instructions spec §5.3, §5.5): who may resolve a folder,
/// that only a Team's manager and the Chief of Staff write it, and that a folder name is matched against the catalog and
/// never joined from the raw argument.
/// </summary>
public sealed class MemoryToolTeamTests
{
    private const string GoodDescription = "How releases are cut and who signs them off. Read before shipping.";

    /// <summary>The file text the tool writes for <paramref name="body"/> under the shared good description.</summary>
    /// <param name="body">The body.</param>
    private static string Compose(string body) => MemoryFileRules.Compose(GoodDescription, body);

    /// <summary>The version token of the file the tool writes for <paramref name="body"/>.</summary>
    /// <param name="body">The body.</param>
    private static string VersionOfFile(string body) => MemoryVersion.Of(Encoding.UTF8.GetBytes(Compose(body)));

    /// <summary>The result of a successful <c>write</c> of <paramref name="body"/> to <paramref name="file"/>.</summary>
    /// <param name="file">The file name.</param>
    /// <param name="body">The body.</param>
    private static string Written(string file, string body) => $"written {file} (version {VersionOfFile(body)})";

    /// <summary>Dev: Ada manages, Leo is a member; one Project, Apollo. Ops has no manager.</summary>
    /// <param name="harness">The harness whose catalog is filled.</param>
    private static void Seed(MemoryToolHarness harness)
    {
        harness.Catalog.Teams =
        [
            new TeamSummary("Dev", ["Apollo"], ["Leo"], true) { Manager = "Ada" },
            new TeamSummary("Hidden", [], ["Zed"], true) { Manager = "Zed" },
            new TeamSummary("Ops", [], ["Leo"], true),
        ];
        Directory.CreateDirectory(Path.Combine(harness.TeamsRoot, "Dev", "Apollo"));
        Directory.CreateDirectory(Path.Combine(harness.TeamsRoot, "Ops"));
        Directory.CreateDirectory(Path.Combine(harness.TeamsRoot, "Hidden"));
    }

    /// <summary>A member reads a Team's Memory: list and read work, and the Team shows as a folder it may use.</summary>
    [Fact]
    public async Task Member_CanListAndReadTeamMemory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);
        MemoryToolHarness.Put(harness.TeamMemoryDir("Dev"), "release-process.md", $"---\ndescription: {GoodDescription}\n---\n\nTuesdays.\n");

        string listed = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("list", ("folder", "Dev")), ct);
        string read = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("read", ("folder", "Dev"), ("file", "release-process.md")), ct);
        string mine = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("list"), ct);

        string content = $"---\ndescription: {GoodDescription}\n---\n\nTuesdays.\n";
        Assert.Equal($"release-process.md - {GoodDescription}\nfolder: {harness.TeamMemoryDir("Dev")}", listed);
        Assert.Equal($"{content}\nversion: {MemoryVersion.Of(Encoding.UTF8.GetBytes(content))}", read);
        Assert.Equal($"Nothing yet.\nfolder: {harness.MemoryDir("Leo")}\nfolders you may use: me, Dev, Dev/Apollo, Ops", mine);
        Assert.DoesNotContain("Hidden", mine, StringComparison.Ordinal);
    }

    /// <summary>A member's write is refused naming the manager and the two alternatives, and writes nothing (U-5).</summary>
    [Fact]
    public async Task Member_WriteIsRefusedNamingTheManager()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string result = await harness.RunAsAsync(
            "Leo",
            MemoryToolHarness.Args("write", ("folder", "Dev"), ("file", "x.md"), ("description", GoodDescription), ("text", "x")),
            ct);

        Assert.Equal("Only Dev's manager, Ada, writes Dev's Team Memory. Tell Ada, or keep it in your own Memory.", result);
        Assert.False(File.Exists(Path.Combine(harness.TeamMemoryDir("Dev"), "x.md")));
        Assert.Single(harness.Logger.AtExactly(LogLevel.Warning));
    }

    /// <summary>Every mutating command is refused for a member, not only <c>write</c>.</summary>
    /// <param name="command">The command.</param>
    [Theory]
    [InlineData("append")]
    [InlineData("replace")]
    [InlineData("delete")]
    public async Task Member_EveryMutationIsRefused(string command)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);
        string path = MemoryToolHarness.Put(harness.TeamMemoryDir("Dev"), "x.md", "Original.");

        string result = await harness.RunAsAsync(
            "Leo",
            MemoryToolHarness.Args(command, ("folder", "Dev"), ("file", "x.md"), ("text", "y"), ("old", "Original"), ("new", "Changed"), ("version", "abc")),
            ct);

        Assert.Equal("Only Dev's manager, Ada, writes Dev's Team Memory. Tell Ada, or keep it in your own Memory.", result);
        Assert.Equal("Original.", await File.ReadAllTextAsync(path, ct));
        Assert.Empty(harness.Bin.Sent);
    }

    /// <summary>The manager writes the Team's Memory and a Project's, and replaces a reversed decision in place (U-6).</summary>
    [Fact]
    public async Task Manager_WritesTeamAndProjectMemory()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string team = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("write", ("folder", "Dev"), ("file", "release-process.md"), ("description", GoodDescription), ("text", "Releases ship on Fridays.")), ct);
        string project = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("write", ("folder", "Dev/Apollo"), ("file", "scope.md"), ("description", GoodDescription), ("text", "Apollo scope.")), ct);
        string replaced = await harness.RunAsAsync(
            "Ada",
            MemoryToolHarness.Args("replace", ("folder", "Dev"), ("file", "release-process.md"), ("old", "Releases ship on Fridays."), ("new", "Releases ship on Tuesdays (changed from Fridays on 2026-10-30).")),
            ct);

        const string Changed = "Releases ship on Tuesdays (changed from Fridays on 2026-10-30).";
        Assert.Equal(Written("release-process.md", "Releases ship on Fridays."), team);
        Assert.Equal(Written("scope.md", "Apollo scope."), project);
        Assert.Equal($"replaced in release-process.md (version {VersionOfFile(Changed)}); description: {GoodDescription}", replaced);
        Assert.Equal(Compose(Changed), await File.ReadAllTextAsync(Path.Combine(harness.TeamMemoryDir("Dev"), "release-process.md"), ct));
        Assert.True(File.Exists(Path.Combine(harness.TeamMemoryDir("Dev", "Apollo"), "scope.md")));
    }

    /// <summary>The Chief of Staff, which is in no Team, resolves and writes a Team it is not in (U-12) and sees every Team as a folder.</summary>
    [Fact]
    public async Task ChiefOfStaff_ResolvesAndWritesAnyTeam()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);
        harness.MakeChiefOfStaff("Chief");

        string written = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("write", ("folder", "Dev"), ("file", "cross-team.md"), ("description", GoodDescription), ("text", "Decision.")), ct);
        string listed = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("list"), ct);

        Assert.Equal(Written("cross-team.md", "Decision."), written);
        Assert.Equal($"Nothing yet.\nfolder: {harness.MemoryDir("Chief")}\nfolders you may use: me, Dev, Dev/Apollo, Hidden, Ops", listed);
    }

    /// <summary>A Team with no manager is written by an Agent only through the Chief of Staff; a member is told the Human writes it.</summary>
    [Fact]
    public async Task TeamWithNoManager_IsWrittenOnlyByTheChiefOfStaff()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);
        harness.MakeChiefOfStaff("Chief");
        JsonObject args = MemoryToolHarness.Args("write", ("folder", "Ops"), ("file", "x.md"), ("description", GoodDescription), ("text", "x"));

        string member = await harness.RunAsAsync("Leo", args, ct);
        string chief = await harness.RunAsAsync("Chief", MemoryToolHarness.Args("write", ("folder", "Ops"), ("file", "x.md"), ("description", GoodDescription), ("text", "x")), ct);

        Assert.Equal("Ops has no manager, so its Team Memory is written by the Human.", member);
        Assert.Equal(Written("x.md", "x"), chief);
    }

    /// <summary>A Team the caller is not in, an unknown Team, an unknown Project and a path-shaped folder all give the same refusal and touch nothing.</summary>
    /// <param name="folder">The folder argument as passed.</param>
    [Theory]
    [InlineData("Hidden")]
    [InlineData("Nope")]
    [InlineData("Dev/Nope")]
    [InlineData("Dev/memory")]
    [InlineData("Dev/Apollo/extra")]
    [InlineData("../Dev")]
    [InlineData("..\\..")]
    [InlineData("Dev/..")]
    [InlineData("/")]
    [InlineData("C:\\")]
    public async Task UnusableFolder_IsRefusedWithTheFoldersAllowed(string folder)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        string result = await harness.RunAsAsync("Leo", MemoryToolHarness.Args("write", ("folder", folder), ("file", "x.md"), ("description", GoodDescription), ("text", "x")), ct);

        Assert.Equal($"'{folder}' is not a Memory folder you can use. Use one of: me, Dev, Dev/Apollo, Ops.", result);
        Assert.False(Directory.Exists(Path.Combine(harness.TeamsRoot, "Hidden", "memory")));
    }

    /// <summary>Team and Project names match ignoring case, and the path is built from the catalog's spelling, never the argument.</summary>
    [Fact]
    public async Task FolderNames_MatchIgnoringCase_AndUseTheCatalogSpelling()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);

        _ = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("write", ("folder", "dEv/aPOLLO"), ("file", "scope.md"), ("description", GoodDescription), ("text", "x")), ct);

        Assert.True(File.Exists(Path.Combine(harness.TeamMemoryDir("Dev", "Apollo"), "scope.md")));
    }

    /// <summary>A Team whose folder is missing on disk is not a folder to write to.</summary>
    [Fact]
    public async Task TeamWithoutAFolder_IsRefused()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        harness.Catalog.Teams = [new TeamSummary("Ghost", [], ["Leo"], false) { Manager = "Ada" }];

        string result = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("write", ("folder", "Ghost"), ("file", "x.md"), ("description", GoodDescription), ("text", "x")), ct);

        Assert.Equal("'Ghost' is not a Memory folder you can use. Use one of: me.", result);
    }

    /// <summary>A Team's folder is created on the first write when the Team has none yet.</summary>
    [Fact]
    public async Task Manager_FirstWrite_CreatesTheMemoryFolder()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using MemoryToolHarness harness = new();
        Seed(harness);
        Assert.False(Directory.Exists(harness.TeamMemoryDir("Dev")));

        _ = await harness.RunAsAsync("Ada", MemoryToolHarness.Args("write", ("folder", "Dev"), ("file", "first.md"), ("description", GoodDescription), ("text", "x")), ct);

        Assert.True(File.Exists(Path.Combine(harness.TeamMemoryDir("Dev"), "first.md")));
    }
}
