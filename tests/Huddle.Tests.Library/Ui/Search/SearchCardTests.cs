namespace Agency.Huddle.Tests.Ui.Search;

using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Search;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Fakes;

/// <summary>
/// Pins spec §6.6 and §6.7 for the five search cards and the <see cref="SearchCard"/> switch: each
/// card's text, its meta line in the spec's exact wording, the bold match, the dimmed Archived Room
/// and Closed Task, the accessible name's closing word, and the empty action slot (D-12).
/// </summary>
public sealed class SearchCardTests : IDisposable
{
    private readonly TempDataDir dataDir = new();
    private readonly AvatarStore avatarStore;

    /// <summary>Builds a real, empty <see cref="AvatarStore"/>, since <see cref="AgentSearchCard"/> injects it for the avatar.</summary>
    public SearchCardTests()
    {
        this.avatarStore = new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance);
    }

    /// <summary>Releases the temporary data directory backing the avatar store.</summary>
    public void Dispose() => this.dataDir.Dispose();

    /// <summary>A Team with a type and four Agents shows its name, then <c>Development Team · 4 Agents</c>.</summary>
    [Fact]
    public async Task TeamCard_RendersNameTypeAndCount()
    {
        await using MudBunitContext ctx = new();
        TeamHit hit = new("Marketing", "Development Team", 4, MatchTier.ExactName);

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Marketing", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("Development Team · 4 Agents", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Blank Team reaches the card with its id as the type, and the card shows the display name <c>Blank Team</c>, never the raw <c>blank</c> (#477).</summary>
    [Fact]
    public async Task TeamCard_BlankType_ShowsItsDisplayName()
    {
        await using MudBunitContext ctx = new();
        TeamHit hit = new("Sandbox", "blank", 4, MatchTier.ExactName);

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Blank Team · 4 Agents", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Library folder result draws the outlined folder glyph, like the file results and the rest of the app, not the solid one (#477).</summary>
    [Fact]
    public async Task LibraryCard_Folder_UsesTheOutlinedFolderIcon()
    {
        await using MudBunitContext ctx = new();
        LibraryHit hit = new("root", "Marketing", "Launch", string.Empty, true, null, null, MatchTier.ExactName);

        IRenderedComponent<LibrarySearchCard> cut = ctx.Render<LibrarySearchCard>(p => p.Add(c => c.Hit, hit));

        string? drawn = cut.FindAll("svg path")[^1].GetAttribute("d");
        Assert.Equal(MudBlazor.Icons.Material.Outlined.Folder.Split("d=\"")[^1].Split('"')[0], drawn);
    }

    /// <summary>A Team with one Agent and no type says <c>1 Agent</c>, not <c>1 Agents</c>.</summary>
    [Fact]
    public async Task TeamCard_OneAgent_IsSingular()
    {
        await using MudBunitContext ctx = new();
        TeamHit hit = new("Platform", null, 1, MatchTier.NamePrefix);

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("1 Agent", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>An Agent shows its Name, then its Title and Teams, joined as <c>Product Manager · Marketing, Platform</c>.</summary>
    [Fact]
    public async Task AgentProfileCard_RendersNameTitleAndTeams()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);
        AgentHit hit = new("Nova", "Product Manager", "nova", ["Marketing", "Platform"], MatchTier.ExactName);

        IRenderedComponent<AgentSearchCard> cut = ctx.Render<AgentSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Nova", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("Product Manager · Marketing, Platform", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>An Agent with no Team says <c>No team</c> in place of the Team list (OP-8).</summary>
    [Fact]
    public async Task AgentProfileCard_NoTeam_SaysNoTeam()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);
        AgentHit hit = new("Nova", "Product Manager", "nova", [], MatchTier.ExactName);

        IRenderedComponent<AgentSearchCard> cut = ctx.Render<AgentSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Product Manager · No team", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A content hit shows its folder, then a snippet whose matched word is the only text inside a <c>mark</c>.</summary>
    [Fact]
    public async Task LibraryCard_ContentHit_RendersSnippetWithBoldMatch()
    {
        await using MudBunitContext ctx = new();
        LibraryHit hit = new("root", "Marketing", "Launch/plan.md", "Launch", false, "the login flow moves to SSO", null, MatchTier.ContentMatch);

        IRenderedComponent<LibrarySearchCard> cut = ctx.Render<LibrarySearchCard>(p => p.Add(c => c.Hit, hit).Add(c => c.Phrase, "login"));

        Assert.Equal("plan.md", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("Launch", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Equal("login", cut.Find(".search-card-snippet mark").TextContent);
    }

    /// <summary>A file at the root of its Library Root shows the Root's name where the folder path would be.</summary>
    [Fact]
    public async Task LibraryCard_FolderAtRoot_ShowsRootName()
    {
        await using MudBunitContext ctx = new();
        LibraryHit hit = new("root", "Marketing", "plan.md", string.Empty, false, null, null, MatchTier.ExactName);

        IRenderedComponent<LibrarySearchCard> cut = ctx.Render<LibrarySearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Marketing", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Task's title is <c>SUP-0012 · Support SAML login</c> with the status chip in the trailing slot; its meta line is <c>Nova · Staff Engineer · Platform Team</c>.</summary>
    [Fact]
    public async Task TaskCard_RendersBothLines()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.InProgress, "Nova", "Platform", false, MatchTier.ExactName, "Staff Engineer");

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("SUP-0012 · Support SAML login", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("In Progress", cut.Find(".search-card-action .search-card-chip").TextContent.Trim());
        Assert.Equal("Nova · Staff Engineer · Platform Team", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Equal("· Staff Engineer", cut.Find(".search-card-subtitle").TextContent.Trim());
    }

    /// <summary>A Team name that already ends in "Team" (any case) is not given a second suffix.</summary>
    [Theory]
    [InlineData("Growth Team", "Growth Team")]
    [InlineData("growth team", "growth team")]
    [InlineData("Platform", "Platform Team")]
    public async Task TaskCard_TeamSuffix_AppendedOnlyWhenMissing(string team, string expected)
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, "Nova", team, false, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal($"Nova · {expected}", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>An unassigned Task's second line is the Team at the right alone: no assignee part and no dot.</summary>
    [Fact]
    public async Task TaskCard_NoAssignee_ShowsTeamOnly()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Platform Team", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Empty(cut.FindAll(".search-card-subtitle"));
    }

    /// <summary>An assignee with no Persona title shows the name only.</summary>
    [Fact]
    public async Task TaskCard_AssigneeWithoutTitle_ShowsNameOnly()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, "Nova", "Platform", false, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Nova · Platform Team", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Empty(cut.FindAll(".search-card-subtitle"));
    }

    /// <summary>A Room shows its name, then <c>14 matches · Kai · HH:mm</c> in the local clock, then the snippet with the match in bold.</summary>
    [Fact]
    public async Task RoomCard_RendersCountSenderTimeAndSnippet()
    {
        await using MudBunitContext ctx = new();
        DateTimeOffset time = new(2026, 10, 12, 14, 2, 0, TimeSpan.Zero);
        RoomHit hit = new("room-1", "findings", 14, "msg-1", "Kai", "we should drop the login cookie", time, false, MatchTier.ContentMatch);
        ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider(time.AddHours(1), TimeZoneInfo.Utc));
        const string clock = "14:02";

        IRenderedComponent<RoomSearchCard> cut = ctx.Render<RoomSearchCard>(p => p.Add(c => c.Hit, hit).Add(c => c.Phrase, "login"));

        Assert.Equal("findings", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal($"14 matches · Kai · {clock}", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Equal("login", cut.Find(".search-card-snippet mark").TextContent);
    }

    /// <summary>A Room with one match says <c>1 match</c>, not <c>1 matches</c>.</summary>
    [Fact]
    public async Task RoomCard_OneMatch_IsSingular()
    {
        await using MudBunitContext ctx = new();
        DateTimeOffset time = new(2026, 10, 12, 14, 2, 0, TimeSpan.Zero);
        RoomHit hit = new("room-1", "findings", 1, "msg-1", "Kai", "login", time, false, MatchTier.ContentMatch);
        ctx.Services.AddSingleton<TimeProvider>(new ManualTimeProvider(time.AddHours(1), TimeZoneInfo.Utc));
        const string clock = "14:02";

        IRenderedComponent<RoomSearchCard> cut = ctx.Render<RoomSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal($"1 match · Kai · {clock}", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>Every card reserves its right-hand action slot as an empty, hidden-from-assistive-tech element (D-12).</summary>
    /// <param name="kind">Which card's sample hit to render through the switch.</param>
    [Theory]
    [InlineData("team")]
    [InlineData("agent")]
    [InlineData("library")]
    [InlineData("room")]
    public async Task EveryCard_HasAnEmptyActionSlot(string kind)
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);

        IRenderedComponent<SearchCard> cut = ctx.Render<SearchCard>(p => p.Add(c => c.Hit, SampleHit(kind)));

        IElement slot = cut.Find(".search-card-action");
        Assert.Equal(string.Empty, slot.TextContent);
        Assert.Equal("true", slot.GetAttribute("aria-hidden"));
    }

    /// <summary>The switch renders the card that matches the hit's subtype, here a Team hit becoming a <see cref="TeamSearchCard"/>.</summary>
    [Fact]
    public async Task SearchCard_PicksTheCardBySubtype()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);

        IRenderedComponent<SearchCard> cut = ctx.Render<SearchCard>(p => p.Add(c => c.Hit, SampleHit("team")));

        _ = cut.FindComponent<TeamSearchCard>();
        Assert.Empty(cut.FindAll(".search-card-archived"));
    }

    /// <summary>An Archived Room is dimmed by its class and its accessible name ends with <c>, archived</c>.</summary>
    [Fact]
    public async Task ArchivedRoom_AccessibleNameEndsWithArchived()
    {
        await using MudBunitContext ctx = new();
        DateTimeOffset time = new(2026, 10, 12, 14, 2, 0, TimeSpan.Zero);
        RoomHit hit = new("room-1", "findings", 1, "msg-1", "Kai", "login", time, true, MatchTier.ExactName);

        IRenderedComponent<RoomSearchCard> cut = ctx.Render<RoomSearchCard>(p => p.Add(c => c.Hit, hit));

        IElement card = cut.Find(".search-card");
        Assert.Contains("search-card-archived", card.ClassName ?? string.Empty, StringComparison.Ordinal);
        Assert.EndsWith(", archived", card.GetAttribute("aria-label"), StringComparison.Ordinal);
    }

    /// <summary>A Closed Task's accessible name ends with <c>, closed</c>.</summary>
    [Fact]
    public async Task ClosedTask_AccessibleNameEndsWithClosed()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.Done, "Nova", "Platform", true, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.EndsWith(", closed", cut.Find(".search-card").GetAttribute("aria-label"), StringComparison.Ordinal);
    }

    /// <summary>One sample hit per card kind, for the tests that do not care about the text.</summary>
    /// <param name="kind">The card kind: team, agent, library, task or room.</param>
    /// <returns>A hit whose subtype selects the matching card.</returns>
    private static SearchHit SampleHit(string kind)
    {
        DateTimeOffset time = new(2026, 10, 12, 14, 2, 0, TimeSpan.Zero);
        return kind switch
        {
            "team" => new TeamHit("Marketing", "Development Team", 4, MatchTier.ExactName),
            "agent" => new AgentHit("Nova", "Product Manager", "nova", ["Marketing"], MatchTier.ExactName),
            "library" => new LibraryHit("root", "Marketing", "plan.md", string.Empty, false, null, null, MatchTier.ExactName),
            "task" => new TaskHit(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName),
            _ => new RoomHit("room-1", "findings", 1, "msg-1", "Kai", "login", time, false, MatchTier.ContentMatch),
        };
    }

    /// <summary>A Team card draws the Team's own avatar, from the hit, as a rounded square in the icon slot: its label here, the monogram when there is none.</summary>
    [Fact]
    public async Task TeamCard_ShowsTheTeamsAvatarAsARoundedSquare()
    {
        await using MudBunitContext ctx = new();
        TeamHit labelled = new("Marketing", null, 1, MatchTier.ExactName) { Avatar = new Avatar("MK", null, null) };

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, labelled));
        IRenderedComponent<TeamSearchCard> bare = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, new TeamHit("Platform", null, 1, MatchTier.ExactName)));

        Assert.Equal("MK", cut.Find(".mud-avatar.avatar-square").TextContent.Trim());
        Assert.Equal("PL", bare.Find(".mud-avatar.avatar-square").TextContent.Trim());
    }
}
