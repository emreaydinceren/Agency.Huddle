using Agency.Huddle.App.FileChanges;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Tests for <see cref="TeamMemoryIndex"/>: one Persona's Team Memory snapshot, capped across its Teams (Spec §6.4, §8.4).</summary>
public sealed class TeamMemoryIndexTests
{
    /// <summary>The default cap, <c>Team:Teams:MaxMemoryEntries</c>.</summary>
    private const int DefaultCap = 50;

    /// <summary>A Team with a Team-wide fact and a Project with a fact gives one group whose Team-wide entries and Project entries are listed separately, with paths built under the Teams root.</summary>
    [Fact]
    public void Build_TeamAndProject_GroupsTeamWideAndProjectEntries()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string projectMemory = Path.Combine(root, "Business", "Marketing", "memory");
        string a = WriteFile(teamMemory, "a.md", "Invoices go out on the 1st.");
        string b = WriteFile(projectMemory, "b.md", "The campaign launches on 3 November.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        Assert.Equal("Business", group.Team);
        Assert.Equal(teamMemory, group.TeamMemoryPath);
        AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Invoices go out on the 1st.", a)], 0);
        (string Project, MemorySnapshot Memory) project = Assert.Single(group.Projects);
        Assert.Equal("Marketing", project.Project);
        AssertSnapshot(project.Memory, projectMemory, [new MemoryEntry("The campaign launches on 3 November.", b)], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A Project with no memory folder is still present, with an empty snapshot whose path is set (S2), and its sibling with a memory folder keeps its entries.</summary>
    [Fact]
    public void Build_ProjectWithoutMemoryFolder_IsPresentWithEmptySnapshot()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string designMemory = Path.Combine(root, "Business", "Design", "memory");
        string d = WriteFile(designMemory, "d.md", "Brand guide v2.");
        Directory.CreateDirectory(Path.Combine(root, "Business", "Marketing"));

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing", "Design")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        Assert.Equal(["Marketing", "Design"], group.Projects.Select(static project => project.Project).ToList());
        AssertSnapshot(group.Projects[0].Memory, Path.Combine(root, "Business", "Marketing", "memory"), [], 0);
        AssertSnapshot(group.Projects[1].Memory, designMemory, [new MemoryEntry("Brand guide v2.", d)], 0);
        AssertSnapshot(group.TeamWide, teamMemory, [], 0);
    }

    /// <summary>A Team whose folder exists but which has no memory folder still gives a group, with an empty Team-wide snapshot whose path is set (allowed neighbour of the no-group rows).</summary>
    [Fact]
    public void Build_TeamFolderWithoutMemoryFolder_GivesGroupWithEmptyTeamWide()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        Directory.CreateDirectory(Path.Combine(root, "Business"));

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        Assert.Equal("Business", group.Team);
        Assert.Equal(Path.Combine(root, "Business", "memory"), group.TeamMemoryPath);
        AssertSnapshot(group.TeamWide, Path.Combine(root, "Business", "memory"), [], 0);
        Assert.Empty(group.Projects);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A label that has no summary at all gives no group, even when a folder of that name holds memory on disk.</summary>
    [Fact]
    public void Build_LabelWithoutSummary_GivesNoGroup()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        _ = WriteFile(Path.Combine(root, "Ghost", "memory"), "g.md", "Ghost fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Ghost"), Teams(Summary("Business")), DefaultCap);

        Assert.Empty(snapshot.Groups);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A summary whose <c>HasFolder</c> is false gives no group even when a folder with memory exists on disk, while a labelled Team with a folder still does.</summary>
    [Fact]
    public void Build_SummaryWithoutFolder_GivesNoGroup()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        _ = WriteFile(Path.Combine(root, "Ghost", "memory"), "g.md", "Ghost fact.");
        _ = WriteFile(Path.Combine(root, "Business", "memory"), "a.md", "Real fact.");
        TeamSummary ghost = new("Ghost", [], ["nova"], false);

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Ghost", "Business"), Teams(ghost, Summary("Business")), DefaultCap);

        Assert.Equal(["Business"], snapshot.Groups.Select(static group => group.Team).ToList());
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A Persona with no Team labels gets an empty snapshot.</summary>
    [Fact]
    public void Build_NoLabels_GivesEmptySnapshot()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        _ = WriteFile(Path.Combine(root, "Business", "memory"), "a.md", "A fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels(), Teams(Summary("Business")), DefaultCap);

        Assert.Empty(snapshot.Groups);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>Labels that differ only in case give one group, named by the catalog's spelling, and the first-seen label wins the position.</summary>
    [Fact]
    public void Build_LabelsDifferingInCase_GiveOneGroup()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memory = Path.Combine(root, "Business", "memory");
        string a = WriteFile(memory, "a.md", "A fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "business", "BUSINESS"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        Assert.Equal("Business", group.Team);
        AssertSnapshot(group.TeamWide, memory, [new MemoryEntry("A fact.", a)], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>Groups follow the labels as written, not the order of the summaries or the folders.</summary>
    [Fact]
    public void Build_Groups_FollowLabelOrder()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        _ = WriteFile(Path.Combine(root, "Business", "memory"), "a.md", "Business fact.");
        _ = WriteFile(Path.Combine(root, "Sales", "memory"), "s.md", "Sales fact.");
        _ = WriteFile(Path.Combine(root, "Design", "memory"), "d.md", "Design fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Sales", "Design", "Business"), Teams(Summary("Business"), Summary("Design"), Summary("Sales")), DefaultCap);

        Assert.Equal(["Sales", "Design", "Business"], snapshot.Groups.Select(static group => group.Team).ToList());
    }

    /// <summary>Paths and the group's Team come from the summary's folder spelling, never from the label: label <c>business</c> with folder <c>Business</c> gives <c>Business</c> paths (on Linux the label's folder does not exist).</summary>
    [Fact]
    public void Build_LabelSpelledDifferently_BuildsPathsFromSummaryName()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string projectMemory = Path.Combine(root, "Business", "Marketing", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        string b = WriteFile(projectMemory, "b.md", "Project fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("business"), Teams(Summary("Business", "Marketing")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        Assert.Equal("Business", group.Team);
        Assert.Equal(teamMemory, group.TeamMemoryPath);
        AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
        AssertSnapshot(group.Projects[0].Memory, projectMemory, [new MemoryEntry("Project fact.", b)], 0);
    }

    /// <summary>Cap 3 across two Teams with 2 + 2 files lists three in Team order; the second Team lists one and reports one not listed, and the whole snapshot reports one.</summary>
    [Fact]
    public void Build_CapAcrossTwoTeams_ListsInTeamOrderAndCountsTheRest()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string businessMemory = Path.Combine(root, "Business", "memory");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        string a = WriteFile(businessMemory, "a.md", "Business a.");
        string b = WriteFile(businessMemory, "b.md", "Business b.");
        string c = WriteFile(salesMemory, "c.md", "Sales c.");
        _ = WriteFile(salesMemory, "d.md", "Sales d.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales"), Teams(Summary("Business"), Summary("Sales")), 3);

        Assert.Equal(2, snapshot.Groups.Count);
        AssertSnapshot(snapshot.Groups[0].TeamWide, businessMemory, [new MemoryEntry("Business a.", a), new MemoryEntry("Business b.", b)], 0);
        AssertSnapshot(snapshot.Groups[1].TeamWide, salesMemory, [new MemoryEntry("Sales c.", c)], 1);
        Assert.Equal(1, snapshot.NotListed);
    }

    /// <summary>The cap is spent Team-wide first, then Projects in the summary's order (not alphabetical, not disk order); once spent, later scopes only count.</summary>
    [Fact]
    public void Build_Cap_CoversTeamWideThenProjectsInSummaryOrder()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string zetaMemory = Path.Combine(root, "Business", "Zeta", "memory");
        string alphaMemory = Path.Combine(root, "Business", "Alpha", "memory");
        string z = WriteFile(zetaMemory, "z.md", "Zeta fact.");
        _ = WriteFile(alphaMemory, "a.md", "Alpha fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Zeta", "Alpha")), 1);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, Path.Combine(root, "Business", "memory"), [], 0);
        Assert.Equal(["Zeta", "Alpha"], group.Projects.Select(static project => project.Project).ToList());
        AssertSnapshot(group.Projects[0].Memory, zetaMemory, [new MemoryEntry("Zeta fact.", z)], 0);
        AssertSnapshot(group.Projects[1].Memory, alphaMemory, [], 1);
        Assert.Equal(1, snapshot.NotListed);
    }

    /// <summary>The Team-wide files use the cap before any Project: with cap 1 the Team-wide file is listed and the Project's file is counted.</summary>
    [Fact]
    public void Build_Cap_SpentByTeamWideLeavesProjectCounted()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string projectMemory = Path.Combine(root, "Business", "Marketing", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        _ = WriteFile(projectMemory, "b.md", "Project fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing")), 1);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
        AssertSnapshot(group.Projects[0].Memory, projectMemory, [], 1);
        Assert.Equal(1, snapshot.NotListed);
    }

    /// <summary>A cap equal to the file count lists everything and counts nothing (boundary neighbour of the cap rows), and a scope after the cap is spent with no files counts nothing.</summary>
    [Fact]
    public void Build_CapEqualToFileCount_ListsAllAndCountsNone()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string businessMemory = Path.Combine(root, "Business", "memory");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        string a = WriteFile(businessMemory, "a.md", "Business a.");
        string b = WriteFile(businessMemory, "b.md", "Business b.");
        string c = WriteFile(salesMemory, "c.md", "Sales c.");
        string d = WriteFile(salesMemory, "d.md", "Sales d.");
        Directory.CreateDirectory(Path.Combine(root, "Design"));

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales", "Design"), Teams(Summary("Business"), Summary("Sales"), Summary("Design")), 4);

        Assert.Equal(3, snapshot.Groups.Count);
        AssertSnapshot(snapshot.Groups[0].TeamWide, businessMemory, [new MemoryEntry("Business a.", a), new MemoryEntry("Business b.", b)], 0);
        AssertSnapshot(snapshot.Groups[1].TeamWide, salesMemory, [new MemoryEntry("Sales c.", c), new MemoryEntry("Sales d.", d)], 0);
        AssertSnapshot(snapshot.Groups[2].TeamWide, Path.Combine(root, "Design", "memory"), [], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A zero or negative cap lists nothing and counts every file (4 here, 2 per Team), without throwing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Build_NonPositiveCap_ListsNothingAndCountsEverything(int cap)
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string businessMemory = Path.Combine(root, "Business", "memory");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        _ = WriteFile(businessMemory, "a.md", "Business a.");
        _ = WriteFile(businessMemory, "b.md", "Business b.");
        _ = WriteFile(salesMemory, "c.md", "Sales c.");
        _ = WriteFile(salesMemory, "d.md", "Sales d.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales"), Teams(Summary("Business"), Summary("Sales")), cap);

        Assert.Equal(2, snapshot.Groups.Count);
        AssertSnapshot(snapshot.Groups[0].TeamWide, businessMemory, [], 2);
        AssertSnapshot(snapshot.Groups[1].TeamWide, salesMemory, [], 2);
        Assert.Equal(4, snapshot.NotListed);
    }

    /// <summary>Files are ordered ordinally by name, not case-insensitively and not numerically: <c>B.md</c> sorts before <c>a.md</c> and <c>10.md</c> before <c>2.md</c>.</summary>
    [Fact]
    public void Build_Files_AreOrderedOrdinallyByName()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memory = Path.Combine(root, "Business", "memory");
        string a = WriteFile(memory, "a.md", "Lower a.");
        string upperB = WriteFile(memory, "B.md", "Upper B.");
        string ten = WriteFile(memory, "10.md", "Ten.");
        string two = WriteFile(memory, "2.md", "Two.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(
            group.TeamWide,
            memory,
            [new MemoryEntry("Ten.", ten), new MemoryEntry("Two.", two), new MemoryEntry("Upper B.", upperB), new MemoryEntry("Lower a.", a)],
            0);
    }

    /// <summary>A non-<c>.md</c> file, a sub-folder and a folder named like a Markdown file are ignored, in the Team-wide and the Project folder alike, and are not counted as not listed.</summary>
    [Fact]
    public void Build_NonMarkdownFilesAndSubfolders_AreIgnored()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string projectMemory = Path.Combine(root, "Business", "Marketing", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        _ = WriteFile(teamMemory, "notes.txt", "Not a memory file.");
        _ = WriteFile(Path.Combine(teamMemory, "nested"), "inner.md", "In a sub-folder.");
        Directory.CreateDirectory(Path.Combine(teamMemory, "folder.md"));
        string b = WriteFile(projectMemory, "b.md", "Project fact.");
        _ = WriteFile(projectMemory, "b.txt", "Not a memory file.");
        _ = WriteFile(Path.Combine(projectMemory, "nested"), "inner.md", "In a sub-folder.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
        AssertSnapshot(group.Projects[0].Memory, projectMemory, [new MemoryEntry("Project fact.", b)], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>Blank leading lines are skipped, and a leading <c>#</c> and spaces are removed from the summary line.</summary>
    [Fact]
    public void Build_BlankLeadingLines_AreSkippedAndHeadingMarkerRemoved()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memory = Path.Combine(root, "Business", "memory");
        string a = WriteFile(memory, "a.md", "\r\n\r\n   \r\n## Invoices go out on the 1st.\r\nA second line.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, memory, [new MemoryEntry("Invoices go out on the 1st.", a)], 0);
    }

    /// <summary>A first line longer than 200 characters is cut at 200 and gets a trailing ellipsis; a line of exactly 200 is not cut (allowed neighbour).</summary>
    [Fact]
    public void Build_LongSummary_IsCutAtTwoHundredCharacters()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memory = Path.Combine(root, "Business", "memory");
        string exact = WriteFile(memory, "a-exact.md", new string('y', 200));
        string over = WriteFile(memory, "b-over.md", new string('x', 250));

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(
            group.TeamWide,
            memory,
            [new MemoryEntry(new string('y', 200), exact), new MemoryEntry(new string('x', 200) + "…", over)],
            0);
    }

    /// <summary>A file with no non-blank line is summarised by its file name without the extension.</summary>
    [Fact]
    public void Build_BlankFile_IsSummarisedByItsFileName()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memory = Path.Combine(root, "Business", "memory");
        string blank = WriteFile(memory, "launch-date.md", "\r\n   \r\n");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, memory, [new MemoryEntry("launch-date", blank)], 0);
    }

    /// <summary>A Team whose <c>memory</c> is a file, not a folder, gives an empty Team-wide snapshot with its path set, and other Teams are unaffected.</summary>
    [Fact]
    public void Build_TeamMemoryIsAFile_GivesEmptySnapshot()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string memoryFile = WriteFile(Path.Combine(root, "Business"), "memory", "I am a file, not a folder.");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        string c = WriteFile(salesMemory, "c.md", "Sales fact.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales"), Teams(Summary("Business"), Summary("Sales")), DefaultCap);

        Assert.Equal(2, snapshot.Groups.Count);
        AssertSnapshot(snapshot.Groups[0].TeamWide, memoryFile, [], 0);
        AssertSnapshot(snapshot.Groups[1].TeamWide, salesMemory, [new MemoryEntry("Sales fact.", c)], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A Project whose <c>memory</c> is a file gives an empty snapshot with its path set, and the Team-wide memory still lists.</summary>
    [Fact]
    public void Build_ProjectMemoryIsAFile_GivesEmptySnapshot()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        string memoryFile = WriteFile(Path.Combine(root, "Business", "Marketing"), "memory", "I am a file, not a folder.");

        TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing")), DefaultCap);

        TeamMemoryGroup group = Assert.Single(snapshot.Groups);
        AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
        AssertSnapshot(group.Projects[0].Memory, memoryFile, [], 0);
        Assert.Equal(0, snapshot.NotListed);
    }

    /// <summary>A Team-wide <c>memory/</c> folder that cannot be listed counts as empty and does not throw, and the next Team still lists. Runs on both OSes (the listing denial is icacls on Windows, a Unix file mode elsewhere); it skips only when the OS does not enforce the denial (a root user).</summary>
    [Fact]
    public void Build_UnreadableTeamMemoryFolder_CountsAsEmptyAndDoesNotThrow()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string locked = Path.Combine(root, "Business", "memory");
        _ = WriteFile(locked, "secret.md", "Hidden fact.");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        string c = WriteFile(salesMemory, "c.md", "Sales fact.");
        TestListing.DenyListing(locked);
        try
        {
            if (!TestListing.ListingIsDenied(locked))
            {
                Assert.Skip("This user can list a folder whose listing was denied (running as root), so the denial cannot be arranged.");
            }

            TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales"), Teams(Summary("Business"), Summary("Sales")), DefaultCap);

            Assert.Equal(2, snapshot.Groups.Count);
            AssertSnapshot(snapshot.Groups[0].TeamWide, locked, [], 0);
            AssertSnapshot(snapshot.Groups[1].TeamWide, salesMemory, [new MemoryEntry("Sales fact.", c)], 0);
            Assert.Equal(0, snapshot.NotListed);
        }
        finally
        {
            TestListing.GrantListing(locked);
        }
    }

    /// <summary>A Project's <c>memory/</c> folder that cannot be listed counts as empty and does not throw, while the Team-wide memory and a sibling Project still list.</summary>
    [Fact]
    public void Build_UnreadableProjectMemoryFolder_KeepsTheOtherScopes()
    {
        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string locked = Path.Combine(root, "Business", "Marketing", "memory");
        string designMemory = Path.Combine(root, "Business", "Design", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        _ = WriteFile(locked, "secret.md", "Hidden fact.");
        string d = WriteFile(designMemory, "d.md", "Design fact.");
        TestListing.DenyListing(locked);
        try
        {
            if (!TestListing.ListingIsDenied(locked))
            {
                Assert.Skip("This user can list a folder whose listing was denied (running as root), so the denial cannot be arranged.");
            }

            TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing", "Design")), DefaultCap);

            TeamMemoryGroup group = Assert.Single(snapshot.Groups);
            AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
            Assert.Equal(["Marketing", "Design"], group.Projects.Select(static project => project.Project).ToList());
            AssertSnapshot(group.Projects[0].Memory, locked, [], 0);
            AssertSnapshot(group.Projects[1].Memory, designMemory, [new MemoryEntry("Design fact.", d)], 0);
            Assert.Equal(0, snapshot.NotListed);
        }
        finally
        {
            TestListing.GrantListing(locked);
        }
    }

    /// <summary>A Team folder that is a reparse point (a junction) is skipped: no group, and what it points at is never listed, while a real Team beside it still gives its group.</summary>
    [Fact]
    public void Build_TeamFolderIsAJunction_IsSkipped()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only: junction");
        }

        using TempDataDir dir = new();
        string root = Root(dir);
        string real = Path.Combine(dir.Path, "outside", "BusinessReal");
        _ = WriteFile(Path.Combine(real, "memory"), "a.md", "Followed through a junction.");
        string salesMemory = Path.Combine(root, "Sales", "memory");
        string c = WriteFile(salesMemory, "c.md", "Sales fact.");
        string link = Path.Combine(root, "Business");
        if (!TestLinks.TryCreateLink(link, real))
        {
            Assert.Skip("The junction could not be created.");
        }

        try
        {
            TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business", "Sales"), Teams(Summary("Business"), Summary("Sales")), DefaultCap);

            TeamMemoryGroup group = Assert.Single(snapshot.Groups);
            Assert.Equal("Sales", group.Team);
            AssertSnapshot(group.TeamWide, salesMemory, [new MemoryEntry("Sales fact.", c)], 0);
            Assert.Equal(0, snapshot.NotListed);
        }
        finally
        {
            TestLinks.RemoveLink(link);
        }
    }

    /// <summary>A Project folder that is a junction is skipped (left out of the group's Projects) and what it points at is never listed, while a real sibling Project and the Team-wide memory still list.</summary>
    [Fact]
    public void Build_ProjectFolderIsAJunction_IsSkipped()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only: junction");
        }

        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string designMemory = Path.Combine(root, "Business", "Design", "memory");
        string a = WriteFile(teamMemory, "a.md", "Team fact.");
        string d = WriteFile(designMemory, "d.md", "Design fact.");
        string real = Path.Combine(dir.Path, "outside", "MarketingReal");
        _ = WriteFile(Path.Combine(real, "memory"), "m.md", "Followed through a junction.");
        string link = Path.Combine(root, "Business", "Marketing");
        if (!TestLinks.TryCreateLink(link, real))
        {
            Assert.Skip("The junction could not be created.");
        }

        try
        {
            TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing", "Design")), DefaultCap);

            TeamMemoryGroup group = Assert.Single(snapshot.Groups);
            AssertSnapshot(group.TeamWide, teamMemory, [new MemoryEntry("Team fact.", a)], 0);
            Assert.Equal(["Design"], group.Projects.Select(static project => project.Project).ToList());
            AssertSnapshot(group.Projects[0].Memory, designMemory, [new MemoryEntry("Design fact.", d)], 0);
            Assert.Equal(0, snapshot.NotListed);
        }
        finally
        {
            TestLinks.RemoveLink(link);
        }
    }

    /// <summary>A <c>memory/</c> folder that is a junction gives an empty snapshot with its path set and what it points at is never listed, in the Team-wide slot and a Project slot alike, while a real Project memory still lists.</summary>
    [Fact]
    public void Build_MemoryFolderIsAJunction_GivesEmptySnapshot()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Windows-only: junction");
        }

        using TempDataDir dir = new();
        string root = Root(dir);
        string teamMemory = Path.Combine(root, "Business", "memory");
        string designMemory = Path.Combine(root, "Business", "Design", "memory");
        string marketingMemory = Path.Combine(root, "Business", "Marketing", "memory");
        string d = WriteFile(designMemory, "d.md", "Design fact.");
        string realTeam = Path.Combine(dir.Path, "outside", "team-memory");
        string realProject = Path.Combine(dir.Path, "outside", "project-memory");
        _ = WriteFile(realTeam, "a.md", "Followed through a junction.");
        _ = WriteFile(realProject, "m.md", "Followed through a junction.");
        Directory.CreateDirectory(Path.Combine(root, "Business", "Marketing"));
        if (!TestLinks.TryCreateLink(teamMemory, realTeam))
        {
            Assert.Skip("The junction could not be created.");
        }

        try
        {
            if (!TestLinks.TryCreateLink(marketingMemory, realProject))
            {
                Assert.Skip("The junction could not be created.");
            }

            try
            {
                TeamMemorySnapshot snapshot = TeamMemoryIndex.Build(root, Labels("Business"), Teams(Summary("Business", "Marketing", "Design")), DefaultCap);

                TeamMemoryGroup group = Assert.Single(snapshot.Groups);
                AssertSnapshot(group.TeamWide, teamMemory, [], 0);
                Assert.Equal(["Marketing", "Design"], group.Projects.Select(static project => project.Project).ToList());
                AssertSnapshot(group.Projects[0].Memory, marketingMemory, [], 0);
                AssertSnapshot(group.Projects[1].Memory, designMemory, [new MemoryEntry("Design fact.", d)], 0);
                Assert.Equal(0, snapshot.NotListed);
            }
            finally
            {
                TestLinks.RemoveLink(marketingMemory);
            }
        }
        finally
        {
            TestLinks.RemoveLink(teamMemory);
        }
    }

    /// <summary>The Teams root under the test's data folder.</summary>
    /// <param name="dir">The temp data folder.</param>
    private static string Root(TempDataDir dir) => Path.Combine(dir.Path, "Teams");

    /// <summary>Writes <paramref name="content"/> to <paramref name="name"/> inside <paramref name="folder"/>, creating the folder.</summary>
    /// <param name="folder">The folder to write into.</param>
    /// <param name="name">The file name.</param>
    /// <param name="content">The file's text.</param>
    /// <returns>The file's full path, as <c>Directory.EnumerateFiles</c> reports it.</returns>
    private static string WriteFile(string folder, string name, string content)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Team labels as a Persona's frontmatter writes them.</summary>
    /// <param name="labels">The labels, in order.</param>
    private static string[] Labels(params string[] labels) => labels;

    /// <summary>Team summaries as the catalog reports them.</summary>
    /// <param name="teams">The summaries.</param>
    private static TeamSummary[] Teams(params TeamSummary[] teams) => teams;

    /// <summary>A summary of a Team with a folder on disk.</summary>
    /// <param name="name">The catalog's folder spelling.</param>
    /// <param name="projects">The Project folder names, in the order the catalog lists them.</param>
    private static TeamSummary Summary(string name, params string[] projects) => new(name, projects, ["nova"], true);

    /// <summary>Asserts a snapshot's path, whole entry list and not-listed count. Records hold their lists by reference, so the parts are compared one by one.</summary>
    /// <param name="actual">The snapshot under test.</param>
    /// <param name="memoryPath">The expected memory folder path.</param>
    /// <param name="entries">The expected entries, in order.</param>
    /// <param name="notListed">The expected not-listed count.</param>
    private static void AssertSnapshot(MemorySnapshot actual, string memoryPath, IReadOnlyList<MemoryEntry> entries, int notListed)
    {
        Assert.Equal(memoryPath, actual.MemoryPath);
        Assert.Equal(entries, actual.Entries);
        Assert.Equal(notListed, actual.NotListed);
    }
}
