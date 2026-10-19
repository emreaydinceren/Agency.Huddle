using System.Text.RegularExpressions;
using Agency.Huddle.Tests.Css;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Source assertions for the three shared-skin fixes of the theming plan (section 7.2 to 7.4): the Composer's Send
/// button is coloured by a class and two tokens rather than by MudBlazor's <c>!important</c> colour utility, the
/// avatar cut-out ring is a token the base rules read, and no selector in <c>app.css</c> names a
/// class nothing emits. A source test, for the reason <see cref="ThemeSourceTests"/> gives: no browser renders here.
/// </summary>
public sealed partial class ThemeFixSourceTests
{
    /// <summary>
    /// Classes MudBlazor itself emits, which no file under <c>src/Huddle.App</c> spells: any class starting with one of
    /// these prefixes is exempt from <see cref="Css_SelectorClasses_AreEmittedBySomeSource"/>.
    /// </summary>
    private static readonly string[] MudBlazorPrefixes = ["mud-", "mdi-"];

    /// <summary>
    /// Classes built by string interpolation, so only their prefix appears in an emitter. Each prefix names that emitter.
    /// </summary>
    private static readonly string[] InterpolatedPrefixes =
    [
        "task-ref-status-", // Services/MarkdownRenderer.cs: $"task-ref-status-{colour}" from TaskColors.For
        "presence-glyph-", // Components/Shared/PresenceBadge.razor and Components/Tasks/TaskAssignee.razor: glyph name
        "task-state-dot-", // Components/Sidebar/RoomTasksPanel.razor and Components/Tasks/TaskStateDot.razor: TaskColors.For
        "task-card-field-", // Components/Tasks/TaskCard.razor: one class per field name
        "library-explorer-split-", // Components/Library/LibraryExplorer.razor: the layout slug (side-by-side, stacked)
        "roster-tier-", // Components/Shared/ProfileCard.razor: the tier name
        "status-chip-", // Components/Shared/StatusChip.razor (colour) and Components/Tasks/TaskStatusChip.razor (state)
        "priority-", // Components/Tasks/TaskPriorityChip.razor: the priority level
    ];

    /// <summary>
    /// Classes that are not MudBlazor-prefixed but are still not spelled in this project: MudBlazor's own split panel
    /// and data grid emit them (<c>child-panel</c> from MudSplitPanel, the two header classes from MudDataGrid).
    /// </summary>
    private static readonly string[] MudBlazorOwnClasses = ["child-panel", "column-header", "sortable-column-header"];

    /// <summary>
    /// Shared utility classes that <c>docs/engineering/design-system.md</c> and <c>UXDesignGuidelines.md</c> document,
    /// existing tests pin, and no component uses today. They are kept as the catalogue's reserve, not as live rules:
    /// removing one is a design decision with its own test and page edits (reported by the p0a package).
    /// </summary>
    private static readonly string[] DocumentedReserveClasses = ["page-lead", "drop-target", "ref-link", "room-strip", "library-empty-pane"];

    /// <summary>The Composer's Send button carries no MudBlazor <c>Color</c> (its <c>!important</c> utility blocks a Theme) and has its own class.</summary>
    [Fact]
    public void Composer_SendButton_HasNoColorAndCarriesItsClass()
    {
        string razor = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "Components", "Shared", "Composer.razor"));

        Match button = SendButtonElement().Match(razor);

        Assert.True(button.Success, "Composer.razor has no MudIconButton with the Send icon.");
        Assert.DoesNotContain("Color=", button.Value, StringComparison.Ordinal);
        Assert.Contains("Class=\"composer-send-button\"", button.Value, StringComparison.Ordinal);
    }

    /// <summary><c>app.css</c> colours the Send button from <c>--hu-send-fg</c> and <c>--hu-send-bg</c>, and <c>app-vars.css</c> declares today's look as their defaults.</summary>
    [Fact]
    public void SendButton_IsColouredFromTokens_WithDefaultsInAppVars()
    {
        string body = RuleBody(CssText.StripComments(CssText.AppCss(CssNewlines.AsIs)), ".composer-send-button");
        string vars = File.ReadAllText(RepoPaths.App("wwwroot", "app-vars.css"));

        Assert.Matches(@"(?<![-\w])color:\s*var\(--hu-send-fg\)", body);
        Assert.Matches(@"(?<![-\w])background:\s*var\(--hu-send-bg\)", body);
        Assert.Matches(@"--hu-send-fg:\s*var\(--mud-palette-primary\);", vars);
        Assert.Matches(@"--hu-send-bg:\s*transparent;", vars);
    }

    /// <summary>The base rules for the presence dot read the ring from <c>--hu-ring</c>, and <c>app-vars.css</c> defaults it to Surface.</summary>
    [Fact]
    public void PresenceRing_BaseRules_ReadTheRingToken()
    {
        string css = CssText.StripComments(CssText.AppCss(CssNewlines.AsIs));
        string vars = File.ReadAllText(RepoPaths.App("wwwroot", "app-vars.css"));

        string dot = RuleBody(css, ".agent-dot.on-avatar");
        string glyph = RuleBody(css, ".agent-dot.on-avatar.presence-glyph-ring");

        Assert.Contains("box-shadow: 0 0 0 2px var(--hu-ring);", dot, StringComparison.Ordinal);
        Assert.Contains("inset 0 0 0 2px var(--mud-palette-text-secondary), 0 0 0 2px var(--hu-ring);", glyph, StringComparison.Ordinal);
        Assert.Matches(@"--hu-ring:\s*var\(--mud-palette-surface\);", vars);
    }

    /// <summary>A Task reference is one <c>a.task-ref</c> link, so <c>app.css</c> sets its mono face and caption size on the link itself, for every Theme.</summary>
    [Fact]
    public void TaskRef_IsStyledOnTheLinkItself()
    {
        string body = RuleBody(CssText.StripComments(CssText.AppCss(CssNewlines.AsIs)), ".task-ref");

        Assert.Contains("font-family: var(--font-mono);", body, StringComparison.Ordinal);
        Assert.Contains("font-size: var(--mud-typography-caption-size);", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every class used in a selector of <c>app.css</c> is emitted by some <c>.razor</c>, <c>.cs</c> or
    /// <c>.js</c> file under <c>src/Huddle.App</c>, apart from MudBlazor's own classes and the interpolated prefixes
    /// listed above. A selector that names a class nothing emits matches nothing in any Theme, which is how
    /// <c>.task-ref .task-id</c> once sat in a stylesheet unnoticed.
    /// </summary>
    [Fact]
    public void Css_SelectorClasses_AreEmittedBySomeSource()
    {
        HashSet<string> emitted = EmittedTokens();
        List<string> dead = [];

        foreach (string path in new[] { RepoPaths.App("wwwroot", "app.css") })
        {
            foreach (string selector in Selectors(File.ReadAllText(path)))
            {
                foreach (Match match in ClassName().Matches(selector))
                {
                    string name = match.Groups["name"].Value;
                    if (!IsExempt(name) && !emitted.Contains(name))
                    {
                        dead.Add($"{Path.GetFileName(path)} .{name}");
                    }
                }
            }
        }

        Assert.Equal(string.Empty, string.Join("; ", dead.Distinct(StringComparer.Ordinal)));
    }

    /// <summary>Whether <paramref name="name"/> is a MudBlazor class or an interpolated one (see the lists above).</summary>
    /// <param name="name">A class name without its dot.</param>
    private static bool IsExempt(string name) =>
        MudBlazorOwnClasses.Contains(name, StringComparer.Ordinal)
        || DocumentedReserveClasses.Contains(name, StringComparer.Ordinal)
        || MudBlazorPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))
        || InterpolatedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Every identifier-shaped word in every emitter source file under <c>src/Huddle.App</c>.</summary>
    private static HashSet<string> EmittedTokens()
    {
        string root = CssSource.RepoPath("src", "Huddle.App");
        HashSet<string> tokens = new(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path);
            bool excluded = relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relative.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar, StringComparison.Ordinal);
            string extension = Path.GetExtension(path);
            bool emitter = string.Equals(extension, ".razor", StringComparison.Ordinal)
                || string.Equals(extension, ".cs", StringComparison.Ordinal)
                || string.Equals(extension, ".js", StringComparison.Ordinal);
            if (excluded || !emitter)
            {
                continue;
            }

            foreach (Match word in Word().Matches(File.ReadAllText(path)))
            {
                tokens.Add(word.Value);
            }
        }

        return tokens;
    }

    /// <summary>The selector text of every style rule in <paramref name="css"/>, at-rule preludes excluded.</summary>
    /// <param name="css">Stylesheet source.</param>
    private static IEnumerable<string> Selectors(string css)
    {
        string stripped = AttributeSelector().Replace(CssString().Replace(CssText.StripComments(css), "\"\""), "[]");
        int start = 0;
        for (int index = 0; index < stripped.Length; index++)
        {
            char c = stripped[index];
            if (c == '{')
            {
                string prelude = stripped[start..index].Trim();
                if (prelude.Length > 0 && prelude[0] != '@')
                {
                    yield return prelude;
                }

                start = index + 1;
            }
            else if (c is '}' or ';')
            {
                start = index + 1;
            }
        }
    }

    /// <summary>The declaration blocks of every rule whose selector list contains <paramref name="selector"/>, joined; fails the test if there is none.</summary>
    /// <param name="css">Stylesheet source with comments stripped.</param>
    /// <param name="selector">The selector list as written.</param>
    private static string RuleBody(string css, string selector)
    {
        List<string> bodies = [];
        foreach (Match rule in PackRule().Matches(css))
        {
            string[] parts = rule.Groups["selectors"].Value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Contains(selector, StringComparer.Ordinal))
            {
                bodies.Add(rule.Groups["body"].Value);
            }
        }

        Assert.NotEmpty(bodies);
        return string.Join('\n', bodies);
    }

    [GeneratedRegex(@"<MudIconButton\b[^>]*Icons\.Material\.Filled\.Send[^>]*/>", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex SendButtonElement();

    [GeneratedRegex(@"(?<selectors>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PackRule();

    [GeneratedRegex("\"[^\"]*\"", RegexOptions.CultureInvariant)]
    private static partial Regex CssString();

    [GeneratedRegex(@"\[[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeSelector();

    [GeneratedRegex(@"\.(?<name>-?[_a-zA-Z][\w-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ClassName();

    [GeneratedRegex(@"[A-Za-z_][\w-]*", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
