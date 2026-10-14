using Bunit;
using Microsoft.AspNetCore.Components;
using Agency.Huddle.App.Components.Settings;

namespace Agency.Huddle.Tests.Ui;

/// <summary>Tests for <see cref="SettingsIntro"/>, the paragraph that opens every Settings panel (UX-012, UX-R17).</summary>
public sealed class SettingsIntroTests
{
    /// <summary>The intro is one paragraph with the <c>settings-intro</c> class (the only CSS hook for the panel intros) holding the caller's text.</summary>
    [Fact]
    public async Task Renders_OneParagraphWithTheText()
    {
        await using MudBunitContext ctx = new();

        var cut = ctx.Render<SettingsIntro>(parameters => parameters
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "A skill is a folder."))));

        var intro = cut.Find("p.settings-intro");
        Assert.Equal("A skill is a folder.", intro.TextContent.Trim());
    }
}
