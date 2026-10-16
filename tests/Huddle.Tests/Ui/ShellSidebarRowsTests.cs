using System.Text.RegularExpressions;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.NeedsYou;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teammates;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Tests.NeedsYou;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// U9 shell rows: the Teammates section header and footer row height (UX-014, UX-017, OP-4), the Team chevron
/// colour (UX-018), the shared <c>RoomActions</c> helper (UX-R29), the global <c>.visually-hidden</c> class
/// (UX-R21) and the Needs You Dismiss tooltip (UX-V09).
/// </summary>
public sealed class ShellSidebarRowsTests
{
    /// <summary>The Teammates entry is drawn as a section header through the <c>nav-section-link</c> class (UX-014).</summary>
    [Fact]
    public void MainLayout_TeammatesLink_TakesTheSectionHeaderClass()
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));

        Assert.Matches(@"<NavTabLink Href=""/teammates""[^>]*Class=""nav-section-link""", markup);
    }

    /// <summary>The <c>nav-section-link</c> rule gives the caption (12px), weight-600, secondary-colour header look of the group headers (UX-014).</summary>
    [Fact]
    public void AppCss_SectionLinkRule_MatchesTheGroupHeaders()
    {
        string rule = ShellSidebarRowsTests.RuleBody(".nav-section-link .mud-nav-link-text");

        ShellSidebarRowsTests.SourceHas(rule, "font-size: var(--mud-typography-caption-size)");
        ShellSidebarRowsTests.SourceHas(rule, "font-weight: 600");
        ShellSidebarRowsTests.SourceHas(rule, "var(--mud-palette-text-secondary)");
    }

    /// <summary>The footer links share the sidebar row height token and the body2 (13px) row size (UX-017, OP-4).</summary>
    [Fact]
    public void AppCss_FooterRows_UseTheRowHeightToken()
    {
        string css = ShellSidebarRowsTests.AppCss();
        string rule = ShellSidebarRowsTests.RuleBody(".nav-settings .mud-nav-link");

        Assert.Matches(@"--nav-row-height\s*:\s*32px\s*;", css);
        ShellSidebarRowsTests.SourceHas(rule, "min-height: var(--nav-row-height)");
        ShellSidebarRowsTests.SourceHas(rule, "font-size: var(--mud-typography-body2-size)");
    }

    /// <summary>The footer stays icon-only (OP-4), so the Settings link and the Archived chats entry each carry an <c>aria-label</c> and a tooltip (UX-017).</summary>
    [Fact]
    public void Footer_IconOnlyLinks_HaveAriaLabelAndTooltip()
    {
        string layout = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "MainLayout.razor"));
        string archived = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "ArchivedChats.razor"));

        ShellSidebarRowsTests.SourceHas(layout, "<MudTooltip Text=\"Settings\">");
        ShellSidebarRowsTests.SourceHas(layout, "aria-label=\"Settings\"");
        ShellSidebarRowsTests.SourceHas(archived, "<MudTooltip Text=\"Archived chats\">");
        ShellSidebarRowsTests.SourceHas(archived, "aria-label=\"Archived chats\"");
    }

    /// <summary>The Team chevron takes the default icon-button colour, not the secondary text colour (UX-018).</summary>
    [Fact]
    public void AppCss_TeamChevron_UsesTheActionDefaultColour()
    {
        string rule = ShellSidebarRowsTests.RuleBody(".teams-nav-host .teams-nav-chevron");

        ShellSidebarRowsTests.SourceHas(rule, "color: var(--mud-palette-action-default)");
        Assert.DoesNotContain("text-secondary", rule, StringComparison.Ordinal);
    }

    /// <summary>Archiving a Room through the helper returns <see langword="null"/> and archives it (UX-R29).</summary>
    [Fact]
    public async Task RoomActions_SetArchived_KnownRoom_ReturnsNullAndArchives()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ChatService chat, SqliteTeamDirectory directory) = await ShellSidebarRowsTests.CreateChatAsync(dir, ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        Room room = await chat.EnsureRoomForAsync(agent, ct);

        string? error = await RoomActions.SetArchivedAsync(chat, room.Id, archived: true, ct);

        Assert.Null(error);
        Room? stored = await directory.GetRoomAsync(room.Id, ct);
        Assert.True(stored?.Archived);
    }

    /// <summary>An archive refusal comes back as the exception's message, not as a thrown exception (UX-R29).</summary>
    [Fact]
    public async Task RoomActions_SetArchived_UnknownRoom_ReturnsTheMessage()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ChatService chat, _) = await ShellSidebarRowsTests.CreateChatAsync(dir, ct);

        string? error = await RoomActions.SetArchivedAsync(chat, "no-such-room", archived: true, ct);

        Assert.Equal("Unknown room 'no-such-room'.", error);
    }

    /// <summary>Deleting a Room through the helper returns <see langword="null"/> and removes it; an unknown Room returns the message (UX-R29).</summary>
    [Fact]
    public async Task RoomActions_Delete_RemovesKnownRoom_AndReportsUnknownOne()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TempDataDir dir = new();
        (ChatService chat, SqliteTeamDirectory directory) = await ShellSidebarRowsTests.CreateChatAsync(dir, ct);
        User? agent = await directory.UpsertAgentUserAsync("coo", null, ct);
        Assert.NotNull(agent);
        Room room = await chat.EnsureRoomForAsync(agent, ct);

        string? ok = await RoomActions.DeleteAsync(chat, room.Id, ct);
        string? missing = await RoomActions.DeleteAsync(chat, room.Id, ct);

        Assert.Null(ok);
        Assert.Equal($"Unknown room '{room.Id}'.", missing);
        Assert.Null(await directory.GetRoomAsync(room.Id, ct));
    }

    /// <summary>Neither <c>RoomNavRow</c> nor <c>ArchivedChatsDialog</c> catches <c>ChatException</c> itself any more: both call <c>RoomActions</c> (UX-R29).</summary>
    [Theory]
    [InlineData("RoomNavRow.razor")]
    [InlineData("ArchivedChatsDialog.razor")]
    public void RoomRows_UseRoomActions_NotTheirOwnCatch(string file)
    {
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", file));

        ShellSidebarRowsTests.SourceHas(markup, "RoomActions.");
        Assert.DoesNotContain("catch (ChatException", markup, StringComparison.Ordinal);
    }

    /// <summary>The scoped stylesheet of the tab strip no longer carries its own <c>.visually-hidden</c>; the global one in <c>app.css</c> does (UX-R21).</summary>
    [Fact]
    public void TabStrip_UsesTheGlobalVisuallyHiddenClass()
    {
        string scoped = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "TabStrip.razor.css"));
        string markup = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Layout", "TabStrip.razor"));

        Assert.DoesNotContain(".visually-hidden", scoped, StringComparison.Ordinal);
        ShellSidebarRowsTests.SourceHas(markup, "visually-hidden");
        ShellSidebarRowsTests.SourceHas(ShellSidebarRowsTests.AppCss(), ".visually-hidden {");
    }

    /// <summary>A Needs You row's Dismiss button sits in a tooltip, like its siblings' icon buttons (UX-V09).</summary>
    [Fact]
    public async Task NeedsYouList_DismissButton_IsInsideATooltip()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using NeedsYouFixture fixture = await NeedsYouFixture.CreateAsync(ct);
        User nova = await fixture.AddAgentAsync("Nova", ct);
        Room room = await fixture.CreateRoomAsync("Backend", [nova], ct);
        _ = await fixture.Service.PostAsync(room.Id, nova.Id, "which database, @You?", ct: ct);
        await using MudBunitContext ctx = new();
        ctx.Services.AddSingleton(fixture.Tracker);
        ctx.Services.AddSingleton<ITeamDirectory>(fixture.Team);
        ctx.Services.AddSingleton(fixture.Events);
        ctx.Services.AddSingleton<TimeProvider>(fixture.Clock);
        ctx.Services.AddSingleton<TabService>();

        IRenderedComponent<NeedsYouList> cut = ctx.Render<NeedsYouList>();

        Assert.Single(cut.FindAll(".mud-tooltip-root button[aria-label='Dismiss Backend']"));
    }

    /// <summary>Asserts that stylesheet or markup source text holds <paramref name="fragment"/>.</summary>
    /// <param name="source">The source file text, not rendered markup.</param>
    /// <param name="fragment">The text that must appear in it.</param>
    internal static void SourceHas(string source, string fragment)
    {
        Assert.Contains(fragment, source, StringComparison.Ordinal); // contains-ok: the haystack is a source file (CSS or Razor text), which has no rendered form to assert on.
    }

    private static string AppCss() =>
        File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    /// <summary>The declarations of the first rule whose selector is exactly <paramref name="selector"/>.</summary>
    private static string RuleBody(string selector)
    {
        Match match = Regex.Match(ShellSidebarRowsTests.AppCss(), @"(?m)^" + Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        Assert.True(match.Success, $"app.css has no rule '{selector}'.");
        return match.Groups[1].Value;
    }

    private static async Task<(ChatService Chat, SqliteTeamDirectory Directory)> CreateChatAsync(TempDataDir dir, CancellationToken ct)
    {
        RoomEvents events = new(NullLogger<RoomEvents>.Instance);
        SqliteTeamDirectory directory = new(dir.Options());
        await directory.InitializeAsync("You", ct);
        FileChatStore store = new(dir.Options(), NullLogger<FileChatStore>.Instance);
        ProposalStore proposals = new(events);
        ChatService chat = new(directory, store, events, new FakeMentionAliasSource(), Options.Create(new TeamOptions()), proposals, new QuestionStore(events), NullLogger<ChatService>.Instance);
        return (chat, directory);
    }
}
