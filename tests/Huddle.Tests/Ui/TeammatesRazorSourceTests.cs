using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Guards a trap that nothing else in this suite can see. A Razor component attribute bound
/// without a leading <c>@</c> is parsed as a plain string literal whenever the target
/// <c>[Parameter]</c> is <c>string</c>/<c>string?</c> — and a literal is legal wherever a string
/// is expected, so the mistake compiles clean and warning-free. It shipped once already: see the
/// comment above <c>&lt;TeammateCard ... /&gt;</c> in Teammates.razor for the story.
///
/// No rendering layer available to this suite can catch it either. <see cref="TeammateCardTests"/>
/// renders <c>TeammateCard</c> directly from a parameter dictionary, so it never sees how
/// Teammates.razor's markup passes those parameters. <see cref="TeammatesPageTests"/> only ever
/// GETs the page's prerender, and the card opens on a click under <c>InteractiveServer</c>, so it
/// never renders there either. <c>HtmlRenderer</c> (used by both) exposes no supported way to
/// dispatch that click and see the card's markup — its public surface is rendering and
/// <c>ToHtmlString()</c> only, nothing that reaches a rendered element's event handler. So a plain
/// source assertion over the .razor file's text is the only guard this suite has left.
/// </summary>
public sealed class TeammatesRazorSourceTests
{
    // Every TeammateCard [Parameter] typed string or string? — the ones a missing @ silently
    // turns into a literal instead of a compile error. Keep this in sync with TeammateCard.razor.
    private static readonly string[] StringTypedParameters = ["Name", "Text", "Model", "Effort", "RoomId", "FilePath", "Error"];

    [Fact]
    public void TeammateCardUsage_BindsEveryStringParameterWithAnAtSign()
    {
        var source = File.ReadAllText(FindTeammatesRazorPath());

        // Non-greedy up to the first "/>" so this survives the tag being reformatted or
        // re-wrapped, so long as it stays a single self-closing element.
        var tagMatch = Regex.Match(source, "<TeammateCard\\b.*?/>", RegexOptions.Singleline);
        Assert.True(tagMatch.Success, "Could not find the <TeammateCard ... /> element in Teammates.razor.");

        var tag = tagMatch.Value;

        foreach (var parameter in StringTypedParameters)
        {
            // \b on both sides so "Model" doesn't match inside "ModelChanged" or "AvailableModels".
            var attributeMatch = Regex.Match(tag, $"\\b{parameter}=\"([^\"]*)\"");
            if (!attributeMatch.Success)
            {
                continue; // Not bound in this usage; nothing to check.
            }

            var value = attributeMatch.Groups[1].Value;
            Assert.True(
                value.StartsWith('@'),
                $"TeammateCard's '{parameter}' attribute is bound to \"{value}\" without a leading '@', " +
                "so Razor passes it as a literal string rather than the field's value.");
        }
    }

    private static string FindTeammatesRazorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                $"Could not locate Huddle.slnx by walking up from '{AppContext.BaseDirectory}'.");
        }

        return Path.Combine(directory.FullName, "src", "Huddle.App", "Components", "Pages", "Teammates.razor");
    }
}