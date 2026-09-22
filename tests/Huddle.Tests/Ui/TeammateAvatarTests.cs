using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="TeammateAvatar"/> on its own, covering the Image-then-Label-then-monogram
/// precedence, the <see cref="Avatar.Background"/> inline style, and the single-root-element
/// constraint <see cref="TeammateCardTests"/> and <see cref="TeammatesPageTests"/> both depend on.
/// Uses the plain generic <see cref="BunitContext.Render{TComponent}(Action{ComponentParameterCollectionBuilder{TComponent}})"/>
/// rather than <see cref="MudBunitContext.RenderWithPopovers"/>: this component shows no popover and
/// no dialog, so it needs neither provider.
/// </summary>
public sealed class TeammateAvatarTests
{
    /// <summary>With no customisation at all (<see cref="Avatar.None"/>), the monogram renders - first-and-last-word initials, per <see cref="TeammateAvatar.Monogram(string)"/>.</summary>
    [Theory]
    [InlineData("Emily Lee", "EL")]
    [InlineData("Chief of Staff", "CS")]
    [InlineData("echo", "E")]
    public async Task Render_AvatarNone_ShowsMonogramFromFirstAndLastWord(string name, string expectedMonogram)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, name)
            .Add(p => p.Avatar, Avatar.None));

        Assert.Equal(expectedMonogram, cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>A <see cref="Avatar.Label"/> beats the monogram fallback.</summary>
    [Fact]
    public async Task Render_LabelSet_BeatsTheMonogram()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "AB", Image: null, Background: null);

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar));

        Assert.Equal("AB", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>An <see cref="Avatar.Image"/> beats a <see cref="Avatar.Label"/>: an <c>&lt;img&gt;</c> renders and the label text is absent.</summary>
    [Fact]
    public async Task Render_ImageSet_BeatsTheLabel()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "AB", Image: "abc123.png", Background: null);

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.NotEmpty(root.QuerySelectorAll("img"));
        Assert.DoesNotContain("AB", root.TextContent, StringComparison.Ordinal);
    }

    /// <summary>The rendered image's <c>src</c> starts with the avatar image route, <c>/teammate-avatars/</c>.</summary>
    [Fact]
    public async Task Render_ImageSet_SrcStartsWithTheTeammateAvatarsRoute()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var image = cut.Find(".mud-avatar img");
        Assert.StartsWith("/teammate-avatars/", image.GetAttribute("src"), StringComparison.Ordinal);
    }

    /// <summary>A <see cref="Avatar.Background"/> emits an inline style carrying both a <c>background</c> and a <c>color</c> declaration.</summary>
    [Fact]
    public async Task Render_BackgroundSet_EmitsInlineStyleWithBackgroundAndColor()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "AB", Image: null, Background: "#4a154b");

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar));

        var style = cut.Find(".mud-avatar").GetAttribute("style") ?? string.Empty;
        Assert.Contains("background:", style, StringComparison.Ordinal);
        Assert.Contains("color:", style, StringComparison.Ordinal);
    }

    /// <summary>With no <see cref="Avatar.Background"/>, no inline style is emitted at all, and the root still carries MudBlazor's own <c>mud-avatar-filled-primary</c> class from <c>Color="Color.Primary"</c>.</summary>
    [Fact]
    public async Task Render_NoBackground_EmitsNoInlineStyle_AndKeepsThePrimaryThemeClass()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, Avatar.None));

        var root = cut.Find(".mud-avatar");

        // MudAvatar always renders a style attribute (empty when nothing is set), so "no inline
        // background" means empty or absent, not strictly null.
        Assert.True(string.IsNullOrEmpty(root.GetAttribute("style")));

        // MudAvatar's own class for Color="Color.Primary" is "mud-avatar-filled-primary" (its
        // Color/Variant-driven class, not a generic "mud-theme-*" token) - confirmed against the
        // rendered markup rather than assumed.
        Assert.Contains("mud-avatar-filled-primary", root.ClassList);
    }

    /// <summary>A label of seven text elements renders only its first three - <see cref="Avatar.TrimLabel(string?)"/>, applied again at render time.</summary>
    [Fact]
    public async Task Render_LabelOfSevenTextElements_RendersThree()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDEFG", Image: null, Background: null);

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        Assert.Equal("ABC", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>A zero-width-joiner family emoji label counts as one text element, so it renders unchanged rather than being cut mid-sequence.</summary>
    [Fact]
    public async Task Render_ZwjFamilyEmojiLabel_RendersAsOneTextElement()
    {
        await using MudBunitContext ctx = new();
        const string family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";
        Avatar avatar = new(Label: family, Image: null, Background: null);

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        Assert.Equal(family, cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>
    /// This component renders exactly one root element - the <c>MudAvatar</c> itself, with no wrapping
    /// <c>&lt;div&gt;</c> or <c>&lt;span&gt;</c>. Asserted directly, since <c>TeammateCardTests</c> and
    /// <c>TeammatesPageTests</c> both locate ".mud-avatar" at a fixed position in the DOM relative to
    /// its siblings and would fail silently in a different way if this ever regressed.
    /// </summary>
    [Fact]
    public async Task Render_RootIsExactlyOneElement()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeammateAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, Avatar.None));

        var root = Assert.Single(cut.Nodes);
        var element = Assert.IsAssignableFrom<IElement>(root);
        Assert.Contains("mud-avatar", element.ClassList);
    }
}
