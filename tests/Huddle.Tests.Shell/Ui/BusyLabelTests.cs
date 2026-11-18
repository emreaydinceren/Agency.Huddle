using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Renders <see cref="BusyLabel"/> on its own: a named status region, or plain decoration when no label is given.</summary>
public sealed class BusyLabelTests
{
    /// <summary>With a label, the spinner is a role="status" region whose words are the label, the spinner itself hidden, and the caller's class kept.</summary>
    [Fact]
    public async Task WithLabel_IsANamedStatus_WithTheSpinnerHidden()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<BusyLabel> cut = ctx.Render<BusyLabel>(parameters => parameters
            .Add(p => p.Label, "Loading profile")
            .Add(p => p.Class, "ml-2"));

        IElement root = cut.Find(".busy-label");
        Assert.Equal("status", root.GetAttribute("role"));
        Assert.True(root.ClassList.Contains("ml-2"));
        Assert.Equal("Loading profile", root.QuerySelector(".visually-hidden")?.TextContent);
        Assert.Equal("true", cut.Find(".mud-progress-circular").GetAttribute("aria-hidden"));
        Assert.True(cut.Find(".mud-progress-circular").ClassList.Contains("mud-progress-small"));
    }

    /// <summary>Without a label, the spinner is decoration: hidden from assistive technology, with no status role and no words.</summary>
    [Fact]
    public async Task WithoutLabel_IsHiddenDecoration()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<BusyLabel> cut = ctx.Render<BusyLabel>();

        IElement root = cut.Find(".busy-label");
        Assert.Null(root.GetAttribute("role"));
        Assert.Equal("true", root.GetAttribute("aria-hidden"));
        Assert.Empty(cut.FindAll(".visually-hidden"));
        Assert.Single(cut.FindAll(".mud-progress-circular"));
    }
}
