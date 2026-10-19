using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Agency.Huddle.App.Sidebar;

namespace Agency.Huddle.Tests.Ui.Sidebar;

/// <summary>
/// A test-only Right sidebar panel. It draws its Subject as text, counts how often it was initialised
/// (and takes a fresh instance id, so a remount is visible), and reports a count when a test clicks it.
/// </summary>
internal sealed class ProbePanel : ComponentBase
{
    private int inits;
    private Guid instance;

    /// <summary>The Subject the host passes.</summary>
    [Parameter]
    public PageSubject? Subject { get; set; }

    /// <summary>The Tab id the host passes.</summary>
    [Parameter]
    public string? TabId { get; set; }

    /// <summary>The count the set button reports.</summary>
    [Parameter]
    public int? CountToSet { get; set; }

    /// <summary>The host's cascaded context, through which the panel reports its count.</summary>
    [CascadingParameter]
    public SidebarPanelContext? Panel { get; set; }

    /// <summary>Counts the initialisations and takes a fresh instance id.</summary>
    protected override void OnInitialized()
    {
        this.inits++;
        this.instance = Guid.NewGuid();
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string text = this.Subject switch
        {
            DocumentSubject document => document.Text ?? string.Empty,
            RoomSubject room => room.RoomId,
            _ => string.Empty,
        };

        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "probe");
        builder.AddAttribute(2, "data-inits", this.inits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AddAttribute(3, "data-instance", this.instance.ToString());
        builder.AddAttribute(4, "data-text", text);
        builder.AddContent(5, "probe");
        builder.CloseElement();

        builder.OpenElement(6, "button");
        builder.AddAttribute(7, "class", "probe-set");
        builder.AddAttribute(8, "onclick", EventCallback.Factory.Create(this, () => this.Panel?.SetCount(this.CountToSet)));
        builder.AddContent(9, "set");
        builder.CloseElement();

        builder.OpenElement(10, "button");
        builder.AddAttribute(11, "class", "probe-clear");
        builder.AddAttribute(12, "onclick", EventCallback.Factory.Create(this, () => this.Panel?.SetCount(null)));
        builder.AddContent(13, "clear");
        builder.CloseElement();
    }
}
