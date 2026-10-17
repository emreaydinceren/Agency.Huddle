using System.Text.RegularExpressions;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Ui.Teams;

/// <summary>
/// Round 3 of the visual audit for the Teammates tiles, the Team page, its dialogs and the avatar pickers: the rules that
/// live in the stylesheet and in razor source (a render cannot tell a 12px padding or a monospace block from a default).
/// </summary>
public sealed class R3TeamsSourceTests
{
    /// <summary>The body of the first rule in <paramref name="css"/> whose selector is exactly <paramref name="selector"/>, whitespace collapsed.</summary>
    private static string RuleBody(string css, string selector)
    {
        Match rule = Regex.Match(css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        Assert.True(rule.Success, $"app.css has no '{selector}' rule.");
        return Regex.Replace(rule.Groups["body"].Value.Trim(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static string Css() => File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

    private static string Source(params string[] path) => File.ReadAllText(CssSource.RepoPath(["src", "Huddle.App", .. path]));

    /// <summary>A Teammates tile has 12px padding, as the Board card does, not 16px (B7a, design-system "Teammates").</summary>
    [Fact]
    public void Tile_HasTwelvePixelPadding()
    {
        string tile = RuleBody(Css(), ".teammate-tile");

        Assert.Contains("padding: 12px;", tile, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>The Persona block is code, so it is monospace at 13px on the wash with an Edge line (B8g).</summary>
    [Fact]
    public void PersonaBlock_IsMonospaceAtThirteenPixelsOnTheWashWithAnEdge()
    {
        string persona = RuleBody(Css(), ".teammate-card-persona");

        Assert.Contains("font-family: var(--font-mono);", persona, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("font-size: var(--mud-typography-body2-size);", persona, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("background: var(--ink-wash);", persona, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("border: 1px solid var(--ink-edge);", persona, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>The avatar editor reserves the height of its tallest mode, so the dialog does not change height between modes (B8i).</summary>
    [Fact]
    public void AvatarEditor_ReservesTheTallestModesHeight()
    {
        string editor = RuleBody(Css(), ".avatar-editor");

        Assert.Matches(@"min-height: \d+(\.\d+)?rem;", editor);
    }

    /// <summary>A swatch is a 32px circle drawn with the avatar recipe: the colour at 20% over Surface, 35% in a Dark Theme (B8h).</summary>
    [Fact]
    public void Swatch_IsA32pxCircleDrawnWithTheAvatarRecipeForTheMode()
    {
        string css = Css();
        string dot = RuleBody(css, ".avatar-swatch-dot");
        string dark = RuleBody(css, ":root:has(.theme-mode-dark) .avatar-swatch-dot");

        Assert.Contains("width: 32px;", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("height: 32px;", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("border-radius: 50%;", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("var(--mud-palette-primary) 20%, var(--mud-palette-surface)", dot, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("var(--mud-palette-primary) 35%, var(--mud-palette-surface)", dark, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>The swatch radios are visually hidden, never <c>display: none</c>, so the group stays keyboard-reachable (B8h).</summary>
    [Fact]
    public void Swatch_RadioIsVisuallyHidden_NotRemovedFromTheTabOrder()
    {
        string editor = Source("Components", "Shared", "AvatarEditor.razor");

        Assert.Contains("class=\"avatar-swatch-input visually-hidden\"", editor, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.DoesNotContain("tabindex", editor, StringComparison.Ordinal);
        Assert.Contains(".avatar-swatch-input:focus-visible + .avatar-swatch-dot", Css(), StringComparison.Ordinal); // contains-ok: haystack is the stylesheet source
    }

    /// <summary>Every palette colour has a colour name a person can tell from the others, never a hex code (B8h).</summary>
    [Fact]
    public void Palette_EverySwatchHasAUniqueColourName()
    {
        IReadOnlyList<AvatarSwatch> swatches = AvatarPalette.Swatches;

        Assert.Equal(AvatarPalette.Colours.Count, swatches.Count);
        Assert.Equal(swatches.Count, swatches.Select(static swatch => swatch.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(swatches, static swatch => Assert.DoesNotContain('#', swatch.Name));
        Assert.All(swatches, static swatch => Assert.Matches("^#[0-9a-f]{6}$", swatch.Hex));
    }

    /// <summary>A skin-tone chip is the Filter chip: 28px, dashed while unset, Accent border and 14% tint when set, and not weight 600 (B8j, A6).</summary>
    [Fact]
    public void SkinChip_IsTheFilterChip()
    {
        string css = Css();
        string unset = RuleBody(css, ".avatar-library-skin");
        string set = RuleBody(css, ".avatar-library-skin[aria-pressed=\"true\"]");

        Assert.Contains("height: 28px;", unset, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("border: 1px dashed var(--ink-field);", unset, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("border: 1px solid var(--mud-palette-primary);", set, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.Contains("var(--mud-palette-primary) 14%", set, StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
        Assert.DoesNotContain("font-weight: 600", set, StringComparison.Ordinal);
    }

    /// <summary>A list being read keeps its normal ink and border (the disabled colours are put back) so the field reads as busy, not off (E5).</summary>
    [Fact]
    public void ProbingField_KeepsItsNormalInkAndBorder()
    {
        string css = Css();

        Assert.Equal("color: var(--mud-palette-text-primary);", RuleBody(css, ".mud-input-control.teammate-card-probing .mud-input.mud-disabled"));
        Assert.Equal("border-color: var(--mud-palette-lines-default);", RuleBody(css, ".mud-input-control.teammate-card-probing .mud-input.mud-input-outlined.mud-disabled .mud-input-outlined-border"));
    }

    /// <summary>"Roles" starts 24px under the tab rail's Chrome line: the global panel padding applies and the Team page's wide layout does not override it (B7e).</summary>
    [Fact]
    public void TeamPage_ContentStartsTwentyFourPixelsUnderTheRail()
    {
        string css = Css();
        string rail = RuleBody(css, ".mud-tabs:not(.mud-tabs-vertical) > .mud-tabs-panels");
        string roster = RuleBody(css, ".team-page-wide .team-tasks-tab,\r\n.team-page-wide .team-roster,\r\n.team-page-wide .team-details");

        Assert.Equal("padding-top: 24px;", rail);
        Assert.DoesNotContain("padding-top", roster, StringComparison.Ordinal);
    }

    /// <summary>The roster's destructive item is last, after a divider, in Error ink; there is no "Remove" menu button with a caret (B7c, design-system "Menus").</summary>
    [Fact]
    public void Roster_RemovalSitsUnderTheMoreActionsMenu()
    {
        string buildTeam = Source("Components", "Pages", "BuildTeam.razor");

        Assert.DoesNotContain("EndIcon=\"@Icons.Material.Filled.ArrowDropDown\"", buildTeam, StringComparison.Ordinal);
        Assert.Contains("MenuDialogs.ActivatorLabel(draft.Name)", buildTeam, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.True(
            buildTeam.IndexOf("roster-leave", StringComparison.Ordinal) < buildTeam.IndexOf("<MudDivider />", StringComparison.Ordinal)
            && buildTeam.IndexOf("<MudDivider />", StringComparison.Ordinal) < buildTeam.IndexOf("roster-delete", StringComparison.Ordinal),
            "Delete teammate must come last, after a divider.");
        Assert.Contains("color: var(--mud-palette-error);", RuleBody(Css(), ".mud-menu-item.roster-delete,\r\n.mud-menu-item.roster-delete .mud-icon-root"), StringComparison.Ordinal); // contains-ok: haystack is one rule of the stylesheet source
    }

    /// <summary>Add a role marks optional fields "(optional)", has no asterisk, gives every field a helper line, and its primary repeats the title's verb (B10k).</summary>
    [Fact]
    public void RoleForm_MarksOptionalFields_NoAsterisk_AndEndsInAdd()
    {
        string form = Source("Components", "Teams", "RoleFormDialog.razor");

        Assert.DoesNotContain("Required=\"true\"", form, StringComparison.Ordinal);
        Assert.Contains("Label=\"What it does (optional)\"", form, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("Label=\"Instructions (optional)\"", form, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("Label=\"Skills (optional)\"", form, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Equal(5, Regex.Count(form, "HelperText=", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)));
        Assert.Contains("this.Id is null ? \"Add\" : \"Save\"", form, StringComparison.Ordinal); // contains-ok: haystack is the razor source
    }

    /// <summary>Duplicate names the Team in its title, offers Browse through the shared folder picker, and keeps the primary disabled until the name is valid (B10 Duplicate Team).</summary>
    [Fact]
    public void DuplicateDialog_NamesTheTeam_BrowsesAndDisablesTheEmptyPrimary()
    {
        string dialog = Source("Components", "Teams", "DuplicateTeamDialog.razor");
        string page = Source("Components", "Pages", "TeamPage.razor");

        Assert.Contains("ShowAsync<FolderPickerDialog>", dialog, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("duplicate-team-browse", dialog, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("Disabled=\"@(string.IsNullOrWhiteSpace(this.title))\"", dialog, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("ShowAsync<DuplicateTeamDialog>($\"Duplicate {team}\"", page, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.DoesNotContain("Duplicate Team\"", page, StringComparison.Ordinal);
    }

    /// <summary>The avatar editor opens the library in place when it is inside a dialog, and the library is one component the library dialog and the editor share (B8k).</summary>
    [Fact]
    public void AvatarLibrary_IsOneComponent_HostedByTheDialogOrByTheEditor()
    {
        string editor = Source("Components", "Shared", "AvatarEditor.razor");
        string dialog = Source("Components", "Shared", "AvatarLibraryDialog.razor");

        Assert.Contains("<AvatarLibraryPicker", editor, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("<AvatarLibraryPicker", dialog, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.Contains("SetOptionsAsync(AvatarLibraryDialog.InlineOptions)", editor, StringComparison.Ordinal); // contains-ok: haystack is the razor source
        Assert.False(AvatarLibraryDialogOptions().BackdropClick ?? true, "the in-place library must not be dismissed by a backdrop click");
    }

    private static MudBlazor.DialogOptions AvatarLibraryDialogOptions()
    {
        System.Reflection.FieldInfo? field = typeof(Agency.Huddle.App.Components.Shared.AvatarLibraryDialog)
            .GetField("InlineOptions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<MudBlazor.DialogOptions>(field.GetValue(null));
    }
}
