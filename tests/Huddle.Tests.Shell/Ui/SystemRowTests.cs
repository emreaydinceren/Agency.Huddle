using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="SystemRow"/> on its own: a native details with the jump hooks, one summary line and an optional detail.</summary>
public sealed class SystemRowTests
{
    /// <summary>The row is a native details carrying the Message id and tabindex -1, and its summary holds the icon, the text, the time and the chevron in that order.</summary>
    [Fact]
    public async Task Row_IsANativeDetails_WithTheJumpHooksAndOneSummaryLine()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SystemRow> cut = Render(ctx, detail: null);

        IElement row = cut.Find("details.system-row");
        Assert.Equal(("m-7", "-1"), (row.GetAttribute("data-message-id"), row.GetAttribute("tabindex")));
        IElement summary = Assert.IsAssignableFrom<IElement>(row.FirstElementChild);
        Assert.Equal("SUMMARY", summary.TagName);
        Assert.Equal(["svg", "SPAN", "SPAN", "svg"], summary.Children.Select(child => child.TagName.Equals("svg", StringComparison.OrdinalIgnoreCase) ? "svg" : child.TagName).ToArray());
        Assert.Equal("Task woke Nova", cut.Find(".system-row-text").TextContent);
        Assert.Equal("09:41", cut.Find(".system-row-time").TextContent);
        Assert.True(summary.LastElementChild?.ClassList.Contains("system-row-chevron"));
    }

    /// <summary>With no detail the row draws no detail section at all, so a pruned goldfish return row is only its summary.</summary>
    [Fact]
    public async Task WithoutDetail_DrawsNoDetailSection()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SystemRow> cut = Render(ctx, detail: null);

        Assert.Empty(cut.FindAll(".system-row-detail"));
    }

    /// <summary>A detail is drawn in one <c>system-row-detail</c> div after the summary, and keeps its own markup.</summary>
    [Fact]
    public async Task WithDetail_DrawsItInOneDetailDivAfterTheSummary()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<SystemRow> cut = Render(ctx, detail: static builder => builder.AddMarkupContent(0, "<ul><li>Status: Done</li></ul>"));

        IElement detail = Assert.Single(cut.FindAll("details.system-row > .system-row-detail"));
        Assert.Equal("Status: Done", detail.QuerySelector("li")?.TextContent);
        Assert.Equal("SUMMARY", detail.PreviousElementSibling?.TagName);
    }

    private static IRenderedComponent<SystemRow> Render(MudBunitContext ctx, Microsoft.AspNetCore.Components.RenderFragment? detail) =>
        ctx.Render<SystemRow>(parameters => parameters
            .Add(p => p.MessageId, "m-7")
            .Add(p => p.Icon, MudBlazor.Icons.Material.Outlined.TaskAlt)
            .Add(p => p.Time, "09:41")
            .Add(p => p.Summary, static builder => builder.AddContent(0, "Task woke Nova"))
            .Add(p => p.Detail, detail));
}
