using System.Text.RegularExpressions;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins the last round-3 open items (UX-E14 to UX-E16 and the avatar editor's B8i): the Move dialog names a Teammate
/// folder by the Teammate, the New teammate form is sized to fit 1440x900, the role and roster scrollbars hide until
/// hover, and the avatar editor reserves its tallest mode's height with swatches that have distinct names.
/// </summary>
public sealed partial class R3OpenItemsTests
{
    private static string ReadCss() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    private static string ReadSource(params string[] segments) => File.ReadAllText(CssSource.RepoPath(segments));

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>UX-E14: the Move dialog's folder list names a Teammate folder through the Persona store, and the tooltip still carries the path.</summary>
    [Fact]
    public void MoveDialog_NamesATeammateFolderByTheTeammate()
    {
        string ops = ReadSource("src", "Huddle.App", "Components", "Library", "LibraryFileOps.razor");
        string dialog = ReadSource("src", "Huddle.App", "Components", "Library", "MoveDialog.razor");

        Assert.Contains("entry.Path.Role == LibraryNodeRole.TeammateFolder ? this.TeammateDisplayName(name) : name", ops, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("ResolveByNameOrAlias(folderName)", ops, StringComparison.Ordinal); // contains-ok: component source text
        Assert.Contains("title=\"@folder.Path.RelativePath\"", dialog, StringComparison.Ordinal); // contains-ok: component source text
    }

    /// <summary>UX-E15: the New teammate form's Persona text is four lines and its fields sit 12px apart, so the dialog fits a 900px-high window.</summary>
    [Fact]
    public void TeammateCard_PersonaTextIsFourLines_AndFieldsUseTheTighterGap()
    {
        string card = ReadSource("src", "Huddle.App", "Components", "Shared", "TeammateCard.razor");
        int form = card.IndexOf("Label=\"Persona text\"", StringComparison.Ordinal);
        string tail = card[form..];

        Assert.Contains("Lines=\"4\"", card[(form - 200)..(form + 400)], StringComparison.Ordinal); // contains-ok: component source text
        Assert.DoesNotContain("Class=\"mb-4", tail[..tail.IndexOf("@code", StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    /// <summary>UX-E16: the role and roster grids use a thin scrollbar that is transparent at rest and takes the Edge rung on hover or focus-within.</summary>
    [Fact]
    public void RoleAndRosterGrids_HideTheScrollbarUntilHoverOrFocus()
    {
        string css = Whitespace().Replace(ReadCss(), " ");

        Assert.Contains("scrollbar-width: thin; scrollbar-color: transparent transparent; }", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
        Assert.Contains(".role-grid:hover, .role-grid:focus-within, .roster-grid:hover, .roster-grid:focus-within { scrollbar-color: var(--ink-edge) transparent; }", css, StringComparison.Ordinal); // contains-ok: stylesheet source text
    }

    /// <summary>B8i: the avatar editor reserves 26rem so Initials, A short label and An image measure the same (the label mode's content is about 415px).</summary>
    [Fact]
    public void AvatarEditor_ReservesTheTallestModesHeight()
    {
        Match rule = AvatarEditorRule().Match(ReadCss());

        Assert.True(rule.Success);
        Assert.True(double.Parse(rule.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 26);
    }

    [GeneratedRegex(@"\.avatar-editor \{[^}]*?min-height: ([0-9.]+)rem;", RegexOptions.CultureInvariant)]
    private static partial Regex AvatarEditorRule();

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
