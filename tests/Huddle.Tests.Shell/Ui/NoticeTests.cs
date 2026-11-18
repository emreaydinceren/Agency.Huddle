using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The one notice (ComponentCatalog, Notice): its kind writes the role, so no caller writes one (Q-A1-1); it is always dense
/// (Q-A1-3); a dismiss handler draws the close button; and the caller's class is a hook beside the component's own.
/// </summary>
public sealed class NoticeTests
{
    /// <summary>
    /// Left unset, the kind follows the severity: an Error interrupts and is announced at once, every other severity reports
    /// and is announced politely.
    /// </summary>
    /// <param name="severity">The notice's severity.</param>
    /// <param name="role">The role it must carry.</param>
    [Theory]
    [InlineData(NoticeLevel.Error, "alert")]
    [InlineData(NoticeLevel.Warning, "status")]
    [InlineData(NoticeLevel.Info, "status")]
    [InlineData(NoticeLevel.Success, "status")]
    public async Task Role_KindUnset_FollowsTheSeverity(NoticeLevel severity, string role)
    {
        await using MudBunitContext ctx = new();

        IElement alert = Render(ctx, severity, kind: null).Find(".mud-alert");

        Assert.Equal(role, alert.GetAttribute("role"));
    }

    /// <summary>A set kind wins over the severity's default: a Warning the Human must act on now interrupts.</summary>
    /// <param name="severity">The notice's severity.</param>
    /// <param name="kind">The kind the caller sets.</param>
    /// <param name="role">The role it must carry.</param>
    [Theory]
    [InlineData(NoticeLevel.Warning, NoticeKind.Interruption, "alert")]
    [InlineData(NoticeLevel.Error, NoticeKind.Consequence, "status")]
    public async Task Role_KindSet_OverridesTheSeverity(NoticeLevel severity, NoticeKind kind, string role)
    {
        await using MudBunitContext ctx = new();

        IElement alert = Render(ctx, severity, kind).Find(".mud-alert");

        Assert.Equal(role, alert.GetAttribute("role"));
    }

    /// <summary>
    /// Every notice is dense and carries the <c>notice</c> class the shared spacing rule selects, beside the caller's hook, and a
    /// passed-through attribute reaches the element; a stray <c>role</c> from the caller cannot replace the kind's.
    /// </summary>
    [Fact]
    public async Task Render_AnyNotice_IsDenseWithItsClassesAndAttributes()
    {
        await using MudBunitContext ctx = new();

        IElement alert = ctx.Render<Notice>(parameters => parameters
            .Add(static p => p.Severity, NoticeLevel.Warning)
            .Add(static p => p.Class, "tasks-sprints-alert")
            .AddUnmatched("data-testid", "sprints")
            .AddUnmatched("role", "alert")
            .AddChildContent("sprints.md has lines that were ignored")).Find(".mud-alert");

        Assert.Contains("mud-dense", alert.ClassList);
        Assert.Contains("notice", alert.ClassList);
        Assert.Contains("tasks-sprints-alert", alert.ClassList);
        Assert.Equal("sprints", alert.GetAttribute("data-testid"));
        Assert.Equal("status", alert.GetAttribute("role"));
        Assert.Equal("sprints.md has lines that were ignored", alert.TextContent.Trim());
    }

    /// <summary>With no dismiss handler there is no close button; with one, the close button raises it.</summary>
    [Fact]
    public async Task OnDismiss_Set_DrawsACloseButtonThatRaisesIt()
    {
        await using MudBunitContext ctx = new();
        int dismissed = 0;

        IRenderedComponent<Notice> plain = Render(ctx, NoticeLevel.Info, kind: null);
        IRenderedComponent<Notice> closable = ctx.Render<Notice>(parameters => parameters
            .Add(static p => p.OnDismiss, () => dismissed++)
            .AddChildContent("Account connectors are now off."));
        await closable.Find(".mud-alert-close button").ClickAsync(new());

        Assert.Empty(plain.FindAll(".mud-alert-close"));
        Assert.Equal(1, dismissed);
    }

    /// <summary>Renders a notice with one sentence.</summary>
    /// <param name="ctx">The bUnit context.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="kind">The kind, or null for the severity's default.</param>
    /// <returns>The rendered notice.</returns>
    private static IRenderedComponent<Notice> Render(MudBunitContext ctx, NoticeLevel severity, NoticeKind? kind) =>
        ctx.Render<Notice>(parameters => parameters
            .Add(static p => p.Severity, severity)
            .Add(static p => p.Kind, kind)
            .AddChildContent("Something happened."));
}
