using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui.Conventions;

/// <summary>
/// The pure scanner behind the UI convention test (plan section 3.2): one function per rule,
/// CV1 to CV11, each taking a repository-relative path and the file text and returning how many
/// violations the text holds. Razor tags are matched across line breaks, and comments
/// (<c>@* *@</c>, <c>&lt;!-- --&gt;</c>, <c>/* */</c>) are removed first.
/// </summary>
internal static partial class UiConventionScanner
{
    /// <summary>Every rule id, in order.</summary>
    public static readonly IReadOnlyList<string> Rules =
        ["CV1", "CV2", "CV3", "CV4", "CV5", "CV6", "CV7", "CV8", "CV9", "CV10", "CV11", "CV13", "CV14", "CV15"];

    private static readonly HashSet<string> SizedControls = new(StringComparer.Ordinal)
    {
        "MudButton", "MudIconButton", "MudChip", "MudToggleGroup", "MudFab",
    };

    private static readonly HashSet<string> OutlinedInputs = new(StringComparer.Ordinal)
    {
        "MudTextField", "MudSelect", "MudAutocomplete", "MudNumericField", "MudDatePicker",
    };

    private static readonly HashSet<string> ReadOnlyInputs = new(StringComparer.Ordinal)
    {
        "MudTextField", "MudSelect", "MudAutocomplete", "MudNumericField", "MudCheckBox", "MudRadioGroup",
    };

    /// <summary>Runs the rule named by <paramref name="rule"/> over one file.</summary>
    /// <param name="rule">The rule id, <c>CV1</c> to <c>CV11</c> and <c>CV13</c> to <c>CV15</c>.</param>
    /// <param name="path">Repository-relative path, forward slashes.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Scan(string rule, string path, string text) => rule switch
    {
        "CV1" => Cv1(path, text),
        "CV2" => Cv2(path, text),
        "CV3" => Cv3(path, text),
        "CV4" => Cv4(path, text),
        "CV5" => Cv5(path, text),
        "CV6" => Cv6(path, text),
        "CV7" => Cv7(path, text),
        "CV8" => Cv8(path, text),
        "CV9" => Cv9(path, text),
        "CV10" => Cv10(path, text),
        "CV11" => Cv11(path, text),
        "CV13" => Cv13(path, text),
        "CV14" => Cv14(path, text),
        "CV15" => Cv15(path, text),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown convention rule."),
    };

    /// <summary>CV1: a button, icon button, chip, toggle group or fab with no <c>Size=</c>.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv1(string path, string text) =>
        new("CV1", path, CountTags(text, SizedControls, static attrs => !SizeAttribute().IsMatch(attrs)));

    /// <summary>
    /// CV13: a raw <c>&lt;MudChip&gt;</c> in a component other than <c>Chip.razor</c>. Every chip is the one <c>Chip</c> component, so one
    /// stylesheet block per look reaches all of them (rule-reduction D12); a <c>MudChipSet</c> is not a chip and is not counted.
    /// </summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv13(string path, string text) =>
        new("CV13", path, path.EndsWith("/Chip.razor", StringComparison.Ordinal) ? 0 : CountTags(text, ["MudChip"], static _ => true));

    /// <summary>
    /// CV14: a raw <c>&lt;MudToggleGroup&gt;</c> in a component other than <c>SegmentedControl.razor</c>. Every mode switch is the one
    /// <c>SegmentedControl</c>, built from an options list, so its 28 px geometry, colour tokens and sliding indicator live in one place
    /// (ComponentCatalog B1). A <c>MudToggleItem</c> is not counted: outside the wrapper it can only sit in a counted group.
    /// </summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv14(string path, string text) =>
        new("CV14", path, path.EndsWith("/SegmentedControl.razor", StringComparison.Ordinal) ? 0 : CountTags(text, ["MudToggleGroup"], static _ => true));

    /// <summary>
    /// CV15: a raw <c>&lt;MudButton&gt;</c> in a component other than <c>ActionButton.razor</c>. Every button is the one <c>ActionButton</c>,
    /// which takes a role (Primary, Secondary, Quiet, Danger, DangerQuiet) and fixes the variant, colour and Small size from it, so the
    /// look lives in one place (ComponentCatalog B2). A <c>MudIconButton</c>, <c>MudButtonGroup</c> or <c>MudFab</c> is a different tag and
    /// is not counted. Its baseline is the migration backlog, one count per file.
    /// </summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv15(string path, string text) =>
        new("CV15", path, path.EndsWith("/ActionButton.razor", StringComparison.Ordinal) ? 0 : CountTags(text, ["MudButton"], static _ => true));

    /// <summary>CV2: an input without <c>Variant.Outlined</c> or without <c>Margin.Dense</c>.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv2(string path, string text) =>
        new("CV2", path, CountTags(
            text,
            OutlinedInputs,
            static attrs => !OutlinedVariant().IsMatch(attrs) || !DenseMargin().IsMatch(attrs)));

    /// <summary>CV3: an alert with no <c>role=</c>.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv3(string path, string text) =>
        new("CV3", path, CountTags(text, ["MudAlert"], static attrs => !RoleAttribute().IsMatch(attrs)));

    /// <summary>CV4: a <c>Snackbar.Add(</c> call with no <c>key:</c> argument before its closing paren.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv4(string path, string text)
    {
        string source = StripRazorComments(text);
        int count = 0;
        foreach (Match match in SnackbarAdd().Matches(source))
        {
            if (!HasKeyArgument(source, match.Index + match.Length))
            {
                count++;
            }
        }

        return new RuleCount("CV4", path, count);
    }

    /// <summary>CV5: <c>ReadOnly=</c> on a text field, select, autocomplete, numeric field, checkbox or radio group.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv5(string path, string text) =>
        new("CV5", path, CountTags(text, ReadOnlyInputs, static attrs => ReadOnlyAttribute().IsMatch(attrs)));

    /// <summary>CV6: <c>Typo.h1</c> to <c>Typo.h4</c>, or a raw <c>h1</c> to <c>h6</c> element.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv6(string path, string text)
    {
        string source = StripRazorComments(text);
        return new RuleCount("CV6", path, HeadingTypo().Count(source) + RawHeading().Count(source));
    }

    /// <summary>CV7: a null-forgiving <c>default</c> or <c>null</c> initialiser.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv7(string path, string text) =>
        new("CV7", path, ForgivingInitialiser().Count(StripRazorComments(text)));

    /// <summary>CV8: the banned Add and ContentPaste icons, or a button or menu label starting with <c>+ </c>.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv8(string path, string text)
    {
        string source = StripRazorComments(text);
        return new RuleCount("CV8", path, BannedIcon().Count(source) + PlusLabel().Count(source));
    }

    /// <summary>CV9: a <c>style=</c> or <c>Style=</c> whose value does not start with <c>@</c>.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv9(string path, string text) =>
        new("CV9", path, LiteralStyle().Count(StripRazorComments(text)));

    /// <summary>CV10: a <c>DispatchAsync</c> method declared in a component.</summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv10(string path, string text) =>
        new("CV10", path, DispatchAsyncDeclaration().Count(StripRazorComments(text)));

    /// <summary>
    /// CV11: in a stylesheet, a <c>border-radius</c> whose every part is not <c>0</c>, <c>50%</c> or one of the five
    /// radius steps (<c>var(--hu-radius-inner|control|card|panel|full)</c>); a literal <c>font-size</c>; a <c>padding</c>, <c>margin</c> or
    /// <c>gap</c> (including side variants) with any non-zero rem value, or a px value whose magnitude is over 2 and
    /// off the spacing scale (<see cref="SpacingScale"/>); a <c>z-index</c> above 2;
    /// each <c>!important</c>; a <c>width</c>, <c>height</c>, <c>min-width</c> or <c>min-height</c> in px that is off the size ladder
    /// (<see cref="SizeLadder"/>, <see cref="GlyphSizes"/>); and a window <c>@media</c> width that is not 600, 960 or 1099 (<see cref="WindowBreakpoints"/>).
    /// </summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv11(string path, string text)
    {
        string source = CssComments().Replace(text, string.Empty);
        int count = Important().Count(source) + SizeViolations(source);
        foreach (Match declaration in CssDeclaration().Matches(source))
        {
            string property = declaration.Groups["prop"].Value;
            string value = Important().Replace(declaration.Groups["value"].Value, string.Empty).Trim();
            if (IsViolation(property, value))
            {
                count++;
            }
        }

        return new RuleCount("CV11", path, count);
    }

    private static bool IsViolation(string property, string value)
    {
        if (string.Equals(property, "border-radius", StringComparison.Ordinal))
        {
            return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(static token => token is not ("0" or "0px" or "50%") && !RadiusStep().IsMatch(token));
        }

        if (string.Equals(property, "font-size", StringComparison.Ordinal))
        {
            return value.Length > 0 && (char.IsAsciiDigit(value[0]) || value[0] == '.');
        }

        if (string.Equals(property, "z-index", StringComparison.Ordinal))
        {
            string first = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int zIndex) && zIndex > 2;
        }

        if (RemValue().IsMatch(value))
        {
            return true;
        }

        foreach (Match px in PixelValue().Matches(value))
        {
            double magnitude = Math.Abs(double.Parse(px.Groups["n"].Value, CultureInfo.InvariantCulture));
            if (magnitude > 2 && !SpacingScale.Contains(magnitude))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The px spacing a stylesheet may use (design-system.md "Spacing"): the 4/8/12/16/24/32 scale, plus 40, 48, 64
    /// and 80 for a clearance that keeps text out from under an overlaid control. A hairline's 1px or 2px is always allowed.
    /// </summary>
    private static readonly HashSet<double> SpacingScale = [4, 8, 12, 16, 24, 32, 40, 48, 64, 80];

    /// <summary>
    /// The px sizes a control, row or mark may be between 17px and 47px (design ladder, D4): 18 for an icon or a small mark,
    /// then 20, 24, 28, 32, 40 and 44. A size below 17px (a dot, a hairline, a bar) or from 48px up (a tile, a column, a layout)
    /// is not a control size and is not checked.
    /// </summary>
    private static readonly HashSet<double> SizeLadder = [18, 20, 24, 28, 32, 40, 44];

    /// <summary>The px size of a glyph, the <c>.mud-icon-root</c> inside a button or a row: 14, 16 or 18, and 32 for an empty state.</summary>
    private static readonly HashSet<double> GlyphSizes = [14, 16, 18, 32];

    /// <summary>
    /// The window widths a stylesheet may switch layout at: MudBlazor's own 600 (sm) and 960 (md), and the shell's 1099, which
    /// its comment derives from the two sidebars' widths. Anything inside a pane uses a container query instead.
    /// </summary>
    private static readonly HashSet<double> WindowBreakpoints = [600, 960, 1099];

    /// <summary>
    /// A switch track and thumb keep a Fluent switch's own proportions, which follow the thumb and not the row ladder.
    /// </summary>
    private static readonly string[] SizeExemptSelectors = ["mud-switch-track", "mud-switch-thumb"];

    private static readonly char[] SelectorSeparators = [' ', '>', '+', '~', '\n', '\r', '\t'];

    private static int SizeViolations(string source)
    {
        int count = 0;
        foreach (Match rule in CssRule().Matches(source))
        {
            string selector = rule.Groups["sel"].Value;
            if (SizeExemptSelectors.Any(exempt => selector.Contains(exempt, StringComparison.Ordinal)))
            {
                continue;
            }

            HashSet<double> allowed = IsGlyphRule(selector) ? GlyphSizes : SizeLadder;
            foreach (Match declaration in SizeDeclaration().Matches(rule.Groups["body"].Value))
            {
                double size = double.Parse(declaration.Groups["n"].Value, CultureInfo.InvariantCulture);
                bool inBand = IsGlyphRule(selector) || (size >= 17 && size < 48);
                if (inBand && !allowed.Contains(size))
                {
                    count++;
                }
            }
        }

        foreach (Match query in WindowQuery().Matches(source))
        {
            if (!WindowBreakpoints.Contains(double.Parse(query.Groups["n"].Value, CultureInfo.InvariantCulture)))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Whether the last compound of a selector in the list is a glyph: a <c>.mud-icon-root</c> or the Feed's check mark, not a button box.</summary>
    /// <param name="selector">The selector list of one rule.</param>
    private static bool IsGlyphRule(string selector) =>
        selector.Split(',').Any(static part =>
        {
            string last = part.Split(SelectorSeparators, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            return last.Contains("icon-root", StringComparison.Ordinal) || last.Contains("feed-check", StringComparison.Ordinal);
        });

    private static int CountTags(string text, IEnumerable<string> names, Func<string, bool> isViolation)
    {
        HashSet<string> wanted = new(names, StringComparer.Ordinal);
        int count = 0;
        foreach (Match tag in MudTag().Matches(StripRazorComments(text)))
        {
            if (wanted.Contains(tag.Groups["name"].Value) && isViolation(tag.Groups["attrs"].Value))
            {
                count++;
            }
        }

        return count;
    }

    private static string StripRazorComments(string text) => RazorComments().Replace(text, string.Empty);

    private static bool HasKeyArgument(string source, int start)
    {
        StringBuilder code = new();
        int depth = 1;
        bool inString = false;
        bool escaped = false;
        foreach (char c in source.AsSpan(start))
        {
            if (depth == 0)
            {
                break;
            }

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }

            if (depth > 0)
            {
                code.Append(c);
            }
        }

        return KeyArgument().IsMatch(code.ToString());
    }

    [GeneratedRegex("""<(?<name>Mud[A-Za-z]+)(?<attrs>(?:"[^"]*"|'[^']*'|[^>"'])*)>""", RegexOptions.CultureInvariant)]
    private static partial Regex MudTag();

    [GeneratedRegex(@"@\*.*?\*@|<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex RazorComments();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CssComments();

    [GeneratedRegex(@"(?<![\w-])Size\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex SizeAttribute();

    [GeneratedRegex(@"(?<![\w-])Variant\s*=\s*""Variant\.Outlined""", RegexOptions.CultureInvariant)]
    private static partial Regex OutlinedVariant();

    [GeneratedRegex(@"(?<![\w-])Margin\s*=\s*""Margin\.Dense""", RegexOptions.CultureInvariant)]
    private static partial Regex DenseMargin();

    [GeneratedRegex(@"(?<![\w-])role\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex RoleAttribute();

    [GeneratedRegex(@"(?<![\w-])ReadOnly\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex ReadOnlyAttribute();

    [GeneratedRegex(@"[Ss]nackbar\s*\.\s*Add\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex SnackbarAdd();

    [GeneratedRegex(@"(?<![\w.])key\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex KeyArgument();

    [GeneratedRegex(@"Typo\.h[1-4]\b", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingTypo();

    [GeneratedRegex(@"<h[1-6][\s>/]", RegexOptions.CultureInvariant)]
    private static partial Regex RawHeading();

    [GeneratedRegex(@"=\s*(?:default|null)!", RegexOptions.CultureInvariant)]
    private static partial Regex ForgivingInitialiser();

    [GeneratedRegex(@"Icons\.Material\.(?:Outlined\.Add|Filled\.ContentPaste|Outlined\.ContentPaste)\b", RegexOptions.CultureInvariant)]
    private static partial Regex BannedIcon();

    [GeneratedRegex("""(?<=>)[ \t]*\+ |(?<![\w-])(?:Label|Text)\s*=\s*"\+ """, RegexOptions.CultureInvariant)]
    private static partial Regex PlusLabel();

    [GeneratedRegex("""(?<![\w-])[Ss]tyle\s*=\s*"(?!@)""", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralStyle();

    [GeneratedRegex(@"\b(?:Task|ValueTask|void)(?:<[^>\s]*>)?\s+DispatchAsync\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex DispatchAsyncDeclaration();

    [GeneratedRegex(@"^var\(--hu-radius-(?:inner|control|card|panel|full)\)$", RegexOptions.CultureInvariant)]
    private static partial Regex RadiusStep();

    [GeneratedRegex(@"(?<sel>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex CssRule();

    [GeneratedRegex(@"(?<![\w-])(?:min-)?(?:width|height)\s*:\s*(?<n>\d*\.?\d+)px\s*(?:!\s*important\s*)?(?:;|$)", RegexOptions.CultureInvariant)]
    private static partial Regex SizeDeclaration();

    [GeneratedRegex(@"@media[^{]*\((?:min|max)-width\s*:\s*(?<n>\d*\.?\d+)px\)", RegexOptions.CultureInvariant)]
    private static partial Regex WindowQuery();

    [GeneratedRegex(@"(?<![\w-])(?<prop>border-radius|font-size|z-index|(?:padding|margin)(?:-(?:top|right|bottom|left|inline|block)(?:-(?:start|end))?)?|gap|row-gap|column-gap)\s*:\s*(?<value>[^;{}]*)", RegexOptions.CultureInvariant)]
    private static partial Regex CssDeclaration();

    [GeneratedRegex(@"!\s*important", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Important();

    [GeneratedRegex(@"(?<n>-?\d*\.?\d+)px", RegexOptions.CultureInvariant)]
    private static partial Regex PixelValue();

    /// <summary>A non-zero rem length: spacing in rem is the old 14px-root scale, which the px scale replaced.</summary>
    [GeneratedRegex(@"(?<![\w.])(?!0*\.?0+rem)\d*\.?\d+rem\b", RegexOptions.CultureInvariant)]
    private static partial Regex RemValue();
}
