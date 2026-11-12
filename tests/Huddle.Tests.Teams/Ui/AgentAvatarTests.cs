using AngleSharp.Dom;
using Bunit;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Components.Shared;
using MudBlazor;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Renders <see cref="AgentAvatar"/> on its own, covering the Image-then-Label-then-monogram
/// precedence, the <see cref="Avatar.Background"/> inline style, and the single-root-element
/// constraint <c>AgentProfileCardTests</c> and <c>AgentsPageTests</c> both depend on.
/// Uses the plain generic <see cref="BunitContext.Render{TComponent}(Action{ComponentParameterCollectionBuilder{TComponent}})"/>
/// rather than <see cref="MudBunitContext.RenderWithPopovers"/>: this component shows no popover and
/// no dialog, so it needs neither provider.
/// </summary>
public sealed class AgentAvatarTests
{
    /// <summary>With no customisation at all (<see cref="Avatar.None"/>), the monogram renders - first-and-last-word initials, per <see cref="AgentAvatar.Monogram(string)"/>.</summary>
    [Theory]
    [InlineData("Emily Lee", "EL")]
    [InlineData("Chief of Staff", "CS")]
    [InlineData("echo", "EC")]
    [InlineData("Helen", "HE")]
    [InlineData("j", "J")]
    [InlineData("  Ada   Lovelace  ", "AL")]
    public async Task Render_AvatarNone_ShowsMonogramFromFirstAndLastWord(string name, string expectedMonogram)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
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

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
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

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.NotEmpty(root.QuerySelectorAll("img"));
        Assert.DoesNotContain("AB", root.TextContent, StringComparison.Ordinal);
    }

    /// <summary>The rendered image's <c>src</c> starts with the avatar image route, <c>/agent-avatars/</c>.</summary>
    [Fact]
    public async Task Render_ImageSet_SrcStartsWithTheAgentAvatarsRoute()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var image = cut.Find(".mud-avatar img");
        Assert.StartsWith("/agent-avatars/", image.GetAttribute("src"), StringComparison.Ordinal);
    }

    /// <summary>At <see cref="Size.Small"/> (24px) an image reads as a blur beside the monograms, so the avatar draws the monogram even when the Agent has an image.</summary>
    [Fact]
    public async Task Render_SmallSize_WithImage_DrawsTheMonogram()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Small));

        var root = cut.Find(".mud-avatar");
        Assert.Empty(root.QuerySelectorAll("img"));
        Assert.Equal("EL", root.TextContent.Trim());
        Assert.Contains("avatar-monogram", root.ClassList);
    }

    /// <summary>A pending (unsaved) image is drawn as the monogram at <see cref="Size.Small"/> too, so a 24px avatar never shows a picture.</summary>
    [Fact]
    public async Task Render_SmallSize_WithPendingImage_DrawsTheMonogram()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, Avatar.None)
            .Add(p => p.Size, Size.Small)
            .Add(p => p.PendingImage, [1, 2, 3, 4])
            .Add(p => p.PendingImageExtension, ".png"));

        var root = cut.Find(".mud-avatar");
        Assert.Empty(root.QuerySelectorAll("img"));
        Assert.Equal("EL", root.TextContent.Trim());
    }

    /// <summary>From <see cref="Size.Medium"/> up the image shows as before.</summary>
    [Fact]
    public async Task Render_MediumSize_WithImage_DrawsTheImage()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Medium));

        Assert.Single(cut.Find(".mud-avatar").QuerySelectorAll("img"));
    }

    /// <summary>
    /// The Transcript's <c>avatar-transcript</c> class makes a <see cref="Size.Small"/> avatar 32px in <c>app.css</c>, and
    /// images show from 32px up, so that avatar keeps its image.
    /// </summary>
    [Fact]
    public async Task Render_SmallSize_WithTranscriptClass_DrawsTheImage()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Small)
            .Add(p => p.Class, "avatar-transcript"));

        var root = cut.Find(".mud-avatar");
        Assert.Single(root.QuerySelectorAll("img"));
        Assert.Contains("avatar-image", root.ClassList);
    }

    /// <summary>A pending image that has not been saved yet beats the stored image and renders inline, as a data URL of its own bytes.</summary>
    [Fact]
    public async Task Render_PendingImageSet_BeatsTheStoredImage_AndRendersInline()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: null);
        byte[] bytes = [1, 2, 3, 4];

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.PendingImage, bytes)
            .Add(p => p.PendingImageExtension, ".png"));

        var image = cut.Find(".mud-avatar img");
        Assert.Equal("data:image/png;base64,AQIDBA==", image.GetAttribute("src"));
    }

    /// <summary>A pending image shows even when the avatar has no stored image at all, which is the Create card's case.</summary>
    [Fact]
    public async Task Render_PendingImageSet_WithNoStoredImage_StillRenders()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, Avatar.None)
            .Add(p => p.PendingImage, [1, 2, 3, 4])
            .Add(p => p.PendingImageExtension, ".jpg"));

        Assert.Equal("data:image/jpeg;base64,AQIDBA==", cut.Find(".mud-avatar img").GetAttribute("src"));
    }

    /// <summary>Each extension <see cref="Agency.Huddle.App.Avatars.AvatarImage.SniffExtension"/> can return maps to its own media type in the data URL.</summary>
    [Theory]
    [InlineData(".png", "image/png")]
    [InlineData(".jpg", "image/jpeg")]
    [InlineData(".webp", "image/webp")]
    public async Task Render_PendingImage_UsesTheMediaTypeOfItsExtension(string extension, string mediaType)
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.PendingImage, [1])
            .Add(p => p.PendingImageExtension, extension));

        Assert.Equal("data:" + mediaType + ";base64,AQ==", cut.Find(".mud-avatar img").GetAttribute("src"));
    }

    /// <summary>A pending image whose extension is not one the editor can produce is not previewed at all: the stored image shows, by its route.</summary>
    [Fact]
    public async Task Render_PendingImage_WithAnUnrecognisedExtension_FallsBackToTheStoredImage()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, new Avatar(Label: null, Image: "abc123.png", Background: null))
            .Add(p => p.PendingImage, [1])
            .Add(p => p.PendingImageExtension, ".gif"));

        Assert.Equal("/agent-avatars/abc123.png", cut.Find(".mud-avatar img").GetAttribute("src"));
    }

    /// <summary>Without a pending image the avatar renders exactly as before: the stored image, by its route.</summary>
    [Fact]
    public async Task Render_NoPendingImage_RendersTheStoredImageByRoute()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, new Avatar(Label: null, Image: "abc123.png", Background: null)));

        Assert.Equal("/agent-avatars/abc123.png", cut.Find(".mud-avatar img").GetAttribute("src"));
    }

    /// <summary>
    /// A <see cref="Avatar.Background"/> on letters emits the monogram recipe: the fill is the colour at 40% over Surface and
    /// the letters are the colour mixed 75% toward TextPrimary, both through <c>color-mix</c> against the Theme's own tokens -
    /// never a fixed black or white.
    /// </summary>
    [Fact]
    public async Task Render_BackgroundSet_EmitsTheColourMixedFillAndLetters()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "AB", Image: null, Background: "#4a154b");

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.Equal(
            ["--mud-palette-primary: #4a154b"],
            Declarations(root));
        Assert.Contains("avatar-monogram", root.ClassList);
    }

    /// <summary>A picture gets the <c>avatar-image</c> class and no inline colour: it sits on the neutral fill from the stylesheet, not the Agent's stored colour.</summary>
    [Fact]
    public async Task Render_Image_GetsTheImageClass_AndNoInlineColour()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: "abc123.png", Background: "#4a154b");

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.Contains("avatar-image", root.ClassList);
        Assert.DoesNotContain("avatar-monogram", root.ClassList);
        Assert.Empty(Declarations(root));
    }

    /// <summary>The root's inline style as its trimmed, non-empty declarations (MudBlazor appends its own separators).</summary>
    /// <param name="root">The avatar root.</param>
    private static List<string> Declarations(IElement root) =>
        [.. (root.GetAttribute("style") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>A caller's own class is kept beside the avatar's kind class.</summary>
    [Fact]
    public async Task Render_CallerClass_IsKeptBesideTheKindClass()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Class, "room-row-avatar"));

        var root = cut.Find(".mud-avatar");
        Assert.Contains("room-row-avatar", root.ClassList);
        Assert.Contains("avatar-monogram", root.ClassList);
    }

    /// <summary>The monogram is always two letters: a one-word name gives its first two, several words give the first and last initials, and one letter is all a one-letter name has.</summary>
    [Theory]
    [InlineData("Helen", "HE")]
    [InlineData("Jo", "JO")]
    [InlineData("N", "N")]
    [InlineData("Emily Lee", "EL")]
    [InlineData("Chief of Staff", "CS")]
    [InlineData("  ", "?")]
    public void Monogram_IsTwoLetters(string name, string expected)
    {
        Assert.Equal(expected, AgentAvatar.Monogram(name));
    }

    /// <summary>With no <see cref="Avatar.Background"/>, no inline style is emitted at all, and the root still carries MudBlazor's own <c>mud-avatar-filled-primary</c> class from <c>Color="Color.Primary"</c>.</summary>
    [Fact]
    public async Task Render_NoBackground_EmitsNoInlineStyle_AndKeepsThePrimaryThemeClass()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
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

    /// <summary>A label of eight text elements renders only its first six at Large (72px) - <see cref="Avatar.TrimLabel(string?)"/>, applied again at render time.</summary>
    [Fact]
    public async Task Render_LabelOfEightTextElements_AtLarge_RendersSix()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDEFGH", Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Large));

        Assert.Equal("ABCDEF", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>A six-character label at Medium (40px) draws its first five, so no label is under the 11px floor.</summary>
    [Fact]
    public async Task Render_SixCharacterLabel_Medium_ShowsFirstFiveWithLengthClass()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDEF", Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Medium));

        var root = cut.Find(".mud-avatar");
        Assert.Equal("ABCDE", root.TextContent.Trim());
        Assert.Contains("avatar-label-len-5", root.ClassList);
    }

    /// <summary>A five-character label at Medium renders in full and carries <c>avatar-label-len-5</c>, which <c>app.css</c> sizes down.</summary>
    [Fact]
    public async Task Render_FiveCharacterLabel_Medium_ShowsAllWithLengthClass()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDE", Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Medium));

        var root = cut.Find(".mud-avatar");
        Assert.Equal("ABCDE", root.TextContent.Trim());
        Assert.Contains("avatar-label-len-5", root.ClassList);
    }

    /// <summary>A one-element label (a letter, an emoji, a ZWJ emoji sequence) carries <c>avatar-label-len-1</c>, which <c>app.css</c> sizes up.</summary>
    [Theory]
    [InlineData("A")]
    [InlineData("😀")]
    [InlineData("🧑‍💻")]
    public async Task Render_OneElementLabel_HasLenOneClass(string label)
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: label, Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.Equal(label, root.TextContent.Trim());
        Assert.Contains("avatar-label-len-1", root.ClassList);
    }

    /// <summary>A picture never carries a label-length class, even when it also has a one-element label.</summary>
    [Fact]
    public async Task Render_PictureWithOneElementLabel_HasNoLengthClass()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "A", Image: "abc123.png", Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.DoesNotContain(root.ClassList, c => c.StartsWith("avatar-label-len-", StringComparison.Ordinal));
    }

    /// <summary>A label of two or three characters keeps today's size: no <c>avatar-label-len-</c> class.</summary>
    [Theory]
    [InlineData("AB")]
    [InlineData("ABC")]
    public async Task Render_ShortLabel_HasNoLengthClass(string label)
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: label, Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        var root = cut.Find(".mud-avatar");
        Assert.Equal(label, root.TextContent.Trim());
        Assert.DoesNotContain(root.ClassList, c => c.StartsWith("avatar-label-len-", StringComparison.Ordinal));
    }

    /// <summary>The 24px avatar has no room for six characters at the 10px floor, so it draws only the first three text elements and no length class.</summary>
    [Fact]
    public async Task Render_SixCharacterLabel_Small_ShowsFirstThreeAndNoLengthClass()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDEF", Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Small));

        var root = cut.Find(".mud-avatar");
        Assert.Equal("ABC", root.TextContent.Trim());
        Assert.DoesNotContain(root.ClassList, c => c.StartsWith("avatar-label-len-", StringComparison.Ordinal));
    }

    /// <summary>A Small avatar wearing <c>avatar-transcript</c> is 32px, still under 40px, so it draws the first three characters and no length class.</summary>
    [Fact]
    public async Task Render_SixCharacterLabel_SmallWithTranscriptClass_ShowsFirstThreeAndNoLengthClass()
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: "ABCDEF", Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Small)
            .Add(p => p.Class, "avatar-transcript"));

        var root = cut.Find(".mud-avatar");
        Assert.Equal("ABC", root.TextContent.Trim());
        Assert.DoesNotContain(root.ClassList, c => c.StartsWith("avatar-label-len-", StringComparison.Ordinal));
    }

    /// <summary>A zero-width-joiner family emoji label counts as one text element, so it renders unchanged rather than being cut mid-sequence.</summary>
    [Fact]
    public async Task Render_ZwjFamilyEmojiLabel_RendersAsOneTextElement()
    {
        await using MudBunitContext ctx = new();
        const string family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";
        Avatar avatar = new(Label: family, Image: null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, avatar));

        Assert.Equal(family, cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>
    /// This component renders exactly one root element - the <c>MudAvatar</c> itself, with no wrapping
    /// <c>&lt;div&gt;</c> or <c>&lt;span&gt;</c>. Asserted directly, since <c>AgentProfileCardTests</c> and
    /// <c>AgentsPageTests</c> both locate ".mud-avatar" at a fixed position in the DOM relative to
    /// its siblings and would fail silently in a different way if this ever regressed.
    /// </summary>
    [Fact]
    public async Task Render_RootIsExactlyOneElement()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "echo")
            .Add(p => p.Avatar, Avatar.None));

        var root = Assert.Single(cut.Nodes);
        var element = Assert.IsAssignableFrom<IElement>(root);
        Assert.Contains("mud-avatar", element.ClassList);
    }

    /// <summary>A rounded-square avatar carries <c>avatar-square</c>, for both a monogram and a picture, so the hairline follows the shape.</summary>
    /// <param name="withImage">Whether the avatar has an image.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Render_RoundedSquare_EmitsAvatarSquare(bool withImage)
    {
        await using MudBunitContext ctx = new();
        Avatar avatar = new(Label: null, Image: withImage ? "abc123.png" : null, Background: null);

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Growth")
            .Add(p => p.Avatar, avatar)
            .Add(p => p.Size, Size.Medium)
            .Add(p => p.Shape, AvatarShape.RoundedSquare));

        IElement root = cut.Find(".mud-avatar");
        Assert.Contains("avatar-square", root.ClassList);
        Assert.Equal(withImage, root.ClassList.Contains("avatar-image"));
    }

    /// <summary>The default shape is a circle: no <c>avatar-square</c> class.</summary>
    [Fact]
    public async Task Render_DefaultShape_IsACircle()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<AgentAvatar>(parameters => parameters
            .Add(p => p.Name, "Emily Lee")
            .Add(p => p.Avatar, Avatar.None));

        Assert.DoesNotContain("avatar-square", cut.Find(".mud-avatar").ClassList);
    }

    /// <summary><c>TeamAvatar</c> always draws a rounded square, and draws the monogram of the Team's name when it has no avatar.</summary>
    [Fact]
    public async Task TeamAvatar_AlwaysRoundedSquare_WithTheTeamMonogram()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeamAvatar>(parameters => parameters
            .Add(p => p.Team, "Growth"));

        IElement root = cut.Find(".mud-avatar");
        Assert.Contains("avatar-square", root.ClassList);
        Assert.Equal("GR", root.TextContent.Trim());
    }

    /// <summary><c>TeamAvatar</c> passes its label, size and class through to the one renderer.</summary>
    [Fact]
    public async Task TeamAvatar_Label_SizeAndClass_ReachTheAvatar()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<TeamAvatar>(parameters => parameters
            .Add(p => p.Team, "Growth")
            .Add(p => p.Avatar, new Avatar("QA", null, null))
            .Add(p => p.Size, Size.Small)
            .Add(p => p.Class, "feed-team-avatar"));

        IElement root = cut.Find(".mud-avatar");
        Assert.Equal("QA", root.TextContent.Trim());
        Assert.Contains("mud-avatar-small", root.ClassList);
        Assert.Contains("feed-team-avatar", root.ClassList);
    }
}
