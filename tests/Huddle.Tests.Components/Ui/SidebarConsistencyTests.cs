using System.Text.RegularExpressions;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// U9 (UX-015, UX-016, OP-5): the sidebar's Room rows tell a 1:1 Room from a group Room by their leading
/// visual (a small <c>TeammateAvatar</c> versus the <c>Filled.Chat</c> icon), and "Left menu" in
/// <c>app.css</c> sets each nesting level's indent once, as a named rem token, instead of repeating literals.
/// </summary>
public sealed class SidebarConsistencyTests
{
    /// <summary>The row element each Room link sits in; the "Chats" group header is outside it.</summary>
    private const string RowSelector = "div.hover-reveal-row";

    /// <summary>A 1:1 Room (one Teammate) shows a small Teammate avatar in its row and not the chat icon (UX-016, OP-5).</summary>
    [Fact]
    public async Task RoomRow_OneToOneRoom_ShowsSmallTeammateAvatarNotChatIcon()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        ChatService chat = SidebarConsistencyTests.CreateChatService(dir, directory, events);
        _ = await chat.EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = SidebarConsistencyTests.NewContext(directory, chat, events, dir);
        var cut = SidebarConsistencyTests.RenderRoomList(ctx);

        var row = cut.Find(SidebarConsistencyTests.RowSelector);
        Assert.Single(row.QuerySelectorAll(".mud-avatar.mud-avatar-small"));
        Assert.Empty(row.QuerySelectorAll("svg.mud-nav-link-icon"));
    }

    /// <summary>A group Room (two or more Teammates) shows the chat icon in its row and no avatar (UX-016, OP-5).</summary>
    [Fact]
    public async Task RoomRow_GroupRoom_ShowsChatIconNotAvatar()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        User? first = await directory.UpsertAgentUserAsync("coo", null, ct);
        User? second = await directory.UpsertAgentUserAsync("cfo", null, ct);
        Assert.NotNull(first);
        Assert.NotNull(second);
        ChatService chat = SidebarConsistencyTests.CreateChatService(dir, directory, events);
        _ = await chat.CreateRoomForAsync([first.Id, second.Id], ct);

        await using MudBunitContext ctx = SidebarConsistencyTests.NewContext(directory, chat, events, dir);
        var cut = SidebarConsistencyTests.RenderRoomList(ctx);

        var row = cut.Find(SidebarConsistencyTests.RowSelector);
        Assert.Single(row.QuerySelectorAll("svg.mud-nav-link-icon"));
        Assert.Empty(row.QuerySelectorAll(".mud-avatar"));
    }

    /// <summary>"Left menu" declares one rem token per nesting level, each exactly once (UX-015).</summary>
    [Theory]
    [InlineData("--nav-indent-1")]
    [InlineData("--nav-indent-2")]
    public void LeftMenu_DeclaresOneIndentTokenPerLevel_InRem(string token)
    {
        string section = SidebarConsistencyTests.LeftMenuSection();

        MatchCollection declarations = Regex.Matches(section, Regex.Escape(token) + @"\s*:\s*([^;]+);");

        Match declaration = Assert.Single(declarations);
        Assert.Matches(@"rem$", declaration.Groups[1].Value.Trim());
    }

    /// <summary>"Left menu" has no literal pixel margin or padding-left for the indent: the old <c>26px</c> is gone (UX-015).</summary>
    [Fact]
    public void LeftMenu_IndentRules_UseTheTokensNotPixelLiterals()
    {
        string section = SidebarConsistencyTests.LeftMenuSection();

        Assert.DoesNotContain("26px", section, StringComparison.Ordinal);
        ShellSidebarRowsTests.SourceHas(section, "var(--nav-indent-1)");
    }

    /// <summary>The Teams nav's Project and Team Room rows take the level-2 token, not the stale <c>3.875rem</c> literal (UX-015).</summary>
    [Fact]
    public void TeamsNav_ProjectAndTeamRoomRows_UseTheLevelTwoToken()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);

        Assert.DoesNotContain("3.875rem", css, StringComparison.Ordinal);
        ShellSidebarRowsTests.SourceHas(css, "padding-left: var(--nav-indent-2)");
    }

    /// <summary>The "Left menu" block of <c>app.css</c>: from its banner comment to the next banner comment.</summary>
    private static string LeftMenuSection()
    {
        string css = CssText.AppCss(CssNewlines.AsIs);
        int start = css.IndexOf("/* ---- Left menu", StringComparison.Ordinal);
        Assert.True(start >= 0, "app.css has no 'Left menu' section.");
        int end = css.IndexOf("/* ---- ", start + 10, StringComparison.Ordinal);
        return end < 0 ? css[start..] : css[start..end];
    }

    private static ChatService CreateChatService(TempDataDir dir, ITeamDirectory directory, RoomEvents events)
    {
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        ProposalStore proposals = new(events);
        return new ChatService(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
    }

    private static MudBunitContext NewContext(ITeamDirectory directory, ChatService chat, RoomEvents events, TempDataDir dir)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(directory);
        ctx.Services.AddSingleton(chat);
        ctx.Services.AddSingleton<Agency.Huddle.App.Teams.ITeamCatalog>(new Agency.Huddle.Tests.Teams.FakeTeamCatalog());
        ctx.Services.AddSingleton(events);
        ctx.Services.AddSingleton<IAgentGateway>(new FakeAgentGateway());
        ctx.Services.AddSingleton(new PersonaHealth(TimeProvider.System, NullLogger<PersonaHealth>.Instance));
        ctx.Services.AddSingleton(new AvatarStore(dir.Options(), NullLogger<AvatarStore>.Instance));
        ctx.Services.AddSingleton<Agency.Huddle.App.Tabs.TabService>();
        return ctx;
    }

    private static IRenderedComponent<ContainerFragment> RenderRoomList(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<RoomList>(0);
            builder.CloseComponent();
        });
}
