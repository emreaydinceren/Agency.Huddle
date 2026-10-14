namespace Agency.Huddle.Tests.Ui.References;

using Bunit;
using Agency.Huddle.App.Components.References;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;

/// <summary>
/// Pins the list half of the Reference picker (AT-T11, spec §5.3 and §11, plan T8.2): the groups and
/// their roles, the highlighted row, the dimmed row with its note, the disabled empty row, the hidden
/// footer, the polite live region and a pick on click.
/// </summary>
public sealed class ReferencePickerTests
{
    private static readonly TeamHit marketingHit = new("Marketing", "Development Team", 4, MatchTier.ExactName);
    private static readonly TeamHit platformHit = new("Platform", null, 1, MatchTier.NamePrefix);

    /// <summary>Two groups are drawn, each a <c>role="group"</c> whose <c>aria-label</c> is its heading.</summary>
    [Fact]
    public async Task Renders_GroupsWithRoles()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 0);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        IReadOnlyList<AngleSharp.Dom.IElement> groups = cut.FindAll("[role=listbox] > [role=group]");
        Assert.Equal(2, groups.Count);
        Assert.Equal("Teams", groups[0].GetAttribute("aria-label"));
        Assert.Equal("Rooms", groups[1].GetAttribute("aria-label"));
        Assert.Equal("true", groups[0].QuerySelector(".composer-picker-heading")?.GetAttribute("aria-hidden"));
        Assert.Equal("listbox", cut.Find("#refs").GetAttribute("role"));
    }

    /// <summary>The row at the highlight index is <c>aria-selected="true"</c>; the others are <c>"false"</c>.</summary>
    [Fact]
    public async Task HighlightedRow_AriaSelected()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 1);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        Assert.Equal("false", cut.Find("#refs-0").GetAttribute("aria-selected"));
        Assert.Equal("true", cut.Find("#refs-1").GetAttribute("aria-selected"));
    }

    /// <summary>A dimmed row carries the <c>composer-picker-dimmed</c> class and its note, <c>Not in this Room</c>.</summary>
    [Fact]
    public async Task DimmedRow_HasNoteAndClass()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 0);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        AngleSharp.Dom.IElement row = cut.Find("#refs-1");
        Assert.Equal("composer-picker-item composer-picker-dimmed", row.ClassName);
        Assert.Equal("Not in this Room", cut.Find("#refs-1 .composer-picker-note").TextContent);
        Assert.Equal("Platform, Not in this Room", cut.Find("#refs-1").GetAttribute("aria-label"));
    }

    /// <summary>With no rows the empty text is a single <c>role="option"</c> marked <c>aria-disabled="true"</c>.</summary>
    [Fact]
    public async Task EmptyText_IsADisabledOption()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = new([], "Nothing matches '@zz'.", "Narrow with teammate: task: room: team: lib:", "No results", 0);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        AngleSharp.Dom.IElement option = cut.Find("[role=option]");
        Assert.Equal("true", option.GetAttribute("aria-disabled"));
        Assert.Equal("Nothing matches '@zz'.", option.TextContent.Trim());
    }

    /// <summary>The footer is drawn for sighted users and hidden from assistive technology.</summary>
    [Fact]
    public async Task Footer_IsHiddenFromAssistiveTech()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 0);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        AngleSharp.Dom.IElement footer = cut.Find(".composer-picker-footer");
        Assert.Equal("true", footer.GetAttribute("aria-hidden"));
        Assert.Equal("Narrow with teammate: task: room: team: lib:", footer.TextContent.Trim());
    }

    /// <summary>The visually hidden status region is polite and carries the view's live text.</summary>
    [Fact]
    public async Task LiveRegion_HasLiveText()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 0);

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs"));

        AngleSharp.Dom.IElement live = cut.Find("[role=status]");
        Assert.Equal("polite", live.GetAttribute("aria-live"));
        Assert.Equal("2 results", live.TextContent);
    }

    /// <summary>A click on a row raises <c>OnPick</c> with that row.</summary>
    [Fact]
    public async Task Click_RaisesOnPick()
    {
        await using MudBunitContext ctx = new();
        ReferencePickerView view = View(highlight: 0);
        ReferencePickerRow? picked = null;

        IRenderedComponent<ReferencePicker> cut = ctx.Render<ReferencePicker>(p => p
            .Add(c => c.View, view)
            .Add(c => c.ListboxId, "refs")
            .Add(c => c.OnPick, (ReferencePickerRow row) => picked = row));

        await cut.Find("#refs-1").ClickAsync();

        Assert.NotNull(picked);
        Assert.Equal("refs-1", picked.Id);
        Assert.Equal("[Platform](/teams/Platform)", picked.InsertText);
    }

    /// <summary>Builds the two-row view the tests share: a Team, then a dimmed Room with its note.</summary>
    /// <param name="highlight">The highlighted row index.</param>
    /// <returns>The view.</returns>
    private static ReferencePickerView View(int highlight)
    {
        ReferencePickerRow teamRow = new("refs-0", marketingHit, "[Marketing](/teams/Marketing)", false, null, "Marketing, Development Team");
        ReferencePickerRow roomRow = new("refs-1", platformHit, "[Platform](/teams/Platform)", true, "Not in this Room", "Platform, Not in this Room");
        ReferencePickerGroup teams = new("Teams", [teamRow]);
        ReferencePickerGroup rooms = new("Rooms", [roomRow]);
        return new ReferencePickerView([teams, rooms], null, "Narrow with teammate: task: room: team: lib:", "2 results", highlight);
    }
}
