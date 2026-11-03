using Agency.Huddle.App.Components.Settings;
using Agency.Huddle.Tests.Acp.Fakes;
using Bunit;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Settings › Prompts warns on the three prompts agents rely on (Spec §6.9; Settled texts T19, T20): the
/// warning is a <c>MudAlert</c> under the field, built from the real catalog through
/// <see cref="PromptFieldFactory"/>, so a missing flag on a definition or a panel that ignores it both fail.
/// </summary>
public sealed class ReliedPromptWarningTests
{
    /// <summary>T19, pinned verbatim.</summary>
    private const string ChatRulesWarning = "Agents rely on this to avoid replying to every message in group Rooms.";

    /// <summary>T20, pinned verbatim.</summary>
    private const string SessionWarning = "Agents rely on this to keep each Room's conversation to that Room.";

    /// <summary>The <c>systemPrompt.chatRules</c> row shows T19 in one warning alert (the keys are read at PromptCatalog.cs:144).</summary>
    [Fact]
    public async Task ChatRules_ShowsTheGroupRoomWarning()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderPanel(ctx);

        AssertSingleWarning(cut, "systemPrompt.chatRules", ChatRulesWarning);
    }

    /// <summary>The <c>systemPrompt.roomSessions</c> row shows T20 (key at PromptCatalog.cs:1571).</summary>
    [Fact]
    public async Task RoomSessions_ShowsTheSessionWarning()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderPanel(ctx);

        AssertSingleWarning(cut, "systemPrompt.roomSessions", SessionWarning);
    }

    /// <summary>The <c>systemPrompt.sharedSession</c> row shows T20 (key at PromptCatalog.cs:1614).</summary>
    [Fact]
    public async Task SharedSession_ShowsTheSessionWarning()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderPanel(ctx);

        AssertSingleWarning(cut, "systemPrompt.sharedSession", SessionWarning);
    }

    /// <summary>The <c>systemPrompt.orientation</c> row, which no agent relies on in this way, shows no alert.</summary>
    [Fact]
    public async Task Orientation_ShowsNoWarning()
    {
        await using MudBunitContext ctx = new();

        var cut = RenderPanel(ctx);

        Assert.Empty(FieldOf(cut, "systemPrompt.orientation").QuerySelectorAll(".mud-alert"));
    }

    /// <summary>With stray override files the panel lists them under the heading "Unknown overrides"; with none it shows no such heading.</summary>
    [Fact]
    public async Task UnknownOverrides_AreListedUnderTheirHeadingOnlyWhenPresent()
    {
        await using MudBunitContext ctx = new();
        var groups = PromptFieldFactory.Build(new FakePromptSource(), new Dictionary<string, string>(StringComparer.Ordinal));

        var empty = ctx.Render<PromptsPanel>(parameters => parameters.Add(p => p.Groups, groups));
        var listed = ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, groups)
            .Add(p => p.UnknownOverrides, ["alpha", "zeta"]));

        Assert.DoesNotContain(empty.FindAll("h2"), h => h.TextContent.Contains("Unknown overrides", StringComparison.Ordinal));
        Assert.Contains(listed.FindAll("h2"), h => string.Equals(h.TextContent.Trim(), "Unknown overrides", StringComparison.Ordinal));
        Assert.Equal(["alpha", "zeta"], listed.FindAll(".prompts-unknown-overrides li").Select(li => li.TextContent.Trim()));
    }

    /// <summary>Renders the panel over the whole catalog with every prompt at its default.</summary>
    private static IRenderedComponent<PromptsPanel> RenderPanel(MudBunitContext ctx) =>
        ctx.Render<PromptsPanel>(parameters => parameters
            .Add(p => p.Groups, PromptFieldFactory.Build(new FakePromptSource(), new Dictionary<string, string>(StringComparer.Ordinal))));

    /// <summary>The field's wrapper element; an attribute selector, because the id holds a <c>.</c> that <c>#id</c> would read as a class.</summary>
    private static AngleSharp.Dom.IElement FieldOf(IRenderedComponent<PromptsPanel> cut, string key) =>
        cut.Find($"[id='{PromptsPanel.PromptFieldId(key)}']");

    /// <summary>Asserts the field has exactly one alert, a warning, with exactly <paramref name="text"/>.</summary>
    private static void AssertSingleWarning(IRenderedComponent<PromptsPanel> cut, string key, string text)
    {
        var alerts = FieldOf(cut, key).QuerySelectorAll(".mud-alert");

        var alert = Assert.Single(alerts);
        Assert.Contains("mud-alert-text-warning", alert.ClassList);
        Assert.Equal(text, alert.QuerySelector(".mud-alert-message")?.TextContent.Trim());
    }
}
