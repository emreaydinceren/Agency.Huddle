using System.Text.RegularExpressions;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the last round-3 open items (UX-E14 to UX-E16 and the avatar editor's B8i): the Move dialog names an Agent
/// folder by the Agent, the New agent form is sized to fit 1440x900, the role and roster scrollbars hide until
/// hover, and the avatar editor reserves its tallest mode's height with swatches that have distinct names.
/// </summary>
public sealed partial class R3OpenItemsTests
{
    private static string ReadSource(params string[] segments) => File.ReadAllText(CssSource.RepoPath(segments));

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>UX-E14: the Move dialog's folder list names an Agent folder through the Persona store, and the tooltip still carries the path.</summary>
    [Fact]
    public void MoveDialog_NamesAAgentFolderByTheAgent()
    {
        string ops = ReadSource("src", "Huddle.App", "Components", "Library", "LibraryFileOps.razor");
        string dialog = ReadSource("src", "Huddle.App", "Components", "Library", "MoveDialog.razor");

        Assert.Contains("entry.Path.Role == LibraryNodeRole.AgentFolder ? this.AgentDisplayName(name) : name", ops, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("ResolveByNameOrAlias(folderName)", ops, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("title=\"@folder.Path.RelativePath\"", dialog, StringComparison.Ordinal); // contains-ok: component source text
    }

    /// <summary>UX-E15: the New agent form's Persona text is four lines and its fields sit 12px apart, so the dialog fits a 900px-high window.</summary>
    [Fact]
    public void AgentProfileCard_PersonaTextIsFourLines_AndFieldsUseTheTighterGap()
    {
        string card = ReadSource("src", "Huddle.App", "Components", "Shared", "AgentProfileCard.razor");
        int form = card.IndexOf("Label=\"Persona text\"", StringComparison.Ordinal);
        string tail = card[form..];

        Assert.Contains("Lines=\"4\"", card[(form - 200)..(form + 400)], StringComparison.Ordinal); // contains-ok: component source text
        Assert.DoesNotContain("Class=\"mb-4", tail[..tail.IndexOf("@code", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    /// <summary>UX-E15: the three-up row's selects carry no helper text (it wrapped to 4-5 lines), one shared line sits under the row, and the default option reads in full.</summary>
    [Fact]
    public void AgentProfileCard_ModelRow_HasOneSharedHelperAndAShortDefault()
    {
        string card = ReadSource("src", "Huddle.App", "Components", "Shared", "AgentProfileCard.razor");
        int start = card.IndexOf("agent-profile-card-model-row", StringComparison.Ordinal);
        int end = card.IndexOf("agent-profile-card-row-helper", StringComparison.Ordinal);
        string row = card[start..end];

        Assert.True(end > start);
        Assert.DoesNotContain("HelperText=", row, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(row, "@DefaultOptionText", RegexOptions.CultureInvariant));
        Assert.Contains("DefaultOptionText = \"Agent default\"", card, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("Changing any of these restarts the agent, which clears what it remembers.", card, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("HelperText=\"Shown beside the name.\"", card, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("HelperText=\"For @@mentions.\"", card, StringComparison.Ordinal); // contains-ok: component source text
    }

    /// <summary>UX-E15: the row container is a flex row whose cells share the width, and the card puts Model, Effort and Work mode in it.</summary>
    [Fact]
    public void AgentProfileCard_ModelEffortWorkModeAreOneRow()
    {
        string card = ReadSource("src", "Huddle.App", "Components", "Shared", "AgentProfileCard.razor");
        string css = Whitespace().Replace(CssText.AppCss(CssNewlines.AsIs), " ");
        int row = card.IndexOf("agent-profile-card-model-row", StringComparison.Ordinal);
        string rowTail = card[row..];
        string rowBody = rowTail[..rowTail.IndexOf("The reset below is deliberate", StringComparison.Ordinal)];

        Assert.Contains("Label=\"Model\"", rowBody, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("Label=\"Effort\"", rowBody, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("Label=\"Work mode\"", rowBody, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains(".agent-profile-card-row { display: flex;", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>UX-E16: the role and roster grids use a thin scrollbar that is transparent at rest and takes the Edge rung on hover or focus-within.</summary>
    [Fact]
    public void RoleAndRosterGrids_HideTheScrollbarUntilHoverOrFocus()
    {
        string css = Whitespace().Replace(CssText.AppCss(CssNewlines.AsIs), " ");

        Assert.Contains("scrollbar-width: thin; scrollbar-color: transparent transparent; }", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains(".role-grid:hover, .role-grid:focus-within, .roster-grid:hover, .roster-grid:focus-within { scrollbar-color: var(--ink-edge) transparent; }", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>B8i: every swatch has its own colour name, none is a numbered repeat ("Red 2"), and all 30 are different.</summary>
    [Fact]
    public void Swatches_HaveDistinctColourNames()
    {
        string[] names = [.. AvatarPalette.Swatches.Select(static swatch => swatch.Name)];

        Assert.Equal(AvatarPalette.Colours.Count, names.Length);
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(names, static name => name.Any(char.IsDigit));
    }
}
