using System.Text.Encodings.Web;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Themes;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Tests for the Settings page's section menu and its Prompts panel. Split two ways, per
/// <c>docs/engineering/testing.md</c>: routing, HTTP status, the prompts/appearance file paths and
/// <c>FakeModelCatalog.ProbeCount</c> are facts about the server, so they stay on a plain HTTP GET
/// against <see cref="TeamWebApplicationFactory"/>; everything that asserts on the section menu, the Save
/// button or the Reset-all button now renders <see cref="Settings"/> directly through
/// <see cref="MudBunitContext"/>, because the MudBlazor migration turned all three into components
/// whose actual disabled-state markup a raw-HTML tag slice can no longer be trusted to parse.
/// </summary>
public sealed class SettingsPageTests
{
    /// <summary>Renders the real <see cref="Settings"/> page against a <see cref="TeamWebApplicationFactory"/>'s live <see cref="PromptStore"/> and <see cref="AppearanceStore"/>, the same pattern <c>AgentsPageTests</c> uses for <c>Agents</c>.</summary>
    private static MudBunitContext NewContext(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Settings));
        ProfileTestServices.AddProfile(ctx.Services, factory);
        SkillsTestServices.AddFromFactory(ctx.Services, factory);
        return ctx;
    }
    /// <summary>The section menu renders inside the page, in its own <c>nav</c>, and the Prompts section shows every group heading.</summary>
    [Fact]
    public async Task SettingsPage_Renders_SectionMenuAndGroupHeadings()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        Assert.Single(cut.FindAll(".settings-page nav.settings-nav .mud-navmenu"));
        Assert.Empty(cut.FindAll(".mud-tabs"));
        Assert.Contains(">Prompts<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Turn<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Get help<", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(">Tool descriptions<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every section is a real link to its own <c>/settings/{section}</c> address, so a section is bookmarkable
    /// and opening it is ordinary navigation rather than local state. This replaces a click test: bUnit does not
    /// follow an anchor's <c>href</c>, so the address the link carries is what can be pinned. The Sessions row sits directly after Library (Spec §5.4, T2).
    /// </summary>
    [Fact]
    public async Task SettingsPage_SectionMenu_LinksEachSectionToItsRoute()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        List<string?> hrefs = [.. cut.FindAll(".settings-nav a.mud-nav-link").Select(static link => link.GetAttribute("href"))];
        Assert.Equal(
            ["/settings/profile", "/settings/prompts", "/settings/helpers", "/settings/spending", "/settings/needsyou", "/settings/appearance", "/settings/personas", "/settings/skills", "/settings/teamtypes", "/settings/library", "/settings/sessions", "/settings/remoteaccess"],
            hrefs);
    }

    /// <summary>
    /// <c>/settings/spending</c> selects the Spending section: it renders <c>SpendingPanel</c> and the menu marks
    /// the Spending link as the current page (issue 356 Part 2).
    /// </summary>
    [Fact]
    public async Task SettingsPage_SpendingSegment_ShowsSpendingPanelAndHighlightsIt()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        foreach (Type type in new[] { typeof(Agency.Huddle.App.Acp.PersonaStore), typeof(Agency.Huddle.App.Acp.PersonaSpend), typeof(Agency.Huddle.App.Data.PersonaSpendStore), typeof(Agency.Huddle.App.Data.HelperUsageStore), typeof(Agency.Huddle.App.Teams.ITeamCatalog) })
        {
            if (!ctx.Services.Any(descriptor => descriptor.ServiceType == type))
            {
                ctx.Services.AddSingleton(type, factory.Services.GetRequiredService(type));
            }
        }

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "spending"));

        Assert.Single(cut.FindAll(".spending-panel"));
        var current = Assert.Single(cut.FindAll(".settings-nav a.mud-nav-link[aria-current='page']"));
        Assert.Equal("/settings/spending", current.GetAttribute("href"));
    }

    /// <summary>
    /// <c>/settings</c> with no segment, and an unknown segment, highlight Profile - the section that renders - and
    /// mark it as the current page, which NavLink's own URL match alone would not do for either address.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("nonsense")]
    [InlineData("99")]
    public async Task SettingsPage_NoOrUnknownSegment_HighlightsProfile(string? tab)
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, tab));

        var active = Assert.Single(cut.FindAll(".settings-nav .settings-nav-item-active a.mud-nav-link"));
        Assert.Equal("Profile", active.TextContent.Trim());
        Assert.Equal("page", active.GetAttribute("aria-current"));
        Assert.Single(cut.FindComponents<ProfilePanel>());
    }

    /// <summary>
    /// The open section is titled with its menu label, as an h2 that names the section for assistive technology; an unknown
    /// segment falls back to Profile's title (side-by-side audit, LL-S01). The Sessions section is titled "Sessions" (Spec §5.4, T2).
    /// </summary>
    /// <param name="tab">The route segment.</param>
    /// <param name="title">The title the section should carry.</param>
    [Theory]
    [InlineData("prompts", "Prompts")]
    [InlineData("library", "Library")]
    [InlineData("sessions", "Sessions")]
    [InlineData("nonsense", "Profile")]
    public async Task SettingsPage_OpenSection_IsTitledWithItsMenuLabel(string tab, string title)
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, tab));

        var heading = cut.Find("section.settings-section > h2.settings-section-title");
        Assert.Equal(title, heading.TextContent.Trim());
        Assert.Equal(heading.Id, cut.Find("section.settings-section").GetAttribute("aria-labelledby"));
    }

    /// <summary>The <c>/settings/remoteaccess</c> route highlights the tenth row, "Remote access", after Sessions, and renders the Remote access panel (Spec 6.12, E1; the Sessions row before it is Spec §5.4, T2).</summary>
    [Fact]
    public async Task SettingsPage_RemoteAccessRoute_RendersRemoteAccessPanelAfterLibrary()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "remoteaccess"));

        Assert.Single(cut.FindComponents<Agency.Huddle.App.Components.Settings.RemoteAccess.RemoteAccessPanel>());
        List<AngleSharp.Dom.IElement> items = cut.FindAll(".settings-nav .mud-nav-item").ToList();
        Assert.Equal(11, items.FindIndex(static item => item.ClassList.Contains("settings-nav-item-active")));
        Assert.Equal("Remote access", cut.Find(".settings-nav .settings-nav-item-active a.mud-nav-link").TextContent.Trim());
    }

    /// <summary>A paired device without the admin scope sees the "Settings need admin access." notice on the Remote access route, never the panel (C-13, regression).</summary>
    [Fact]
    public async Task SettingsPage_RemoteAccessRoute_LimitedSession_SeesTheNoticeNotThePanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);
        ctx.SignInWithScopes("operator.read", "operator.write");

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "remoteaccess"));

        Assert.Equal("Settings need admin access.", cut.Find("[role=alert]").TextContent.Trim());
        Assert.Empty(cut.FindComponents<Agency.Huddle.App.Components.Settings.RemoteAccess.RemoteAccessPanel>());
    }

    /// <summary>The <c>/settings/library</c> route highlights the eighth menu row and renders the Library panel (D3: the tab was unreachable).</summary>
    [Fact]
    public async Task SettingsPage_LibraryRoute_RendersLibraryPanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "library"));

        Assert.Single(cut.FindComponents<LibraryPanel>());
        Assert.Equal(9, cut.FindAll(".settings-nav .mud-nav-item").ToList().FindIndex(static item => item.ClassList.Contains("settings-nav-item-active")));
    }

    /// <summary>The <c>/settings/sessions</c> route highlights the ninth row, "Sessions", directly after Library, and renders the Sessions panel (Spec §5.4, T2).</summary>
    [Fact]
    public async Task SettingsPage_SessionsRoute_RendersSessionsPanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "sessions"));

        Assert.Single(cut.FindComponents<Agency.Huddle.App.Components.Settings.SessionsPanel>());
        Assert.Equal(10, cut.FindAll(".settings-nav .mud-nav-item").ToList().FindIndex(static item => item.ClassList.Contains("settings-nav-item-active")));
        Assert.Equal("Sessions", cut.Find(".settings-nav .settings-nav-item-active a.mud-nav-link").TextContent.Trim());
    }

    /// <summary>The Prompts panel renders the Prompts panel's MudPaper-wrapped groups.</summary>
    [Fact]
    public async Task SettingsPromptsPage_Renders_PromptsPanel()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        Assert.NotEmpty(cut.FindAll(".mud-paper"));
        Assert.Contains(">System prompt<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>An unrecognised {Tab} value falls back to the first tab, Profile, rather than 404ing or throwing - a fact about routing, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPage_UnknownTab_FallsBackToProfilePanel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/settings/nonsense", ct);
        var html = await response.Content.ReadAsStringAsync(ct);

        response.EnsureSuccessStatusCode();
        // contains-ok: a raw prerendered page; the panel's class proves which tab rendered, its markup is pinned by ProfilePanelTests.
        Assert.Contains("profile-panel", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">System prompt<", html, StringComparison.Ordinal);
    }

    /// <summary>Every one of the catalog's prompt labels reaches the rendered page - unaffected by the MudBlazor conversion, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPromptsPage_ListsEveryPromptLabel()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/prompts", ct);

        foreach (var definition in PromptCatalog.All)
        {
            Assert.Contains(definition.Label, html, StringComparison.Ordinal);
        }
    }

    /// <summary>Loading the Settings page must never spawn an adapter process - a fact about the server, so it stays on HTTP.</summary>
    [Fact]
    public async Task SettingsPage_DoesNotProbeForModelsOnAPlainLoad()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/settings", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);
    }

    /// <summary>With nothing pending, the Save button renders disabled - there is nothing yet to commit.</summary>
    [Fact]
    public async Task SettingsPromptsPage_WithNothingPending_SaveButtonIsDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        var save = cut.Find("button[type='submit']");
        Assert.True(save.HasAttribute("disabled"));
    }

    /// <summary>
    /// The page tells a user exactly where the Prompts folder lives - matched against the factory's
    /// own configured path (see <see cref="TeamWebApplicationFactory.PromptsDirPath"/>) rather than a
    /// hardcoded guess, since <c>Team:DataDir</c> is redirected to a fresh temp directory per factory.
    /// This is a fact about the server's configuration, so it stays on HTTP.
    /// </summary>
    [Fact]
    public async Task SettingsPromptsPage_ShowsTheOverrideFilePath()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/prompts", ct);

        Assert.Contains(factory.PromptsDirPath, html, StringComparison.Ordinal);
    }

    /// <summary>With no override configured, the "Reset all to defaults" button renders disabled.</summary>
    [Fact]
    public async Task SettingsPromptsPage_WithNothingModified_ResetAllButtonIsDisabled()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>(parameters => parameters.Add(page => page.Tab, "prompts"));

        var buttons = cut.FindAll("button");
        var resetAll = Assert.Single(buttons, button => button.TextContent.Contains("Reset all to defaults", StringComparison.Ordinal));
        Assert.True(resetAll.HasAttribute("disabled"));
    }

    /// <summary>The sidebar link to Settings appears on every page, alongside the Agents link.</summary>
    [Fact]
    public async Task Sidebar_LinksToSettingsPage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("href=\"/settings\"", html, StringComparison.Ordinal);
    }

    /// <summary>The tab rail includes an Appearance tab alongside Prompts.</summary>
    [Fact]
    public async Task SettingsPage_Renders_AppearanceTabInTheRail()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains(">Appearance<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab shows a labelled light-theme picker and a labelled dark-theme picker - no light/dark preference control - carrying the catalog's
    /// default theme's label. The second control it used to show, a light/dark preference, is gone:
    /// a theme now carries its own mode, so there is nothing left for a second control to contradict
    /// (<c>docs/adr/0017-a-theme-is-a-palette-not-a-pair.md</c>). Stays on HTTP: it is a fact about
    /// the prerender.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_RendersALightAndADarkThemePickerAndNoDarkModeControl()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains("aria-label=\"Light theme\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Dark theme\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Appearance\"", html, StringComparison.Ordinal);
        Assert.Contains($">{HtmlEncoder.Default.Encode(ThemeCatalog.BuiltIn[0].Label)}<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every theme in the catalog is offered, under its group's heading. No click is simulated to get
    /// there: the picker is a <c>MudList</c>, so every option is in the markup from the first render -
    /// which is the practical reason to prefer it over the <c>MudSelect</c> it replaced, whose options
    /// only reached a popover after a real <c>MouseDown</c>. Labels are compared through
    /// <see cref="HtmlEncoder.Default"/> because that is the encoder the response was written with,
    /// and it escapes more than the five XML entities - a label such as "Cobalt Soft"
    /// must match its encoded form, whatever the encoder does to it.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_OffersEveryThemeUnderItsGroupHeading()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        foreach (var theme in ThemeCatalog.BuiltIn)
        {
            Assert.Contains(HtmlEncoder.Default.Encode(theme.Label), html, StringComparison.Ordinal);
        }

        // No group heading is drawn at all now: "Light" and "Dark" repeated the picker's own title (audit B9c) and the High contrast group is gone.
        Assert.DoesNotContain(">High contrast<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("theme-picker-heading group-label\">Light<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The paragraph move in T4.1 must not regress: the Appearance tab never shows the Prompts intro's
    /// file-path paragraph, which talked about <c>prompts.json</c> and would make no sense here.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_DoesNotRenderThePromptsIntro()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.DoesNotContain(factory.PromptsDirPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab tells a user exactly where <c>appearance.json</c> lives, the same guarantee
    /// <see cref="SettingsPromptsPage_ShowsTheOverrideFilePath"/> gives the Prompts tab.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_NamesTheOverrideFile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains(factory.AppearanceJsonPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Appearance tab no longer credits Visual Studio Code or any ported theme's author (the ports were
    /// removed on 2026-10-17); it says the shipped Themes are this application's own - an ordinary
    /// prerendered paragraph, so this stays on HTTP rather than bUnit.
    /// </summary>
    [Fact]
    public async Task SettingsAppearancePage_CreditsNoThirdPartyTheme()
    {
        // A fresh factory boots a whole host and JITs the first request; under CI load that alone outlasted ten seconds.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/appearance", ct);

        Assert.Contains("is taken from a third-party colour theme.", html, StringComparison.Ordinal); // contains-ok: rendered markup text
        Assert.DoesNotContain("Visual Studio Code", html, StringComparison.Ordinal);
    }

    /// <summary>The tab rail includes a Personas tab alongside Prompts and Appearance.</summary>
    [Fact]
    public async Task SettingsPage_Renders_PersonasTabInTheRail()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = ctx.Render<Settings>();

        Assert.Contains(">Personas<", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Personas tab tells a user exactly where its Persona files and its room database live -
    /// matched against the factory's own configured paths rather than a hardcoded guess, since
    /// <c>Team:DataDir</c> is redirected to a fresh temp directory per factory. A fact about the
    /// server's configuration, so it stays on HTTP.
    /// </summary>
    [Fact]
    public async Task SettingsPersonasPage_ShowsThePersonaFolderAndDatabasePaths()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/personas", ct);

        // contains-ok: the haystack is the whole rendered page's HTML; only the presence of the path matters here, not the surrounding markup.
        Assert.Contains(factory.AgentsDirPath, html, StringComparison.Ordinal);
        Assert.Contains(factory.DbPath, html, StringComparison.Ordinal);
    }

    /// <summary>The Personas tab never shows the Prompts intro's file-path paragraph, which talks about <c>prompts.json</c> and would make no sense here.</summary>
    [Fact]
    public async Task SettingsPersonasPage_DoesNotRenderThePromptsIntro()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/personas", ct);

        Assert.DoesNotContain(factory.PromptsDirPath, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §6.7 (Settings row): the Skills tab's read-only table lists every shipped default -
    /// <c>team-building</c> included - labelled Default when no override folder exists on disk for
    /// it. A fact about the prerendered markup, so this stays on HTTP like the sibling Personas/
    /// Appearance content tests.
    /// </summary>
    [Fact]
    public async Task SkillsTab_ListsTeamBuildingAsDefault()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/skills", ct);

        Assert.Contains(">Team building<", html, StringComparison.Ordinal); // contains-ok: rendered markup text
        Assert.Contains(">Built in<", html, StringComparison.Ordinal); // contains-ok: rendered markup text
    }

    /// <summary>
    /// Spec §6.7, U10: an Overridden Skill's row offers Restore default; clicking it opens a MudDialog
    /// confirm naming the exact folder <see cref="Agency.Huddle.App.Skills.Skill.FolderPath"/> resolved
    /// - never typed by the UI, per the Spec's "Constraints" paragraph - and confirming it (the
    /// dialog's own "Restore default" button, which repeats the verb of the "Restore default?" title,
    /// UX-080) deletes that folder and the row reverts to Default. Needs
    /// <see cref="MudBunitContext.RenderWithPopovers"/>, not a plain HTTP round trip: the dialog is
    /// real MudBlazor content shown through <c>IDialogService</c>, the same reasoning
    /// <c>AgentProfileCardTests</c> gives for <c>AgentProfileCard</c>'s own dialog.
    /// </summary>
    [Fact]
    public async Task SkillsTab_OverriddenSkill_ShowsRestoreDefault_ClickRemovesFolder()
    {
        await using var factory = new TeamWebApplicationFactory();
        var overrideFolder = Path.Combine(factory.SkillsDirPath, "team-building");
        Directory.CreateDirectory(overrideFolder);
        await File.WriteAllTextAsync(
            Path.Combine(overrideFolder, "roles.md"),
            "A hand-edited override.",
            Xunit.TestContext.Current.CancellationToken);

        await using var ctx = NewContext(factory);
        var cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Settings>(0);
            builder.AddAttribute(1, nameof(Settings.Tab), "skills");
            builder.CloseComponent();
        });

        // Restore default lives in the row's ⋯ menu (Skill Packs spec §6.9.3). Fire-and-forget: the handler awaits the
        // confirm dialog, which this test answers next.
        const string RowMenu = "tr:has([data-skill='team-building']) .skills-row-menu button";
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(RowMenu)));
        await cut.InvokeAsync(() => cut.Find(RowMenu).ClickAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".skills-menu-restore-default")));
        Task restore = cut.InvokeAsync(() => cut.Find(".skills-menu-restore-default").ClickAsync());

        cut.WaitForAssertion(() => Assert.Contains(overrideFolder, cut.Markup, StringComparison.Ordinal));

        await cut.InvokeAsync(() => cut.Find(".mud-message-box__yes-button").ClickAsync());
        await restore;

        Assert.False(Directory.Exists(overrideFolder));
    }

    /// <summary>
    /// Spec §6.7 (Settings row, "Issues" column): a hand-written Yours Skill whose SKILL.md fails
    /// validation has no shipped default to fall back to, so the store omits it from
    /// <see cref="Agency.Huddle.App.Skills.SkillStore.All"/> entirely and keeps its Error in
    /// <see cref="Agency.Huddle.App.Skills.SkillStore.Issues"/> instead - so the row's error text must
    /// be the validator's own message, not a re-derived one, or a Human editing the file has no way to
    /// see why it never appeared.
    /// </summary>
    [Fact]
    public async Task SkillsTab_InvalidYoursSkill_ShowsError()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        Directory.CreateDirectory(Path.Combine(factory.SkillsDirPath, "broken-skill"));
        await File.WriteAllTextAsync(
            Path.Combine(factory.SkillsDirPath, "broken-skill", "SKILL.md"),
            "---\nname: broken-skill\n---\nBody.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/settings/skills", ct);

        Assert.Contains(
            HtmlEncoder.Default.Encode("SKILL.md is missing required field 'description'."),
            html,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec §6.7, U8/U10: navigating to <c>/settings/skills</c> selects the Skills tab rather than
    /// falling back to Prompts - the same routing guarantee <c>/settings/personas</c> and
    /// <c>/settings/appearance</c> already have (<see cref="SettingsPage_UnknownTab_FallsBackToProfilePanel"/>'s
    /// inverse). Asserted by the ABSENCE of the Prompts panel's own content rather than by referencing
    /// <c>SettingsTab.Skills</c> directly, so this stays an assertion red rather than a compile one.
    /// </summary>
    [Fact]
    public async Task SettingsRoute_TabSkills_SelectsSkillsTab()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/settings/skills", ct);

        Assert.DoesNotContain(">System prompt<", html, StringComparison.Ordinal);
    }
}
