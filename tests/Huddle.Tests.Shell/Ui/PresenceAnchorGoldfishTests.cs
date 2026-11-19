using System.Reflection;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Presence;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.Css;
using Agency.Huddle.Tests.Factories;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TestContext = Xunit.TestContext;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the goldfish avatar cue (Goldfish slice 4, step 2): <see cref="PresenceAnchor"/> draws a ring and a fish badge for an
/// Agent that <see cref="GoldfishLive"/> reports in goldfish mode, the cue and the fish symbol behind it, and the stylesheet rules.
/// </summary>
public sealed class PresenceAnchorGoldfishTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 8, 0, 0, TimeSpan.Zero);

    /// <summary>The goldfish cue is the fish glyph in the <c>goldfish</c> tone, labelled "{Name}, in goldfish mode".</summary>
    [Fact]
    public void Cue_Goldfish_IsTheFishGlyphInItsOwnTone()
    {
        PresenceCue cue = PresenceCue.Goldfish("Ruth");

        Assert.Equal(new PresenceCue(PresenceGlyph.Fish, "goldfish", "Ruth, in goldfish mode"), cue);
    }

    /// <summary>The fish symbol is one SVG path in the same shape as its neighbours, so a chip or row can draw it.</summary>
    [Fact]
    public void MaterialSymbols_Fish_IsAPathElement()
    {
        Assert.StartsWith("<path d=\"", MaterialSymbols.Fish, StringComparison.Ordinal); // contains-ok: prefix/suffix of a constant's own SVG element
        Assert.EndsWith("/>", MaterialSymbols.Fish, StringComparison.Ordinal); // contains-ok: prefix/suffix of a constant's own SVG element
    }

    /// <summary>An Agent with no possession is drawn exactly as before: its Health badge, and no goldfish class on the anchor.</summary>
    [Fact]
    public async Task Anchor_NoPossession_KeepsTheHealthCue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);

        Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName);
        Assert.Equal("Online", Assert.Single(cut.FindAll("span.agent-dot")).GetAttribute("aria-label"));
    }

    /// <summary>A live possession puts the goldfish class on the anchor and replaces the Health badge with the fish badge.</summary>
    [Fact]
    public async Task Anchor_Possessed_DrawsTheRingClassAndTheFishBadge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);

        Assert.Equal("presence-anchor goldfish", cut.Find("span.presence-anchor").ClassName);
        IElement badge = Assert.Single(cut.FindAll("span.agent-dot"));
        Assert.Equal("agent-dot presence-badge presence-glyph-fish goldfish on-avatar", badge.ClassName);
    }

    /// <summary>The fish badge's accessible name is "{Name}, in goldfish mode".</summary>
    [Fact]
    public async Task Anchor_Possessed_BadgeHasTheGoldfishAriaLabel()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);

        Assert.Equal("Ruth, in goldfish mode", cut.Find("span.agent-dot").GetAttribute("aria-label"));
    }

    /// <summary>A Waiting possession is still live, so the Agent still wears the cue.</summary>
    [Fact]
    public async Task Anchor_Waiting_StillDrawsTheCue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Waiting, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);

        Assert.Equal(("presence-anchor goldfish", "Ruth, in goldfish mode"), (cut.Find("span.presence-anchor").ClassName, cut.Find("span.agent-dot").GetAttribute("aria-label")));
    }

    /// <summary>BadgeOnly (the ProfileCard) draws the fish badge but not the ring class.</summary>
    [Fact]
    public async Task Anchor_BadgeOnly_DrawsNoRing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: true);

        Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName);
        Assert.Equal("Ruth, in goldfish mode", Assert.Single(cut.FindAll("span.agent-dot.presence-glyph-fish")).GetAttribute("aria-label"));
    }

    /// <summary>Another Agent's possession changes nothing for this one.</summary>
    [Fact]
    public async Task Anchor_OtherAgentPossessed_IsUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Kai, PossessionState.Working, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);

        Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName);
        Assert.Equal("Online", Assert.Single(cut.FindAll("span.agent-dot")).GetAttribute("aria-label"));
    }

    /// <summary>With no AgentName the anchor ignores goldfish mode entirely, as every caller did before.</summary>
    [Fact]
    public async Task Anchor_NoAgentName_IgnoresGoldfishMode()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        await using MudBunitContext ctx = rig.NewContext();

        IRenderedComponent<PresenceAnchor> cut = ctx.Render<PresenceAnchor>(parameters => parameters
            .Add(p => p.Cue, PresenceCue.From(App.Acp.PersonaState.Online))
            .AddChildContent<AgentAvatar>(avatar => avatar.Add(p => p.Name, "Ruth")));

        Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName);
        Assert.Equal("Online", Assert.Single(cut.FindAll("span.agent-dot")).GetAttribute("aria-label"));
    }

    /// <summary>A possession that starts after the first render re-renders the anchor with the cue, and ending it takes the cue away.</summary>
    [Fact]
    public async Task Anchor_StoreChanges_ReRendersWithAndWithoutTheCue()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        IRenderedComponent<PresenceAnchor> cut = RenderAnchor(ctx, "Ruth", badgeOnly: false);
        Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName);

        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        cut.WaitForAssertion(() => Assert.Equal("presence-anchor goldfish", cut.Find("span.presence-anchor").ClassName));

        _ = rig.Store.BeginReturn("p1", GoldfishExit.Finished, null);
        cut.WaitForAssertion(() => Assert.Equal("presence-anchor", cut.Find("span.presence-anchor").ClassName));
    }

    /// <summary>Disposing the anchor unsubscribes it from GoldfishLive, so a retired component is not re-rendered.</summary>
    [Fact]
    public async Task Anchor_Dispose_UnsubscribesFromGoldfishLive()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext ctx = rig.NewContext();
        _ = RenderAnchor(ctx, "Ruth", badgeOnly: false);
        int whileRendered = rig.ChangedSubscribers();

        await ctx.DisposeComponentsOnDispatcherAsync();

        Assert.Equal((1, 0), (whileRendered, rig.ChangedSubscribers()));
    }

    /// <summary>The avatar's own markup is the same with and without the cue: the ring is on the anchor, never inside the avatar.</summary>
    [Fact]
    public async Task Anchor_Possessed_LeavesTheAvatarMarkupUnchanged()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using Rig rig = await Rig.CreateAsync(ct);
        await using MudBunitContext plain = rig.NewContext();
        string before = RenderAnchor(plain, "Ruth", badgeOnly: false).Find(".mud-avatar").OuterHtml;
        await rig.PossessAsync("p1", rig.Ruth, PossessionState.Working, ct);
        await using MudBunitContext live = rig.NewContext();

        string after = RenderAnchor(live, "Ruth", badgeOnly: false).Find(".mud-avatar").OuterHtml;

        Assert.Equal(before, after);
    }

    /// <summary>shell.css draws the ring on the anchor (2px Info outline, 2px clear) and the fish badge in Info mixed 20% toward black.</summary>
    [Fact]
    public void Stylesheet_HasTheRingAndTheFishBadgeRules()
    {
        string css = CssText.Read(RepoPaths.App("wwwroot", "css", "shell.css"), CssNewlines.AsIs);

        string ring = CssText.RuleBodyLoose(css, ".presence-anchor.goldfish", false);
        string dot = CssText.RuleBodyLoose(css, ".agent-dot.goldfish", false);

        Assert.Contains("outline: 2px solid var(--mud-palette-info);", ring, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("outline-offset: 2px;", ring, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("border-radius: 50%;", ring, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("color-mix(in srgb, var(--mud-palette-info) 80%, var(--mud-palette-black))", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>shell.css masks the fish glyph into the badge like the other four glyphs, so the white mark is painted over the Info tone.</summary>
    [Fact]
    public void Stylesheet_FishGlyph_IsMaskedLikeTheOthers()
    {
        string css = CssText.Read(RepoPaths.App("wwwroot", "css", "shell.css"), CssNewlines.AsIs);

        Assert.Equal(1, CssText.CountOccurrences(css, ".agent-dot.presence-glyph-fish::after {"));
        Assert.Contains("mask-image: url(", CssText.RuleBodyLoose(css, ".agent-dot.presence-glyph-fish::after", false), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>The three callers pass the Agent's name to the anchor, and only the ProfileCard asks for the badge alone.</summary>
    [Fact]
    public void Callers_PassAgentName_AndOnlyProfileCardIsBadgeOnly()
    {
        string label = File.ReadAllText(RepoPaths.App("Components", "Shared", "AgentLabel.razor"));
        string card = File.ReadAllText(RepoPaths.App("Components", "Shared", "ProfileCard.razor"));
        string members = File.ReadAllText(RepoPaths.App("Components", "Sidebar", "MembersPanel.razor"));

        Assert.All([label, card, members], source => Assert.Contains("AgentName=", source, StringComparison.Ordinal)); // contains-ok: each caller source must spell the parameter
        Assert.Equal([0, 2, 0], new[] { label, card, members }.Select(source => CssText.CountOccurrences(source, "BadgeOnly=\"true\"")));
    }

    /// <summary>design-system.md names the fish symbol, the ring exception and the avatar label.</summary>
    [Fact]
    public void DesignSystem_DocumentsTheFishAndTheRing()
    {
        string doc = File.ReadAllText(RepoPaths.Combine("docs", "engineering", "design-system.md"));

        Assert.Contains("MaterialSymbols.Fish", doc, StringComparison.Ordinal); // contains-ok: the doc names the constant
        Assert.Contains("except the goldfish ring", doc, StringComparison.Ordinal); // contains-ok: the Profile ring row's exception
        Assert.Contains("{Name}, in goldfish mode", doc, StringComparison.Ordinal); // contains-ok: the Avatars row's aria-label
    }

    /// <summary>
    /// The fish symbol is for the goldfish concept only: the chip and the transcript rows and stamp draw it, and the avatar badge
    /// draws it from a stylesheet mask rather than the constant, so exactly these two sources reference it.
    /// </summary>
    [Fact]
    public void MaterialSymbols_Fish_IsReferencedFromExactlyTheExpectedFiles()
    {
        string root = RepoPaths.App();
        string[] users = [.. Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("obj/", StringComparison.Ordinal) && !path.StartsWith("bin/", StringComparison.Ordinal) && path != "Tabs/MaterialSymbols.cs")
            .Where(path => File.ReadAllText(Path.Combine(root, path)).Contains("MaterialSymbols.Fish", StringComparison.Ordinal)) // contains-ok: a source scan for a constant's name
            .Order(StringComparer.Ordinal)];

        Assert.Equal(["Components/Chat/GoldfishChip.razor", "Components/Shared/MessageList.razor"], users);
    }

    private static IRenderedComponent<PresenceAnchor> RenderAnchor(MudBunitContext ctx, string name, bool badgeOnly) =>
        ctx.Render<PresenceAnchor>(parameters => parameters
            .Add(p => p.AgentName, name)
            .Add(p => p.BadgeOnly, badgeOnly)
            .Add(p => p.Cue, PresenceCue.From(App.Acp.PersonaState.Online))
            .AddChildContent<AgentAvatar>(avatar => avatar.Add(p => p.Name, name)));

    private sealed class Rig : IDisposable
    {
        private readonly TempDataDir dir;

        private Rig(TempDataDir dir, GoldfishStore store, GoldfishLive live, User ruth, User kai)
        {
            this.dir = dir;
            this.Store = store;
            this.Live = live;
            this.Ruth = ruth;
            this.Kai = kai;
        }

        public GoldfishStore Store { get; }

        public GoldfishLive Live { get; }

        public User Ruth { get; }

        public User Kai { get; }

        public static async Task<Rig> CreateAsync(CancellationToken ct)
        {
            TempDataDir dir = new();
            SqliteTeamDirectory team = dir.CreateDirectory();
            await team.InitializeAsync("You", ct);
            User ruth = await team.UpsertAgentUserAsync("Ruth", null, ct) ?? throw new InvalidOperationException("Ruth was not created.");
            User kai = await team.UpsertAgentUserAsync("Kai", null, ct) ?? throw new InvalidOperationException("Kai was not created.");
            GoldfishStore store = new(dir.Options());
            return new Rig(dir, store, new GoldfishLive(store, team, NullLogger<GoldfishLive>.Instance), ruth, kai);
        }

        public MudBunitContext NewContext()
        {
            MudBunitContext ctx = new();
            ctx.Services.AddSingleton(this.Live);
            return ctx;
        }

        public async Task PossessAsync(string id, User agent, PossessionState state, CancellationToken ct)
        {
            Possession row = new(id, agent.Id, "r1", null, "Look into it", state, null, null, T0, null, 0, 0m, null, null);
            _ = this.Store.TryInsert(row);
            await this.Live.RefreshAsync(ct);
        }

        public int ChangedSubscribers() =>
            ((Delegate?)typeof(GoldfishLive).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this.Live))?.GetInvocationList().Length ?? 0;

        public void Dispose()
        {
            this.Live.Dispose();
            this.dir.Dispose();
        }
    }
}
