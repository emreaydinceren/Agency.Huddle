namespace Agency.Huddle.App.Tabs;

/// <summary>Pins the <c>feed</c> arms of <see cref="TabKeys.Describe(string)"/> and <see cref="TabIcons.For(string)"/>, and how a Feed Tab's URL fills the page's parameters (Feed spec 6.9; ADR-0045).</summary>
public sealed class TabKeysFeedTests
{
    /// <summary>The Feed page opens as one Tab titled "Feed", whatever filter its address carries.</summary>
    /// <param name="url">A Feed address.</param>
    [Theory]
    [InlineData("/feed")]
    [InlineData("/feed?category=tasks")]
    [InlineData("/feed?category=needs-you&agent=Nova&team=Growth")]
    public void Describe_Feed_IsOneTabTitledFeed(string url)
    {
        TabDescriptor descriptor = TabKeys.Describe(url);

        Assert.Equal("feed", descriptor.Key);
        Assert.Equal("Feed", descriptor.Title);
        Assert.Equal("feed", descriptor.Icon);
    }

    /// <summary>The Feed Tab has an icon of its own, not the generic page icon.</summary>
    [Fact]
    public void Icon_Feed_IsNotTheGenericPageIcon()
    {
        Assert.NotEqual(TabIcons.For("page"), TabIcons.For("feed"));
    }

    /// <summary>A Feed Tab's remembered address reaches the page's Category, Agent and Team parameters, so a restored Tab shows the same filter.</summary>
    [Fact]
    public void Resolve_FeedAddress_FillsTheFilterParameters()
    {
        TabPageRoutes app = new(typeof(Agency.Huddle.App.ServiceCollectionExtensions).Assembly);

        TabPage? page = app.Resolve("/feed?category=gates&agent=Nova&team=Growth%20Team");

        Assert.NotNull(page);
        Assert.Equal(typeof(Agency.Huddle.App.Components.Pages.FeedPage), page.Component);
        Assert.Equal("gates", page.Parameters["Category"]);
        Assert.Equal("Nova", page.Parameters["Agent"]);
        Assert.Equal("Growth Team", page.Parameters["Team"]);
    }
}
