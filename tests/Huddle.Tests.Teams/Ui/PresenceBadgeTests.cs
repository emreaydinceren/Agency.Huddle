using Bunit;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Presence;
using Agency.Huddle.App.Tasks;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The presence badge (<see cref="PresenceBadge"/>, <see cref="PresenceAnchor"/>, <see cref="StatusDot"/>) and the one mapping
/// (<see cref="PresenceCue"/>) that folds both status systems, Health (<see cref="PersonaState"/>) and Presence
/// (<see cref="PresenceState"/>), into four glyphs: a check, a pulse, a dash and a hollow ring. The colour is never the only cue.
/// </summary>
public sealed class PresenceBadgeTests
{
    /// <summary>Health maps to a glyph per state: Online a check, Starting a pulse, Degraded a dash, Offline a hollow ring.</summary>
    [Theory]
    [InlineData(PersonaState.Online, PresenceGlyph.Check)]
    [InlineData(PersonaState.Starting, PresenceGlyph.Pulse)]
    [InlineData(PersonaState.Degraded, PresenceGlyph.Dash)]
    [InlineData(PersonaState.Offline, PresenceGlyph.Ring)]
    public void Health_MapsToItsGlyph(PersonaState state, PresenceGlyph glyph)
    {
        Assert.Equal(glyph, PresenceCue.From(state).Glyph);
    }

    /// <summary>Presence maps into the same four glyphs: Awake (a Turn running) a pulse, Asleep (online, idle) and Offline a hollow ring.</summary>
    [Theory]
    [InlineData(PresenceState.Awake, PresenceGlyph.Pulse, "Awake")]
    [InlineData(PresenceState.Asleep, PresenceGlyph.Ring, "Asleep")]
    [InlineData(PresenceState.Offline, PresenceGlyph.Ring, "Offline")]
    public void Presence_MapsToItsGlyphAndKeepsItsOwnWord(PresenceState state, PresenceGlyph glyph, string label)
    {
        PresenceCue cue = PresenceCue.From(state);

        Assert.Equal(glyph, cue.Glyph);
        Assert.Equal(label, cue.Label);
    }

    /// <summary>A standalone <see cref="StatusDot"/> carries the glyph class and the state's word as its accessible name and title.</summary>
    [Theory]
    [InlineData(PersonaState.Online, "presence-glyph-check", "Online")]
    [InlineData(PersonaState.Starting, "presence-glyph-pulse", "Starting")]
    [InlineData(PersonaState.Degraded, "presence-glyph-dash", "Degraded")]
    [InlineData(PersonaState.Offline, "presence-glyph-ring", "Offline")]
    public async Task StatusDot_CarriesTheGlyphAndTheLabel(PersonaState state, string glyphClass, string label)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StatusDot>(parameters => parameters.Add(p => p.State, state));

        var dot = cut.Find("span.agent-dot");
        Assert.Contains(glyphClass, dot.ClassList);
        Assert.Contains("presence-badge", dot.ClassList);
        Assert.DoesNotContain("on-avatar", dot.ClassList);
        Assert.Equal(label, dot.GetAttribute("aria-label"));
        Assert.Equal(label, dot.GetAttribute("title"));
    }

    /// <summary>The four Health glyphs are four different shapes, so no two states look alike once colour is taken away.</summary>
    [Fact]
    public async Task StatusDot_FourStates_FourDifferentGlyphs()
    {
        await using MudBunitContext ctx = new();
        PersonaState[] states = [PersonaState.Online, PersonaState.Starting, PersonaState.Degraded, PersonaState.Offline];

        List<string> glyphs = [.. states.Select(state => ctx
            .Render<StatusDot>(parameters => parameters.Add(p => p.State, state))
            .Find("span.agent-dot").ClassList.Single(c => c.StartsWith("presence-glyph-", StringComparison.Ordinal)))];

        Assert.Equal(4, glyphs.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A reason is added to the tooltip after the state's word, and the accessible name stays the word alone.</summary>
    [Fact]
    public async Task StatusDot_Detail_ExtendsTheTitleButNotTheAccessibleName()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<StatusDot>(parameters => parameters
            .Add(p => p.State, PersonaState.Offline)
            .Add(p => p.Detail, "adapter not found"));

        var dot = cut.Find("span.agent-dot");
        Assert.Equal("Offline", dot.GetAttribute("aria-label"));
        Assert.Equal("Offline: adapter not found", dot.GetAttribute("title"));
    }

    /// <summary>On an avatar the badge sits in the anchor beside the avatar, which stays the one root of its own component.</summary>
    [Fact]
    public async Task Anchor_WithACue_PutsTheBadgeOnTheAvatar()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PresenceAnchor>(parameters => parameters
            .Add(p => p.Cue, PresenceCue.From(PersonaState.Online))
            .AddChildContent<AgentAvatar>(avatar => avatar.Add(p => p.Name, "Helen")));

        var anchor = cut.Find("span.presence-anchor");
        Assert.NotNull(anchor.QuerySelector(".mud-avatar"));
        var badge = Assert.Single(anchor.QuerySelectorAll("span.agent-dot.on-avatar.presence-glyph-check"));
        Assert.Equal("Online", badge.GetAttribute("aria-label"));
    }

    /// <summary>With no cue (the Human, or an unknown presence) the anchor draws the avatar and no badge, never an Offline guess.</summary>
    [Fact]
    public async Task Anchor_WithoutACue_DrawsNoBadge()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<PresenceAnchor>(parameters => parameters
            .AddChildContent<AgentAvatar>(avatar => avatar.Add(p => p.Name, "Helen")));

        Assert.Empty(cut.FindAll("span.agent-dot"));
        Assert.NotEmpty(cut.FindAll(".mud-avatar"));
    }

    /// <summary>The Agent label puts the badge on its avatar rather than as a bare dot beside it.</summary>
    [Fact]
    public async Task AgentLabel_PutsTheBadgeOnTheAvatar()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentLabel>(parameters => parameters
            .Add(p => p.Name, "Helen")
            .Add(p => p.State, PersonaState.Degraded));

        var badge = cut.Find(".presence-anchor > span.agent-dot.on-avatar");
        Assert.Contains("presence-glyph-dash", badge.ClassList);
        Assert.Equal("Degraded", badge.GetAttribute("aria-label"));
    }

    /// <summary>A Task assignee's presence badge frame carries the glyph and tone classes that the stylesheet draws from.</summary>
    [Theory]
    [InlineData(PresenceState.Awake, "task-card-presence presence-glyph-pulse awake")]
    [InlineData(PresenceState.Asleep, "task-card-presence presence-glyph-ring asleep")]
    [InlineData(PresenceState.Offline, "task-card-presence presence-glyph-ring offline")]
    public void TaskAssignee_PresenceClass_CarriesGlyphAndTone(PresenceState state, string expected)
    {
        Assert.Equal(expected, TaskAssignee.PresenceClass(state));
    }
}
