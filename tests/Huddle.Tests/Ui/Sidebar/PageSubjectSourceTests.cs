using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>Pins <see cref="PageSubjectSource"/>: a page publishes its Subject for its own Tab, and takes it back when it goes.</summary>
public sealed class PageSubjectSourceTests
{
    private static MudBunitContext NewContext()
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton<RightSidebarService>();
        return ctx;
    }

    private static IRenderedComponent<CascadingValue<TabPageContext>> RenderSource(MudBunitContext ctx, PageSubject? value, bool withTab = true)
    {
        TabPageContext? page = withTab ? new TabPageContext("/x", "t1") : null;
        return ctx.Render<CascadingValue<TabPageContext>>(p => p
            .Add(c => c.Value, page)
            .AddChildContent<PageSubjectSource>(s => s.Add(c => c.Value, value)));
    }

    /// <summary>The source publishes its value for the cascaded Tab.</summary>
    [Fact]
    public async Task Source_PublishesItsValue()
    {
        await using MudBunitContext ctx = NewContext();
        RoomSubject subject = new("r1");

        _ = RenderSource(ctx, subject);

        Assert.Equal(subject, ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor("t1"));
    }

    /// <summary>Re-rendering with a null value takes the Subject away.</summary>
    [Fact]
    public async Task Source_NullValue_RemovesIt()
    {
        await using MudBunitContext ctx = NewContext();
        IRenderedComponent<CascadingValue<TabPageContext>> cut = RenderSource(ctx, new RoomSubject("r1"));

        cut.Render(p => p
            .Add(c => c.Value, new TabPageContext("/x", "t1"))
            .AddChildContent<PageSubjectSource>(s => s.Add(c => c.Value, null)));

        Assert.Null(ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor("t1"));
    }

    /// <summary>Disposing the source (the page unmounts) removes its Subject.</summary>
    [Fact]
    public async Task Source_Disposed_RemovesIt()
    {
        await using MudBunitContext ctx = NewContext();
        IRenderedComponent<CascadingValue<TabPageContext>> cut = RenderSource(ctx, new RoomSubject("r1"));

        cut.Render(p => p.Add(c => c.Value, new TabPageContext("/x", "t1")));

        Assert.Null(ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor("t1"));
    }

    /// <summary>Outside a Tab the source publishes nothing.</summary>
    [Fact]
    public async Task Source_NoTab_PublishesNothing()
    {
        await using MudBunitContext ctx = NewContext();
        int raised = 0;
        ctx.Services.GetRequiredService<RightSidebarService>().Changed += () => raised++;

        _ = RenderSource(ctx, new RoomSubject("r1"), withTab: false);

        Assert.Equal(0, raised);
        Assert.Null(ctx.Services.GetRequiredService<RightSidebarService>().SubjectFor("t1"));
    }
}
