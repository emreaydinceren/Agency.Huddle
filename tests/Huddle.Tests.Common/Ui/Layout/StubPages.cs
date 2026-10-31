using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Agency.Huddle.App.Components.Sidebar;
using Agency.Huddle.App.Sidebar;
using Agency.Huddle.App.Tabs;

namespace Agency.Huddle.Tests.Ui.Layout;

/// <summary>Stands in for a page that publishes a Subject to the Right sidebar with <see cref="PageSubjectSource"/>.</summary>
[Route("/sidebar")]
internal sealed class StubSidebarPage : ComponentBase
{
    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddMarkupContent(0, "<p class=\"stub-page\" id=\"page-sidebar\">sidebar</p>");

        builder.OpenComponent<PageSubjectSource>(1);
        builder.AddComponentParameter(2, nameof(PageSubjectSource.Value), new RoomSubject("stub"));
        builder.CloseComponent();
    }
}

/// <summary>
/// Stand-ins for the app's pages, found by a <see cref="TabPageRoutes"/> built over this assembly. The
/// real pages need the whole app behind them; a Tab test only needs to see which page was mounted,
/// with which parameters, and for which address.
/// </summary>
internal static class StubPages
{
    /// <summary>A <see cref="TabPageRoutes"/> over the stub pages in this file.</summary>
    public static TabPageRoutes Routes() => new(typeof(StubPages).Assembly);
}

/// <summary>Stands in for the Agents page.</summary>
[Route("/agents")]
internal sealed class StubAgentsPage : ComponentBase
{
    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, "<p class=\"stub-page\" id=\"page-agents\">agents</p>");
}

/// <summary>Stands in for the Settings page.</summary>
[Route("/settings")]
internal sealed class StubSettingsPage : ComponentBase
{
    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, "<p class=\"stub-page\" id=\"page-settings\">settings</p>");
}

/// <summary>Stands in for the Chat page: shows the Room id it was given.</summary>
[Route("/rooms/{RoomId}")]
internal sealed class StubRoomPage : ComponentBase
{
    /// <summary>The Room id from the route.</summary>
    [Parameter]
    public string? RoomId { get; set; }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "stub-page stub-room");
        builder.AddContent(2, this.RoomId);
        builder.CloseElement();
    }
}

/// <summary>Stands in for the Tasks page's View route: a view id from the route.</summary>
[Route("/tasks/{ViewId}")]
internal sealed class StubTasksViewPage : ComponentBase
{
    /// <summary>The view id from the route.</summary>
    [Parameter]
    public string? ViewId { get; set; }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, "<p class=\"stub-page\">tasks view</p>");
}

/// <summary>Stands in for the literal <c>/tasks/new</c> route, which must win over <c>/tasks/{ViewId}</c>.</summary>
[Route("/tasks/new")]
internal sealed class StubTasksNewPage : ComponentBase
{
    /// <summary>The address of the Tab showing this page, as the real Tasks page reads it.</summary>
    [CascadingParameter]
    public TabPageContext? Page { get; set; }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "stub-page stub-tasks-new");
        builder.AddContent(2, this.Page?.Path);
        builder.CloseElement();
    }
}

/// <summary>Stands in for the Library page, whose parameters come from the query string.</summary>
[Route("/library")]
internal sealed class StubLibraryPage : ComponentBase
{
    /// <summary>The root id from <c>?root=</c>.</summary>
    [Parameter]
    public string? Root { get; set; }

    /// <summary>The path from <c>?path=</c>.</summary>
    [Parameter]
    public string? Path { get; set; }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "stub-page stub-library");
        builder.AddContent(2, $"{this.Root}|{this.Path}");
        builder.CloseElement();
    }
}

/// <summary>Stands in for the Team route, which must lose to the literal <c>/teams/new</c>.</summary>
[Route("/teams/{Team}")]
internal sealed class StubTeamPage : ComponentBase
{
    /// <summary>The Team name from the route.</summary>
    [Parameter]
    public string? Team { get; set; }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, "<p class=\"stub-page\">team</p>");
}

/// <summary>Stands in for the new-Team route.</summary>
[Route("/teams/new")]
internal sealed class StubTeamNewPage : ComponentBase
{
    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddMarkupContent(0, "<p class=\"stub-page\">new team</p>");
}
