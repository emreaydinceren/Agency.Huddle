namespace Agency.Huddle.App.Tabs;

/// <summary>Pins the <c>needs-you</c> arms of <see cref="TabKeys.Describe(string)"/> and <see cref="TabIcons.For(string)"/> (Human gate T12.1; UI copy row 37).</summary>
public sealed class TabKeysNeedsYouTests
{
    /// <summary>The Needs You page opens as a Tab titled "Needs you", also when the address carries a Task id fragment.</summary>
    [Theory]
    [InlineData("/needs-you")]
    [InlineData("/needs-you#PLAT-0001")]
    public void Describe_NeedsYou_TitlesTheTabNeedsYou(string url)
    {
        TabDescriptor descriptor = TabKeys.Describe(url);

        Assert.Equal("needs-you", descriptor.Key);
        Assert.Equal("Needs you", descriptor.Title);
    }

    /// <summary>The Needs You Tab has an icon of its own, not the generic page icon.</summary>
    [Fact]
    public void Icon_NeedsYou_IsNotTheGenericPageIcon()
    {
        TabDescriptor descriptor = TabKeys.Describe("/needs-you");

        Assert.NotEqual(TabIcons.For("page"), TabIcons.For(descriptor.Icon));
    }
}
