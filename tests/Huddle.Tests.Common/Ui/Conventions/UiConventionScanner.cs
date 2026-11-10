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
        ["CV1", "CV2", "CV3", "CV4", "CV5", "CV6", "CV7", "CV8", "CV9", "CV10", "CV11", "CV13"];

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
    /// <param name="rule">The rule id, <c>CV1</c> to <c>CV11</c> and <c>CV13</c>.</param>
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
    /// CV11: in a stylesheet, a <c>border-radius</c> that is not <c>0</c>, <c>50%</c>, <c>999px</c> or built from
    /// <c>var(--mud-default-borderradius)</c> or a skin radius token such as <c>var(--hu-feed-card-radius)</c>; a literal <c>font-size</c>; a <c>padding</c>, <c>margin</c> or
    /// <c>gap</c> (including side variants) with any non-zero rem value, or a px value whose magnitude is over 2 and
    /// off the spacing scale (<see cref="SpacingScale"/>); a <c>z-index</c> above 2;
    /// each <c>!important</c>.
    /// </summary>
    /// <param name="path">Repository-relative path.</param>
    /// <param name="text">The file text.</param>
    public static RuleCount Cv11(string path, string text)
    {
        string source = CssComments().Replace(text, string.Empty);
        int count = Important().Count(source);
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
            return !value.Contains("var(--mud-default-borderradius)", StringComparison.Ordinal)
                && !HuRadiusToken().IsMatch(value)
                && value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(static token => token is not ("0" or "0px" or "50%" or "999px"));
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

    [GeneratedRegex(@"var\(--hu-[a-z-]*radius\)", RegexOptions.CultureInvariant)]
    private static partial Regex HuRadiusToken();

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
