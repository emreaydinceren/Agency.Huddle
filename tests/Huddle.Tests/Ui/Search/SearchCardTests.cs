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

    /// <summary>Builds a real, empty <see cref="AvatarStore"/>, since <see cref="TeammateSearchCard"/> injects it for the avatar.</summary>
    public SearchCardTests()
    {
        this.avatarStore = new AvatarStore(this.dataDir.Options(), NullLogger<AvatarStore>.Instance);
    }

    /// <summary>Releases the temporary data directory backing the avatar store.</summary>
    public void Dispose() => this.dataDir.Dispose();

    /// <summary>A Team with a type and four Teammates shows its name, then <c>Development Team · 4 teammates</c>.</summary>
    [Fact]
    public async Task TeamCard_RendersNameTypeAndCount()
    {
        await using MudBunitContext ctx = new();
        TeamHit hit = new("Marketing", "Development Team", 4, MatchTier.ExactName);

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Marketing", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("Development Team · 4 teammates", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Team with one Teammate and no type says <c>1 teammate</c>, not <c>1 teammates</c>.</summary>
    [Fact]
    public async Task TeamCard_OneTeammate_IsSingular()
    {
        await using MudBunitContext ctx = new();
        TeamHit hit = new("Platform", null, 1, MatchTier.NamePrefix);

        IRenderedComponent<TeamSearchCard> cut = ctx.Render<TeamSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("1 teammate", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Teammate shows its Name, then its Title and Teams, joined as <c>Product Manager · Marketing, Platform</c>.</summary>
    [Fact]
    public async Task TeammateCard_RendersNameTitleAndTeams()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);
        TeammateHit hit = new("Nova", "Product Manager", "nova", ["Marketing", "Platform"], MatchTier.ExactName);

        IRenderedComponent<TeammateSearchCard> cut = ctx.Render<TeammateSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Nova", cut.Find(".search-card-title").TextContent.Trim());
        Assert.Equal("Product Manager · Marketing, Platform", cut.Find(".search-card-meta").TextContent.Trim());
    }

    /// <summary>A Teammate with no Team says <c>No team</c> in place of the Team list (OP-8).</summary>
    [Fact]
    public async Task TeammateCard_NoTeam_SaysNoTeam()
    {
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(this.avatarStore);
        TeammateHit hit = new("Nova", "Product Manager", "nova", [], MatchTier.ExactName);

        IRenderedComponent<TeammateSearchCard> cut = ctx.Render<TeammateSearchCard>(p => p.Add(c => c.Hit, hit));

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

    /// <summary>A Task's first line is <c>SUP-0012 · Support SAML login</c> with the status chip beside it; the second is <c>Nova · Staff Engineer</c> at the left and <c>Platform Team</c> at the right.</summary>
    [Fact]
    public async Task TaskCard_RendersBothLines()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.InProgress, "Nova", "Platform", false, MatchTier.ExactName, "Staff Engineer");

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("SUP-0012 · Support SAML login", cut.Find(".search-card-head .search-card-title").TextContent.Trim());
        Assert.Equal("In Progress", cut.Find(".search-card-head .search-card-chip").TextContent.Trim());
        Assert.Equal("Nova · Staff Engineer", cut.Find(".search-card-foot .search-card-meta").TextContent.Trim());
        Assert.Equal("Platform Team", cut.Find(".search-card-foot .search-card-team").TextContent.Trim());
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

        Assert.Equal(expected, cut.Find(".search-card-team").TextContent.Trim());
    }

    /// <summary>An unassigned Task's second line is the Team at the right alone: no assignee part and no dot.</summary>
    [Fact]
    public async Task TaskCard_NoAssignee_ShowsTeamOnly()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Platform Team", cut.Find(".search-card-foot").TextContent.Trim());
        Assert.Empty(cut.FindAll(".search-card-meta"));
    }

    /// <summary>An assignee with no Persona title shows the name only.</summary>
    [Fact]
    public async Task TaskCard_AssigneeWithoutTitle_ShowsNameOnly()
    {
        await using MudBunitContext ctx = new();
        TaskHit hit = new(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, "Nova", "Platform", false, MatchTier.ExactName);

        IRenderedComponent<TaskSearchCard> cut = ctx.Render<TaskSearchCard>(p => p.Add(c => c.Hit, hit));

        Assert.Equal("Nova", cut.Find(".search-card-meta").TextContent.Trim());
        Assert.Equal("Platform Team", cut.Find(".search-card-team").TextContent.Trim());
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
    [InlineData("teammate")]
    [InlineData("library")]
    [InlineData("task")]
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
    /// <param name="kind">The card kind: team, teammate, library, task or room.</param>
    /// <returns>A hit whose subtype selects the matching card.</returns>
    private static SearchHit SampleHit(string kind)
    {
        DateTimeOffset time = new(2026, 10, 12, 14, 2, 0, TimeSpan.Zero);
        return kind switch
        {
            "team" => new TeamHit("Marketing", "Development Team", 4, MatchTier.ExactName),
            "teammate" => new TeammateHit("Nova", "Product Manager", "nova", ["Marketing"], MatchTier.ExactName),
            "library" => new LibraryHit("root", "Marketing", "plan.md", string.Empty, false, null, null, MatchTier.ExactName),
            "task" => new TaskHit(new TaskId("SUP", 12), "Support SAML login", TaskState.ToDo, null, "Platform", false, MatchTier.ExactName),
            _ => new RoomHit("room-1", "findings", 1, "msg-1", "Kai", "login", time, false, MatchTier.ContentMatch),
        };
    }
}
