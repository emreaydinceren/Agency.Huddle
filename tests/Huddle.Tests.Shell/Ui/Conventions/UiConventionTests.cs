namespace Agency.Huddle.Tests.Ui.Conventions;

/// <summary>
/// The UI convention test (plan section 3.2): scans <c>src/Huddle.App</c> for the rules CV1 to CV11, CV13 to CV18 and CV23
/// and holds each file to its baseline, which may only shrink. Also proves, against a copy of one
/// real file per rule, that the scanner and the baseline check can fail.
/// </summary>
public sealed class UiConventionTests
{
    private static readonly Lazy<IReadOnlyList<RuleCount>> Scanned = new(UiConventionSource.ScanRepo);

    /// <summary>The <c>Baseline</c> folder beside this source file.</summary>
    /// <returns>The folder path.</returns>
    private static string BaselineDirectory() => RepoPaths.Data("Baseline");

    /// <summary>A file never has more violations of a rule than its baseline allows, and none unlisted.</summary>
    /// <param name="rule">The rule id.</param>
    [Theory]
    [InlineData("CV1")]
    [InlineData("CV2")]
    [InlineData("CV3")]
    [InlineData("CV4")]
    [InlineData("CV5")]
    [InlineData("CV6")]
    [InlineData("CV7")]
    [InlineData("CV8")]
    [InlineData("CV9")]
    [InlineData("CV10")]
    [InlineData("CV11")]
    [InlineData("CV13")]
    [InlineData("CV14")]
    [InlineData("CV15")]
    [InlineData("CV16")]
    [InlineData("CV17")]
    [InlineData("CV18")]
    [InlineData("CV23")]
    public void Source_HasNoViolationsBeyondItsBaseline(string rule)
    {
        IReadOnlyList<RuleCount> actual = Scanned.Value.Where(a => string.Equals(a.Rule, rule, StringComparison.Ordinal)).ToList();

        IReadOnlyList<string> overruns =
            UiConventionSource.Overruns(actual, UiConventionSource.ReadBaselines(BaselineDirectory()), UiConventionSource.ReadExceptions(BaselineDirectory()));

        Assert.True(overruns.Count == 0, "rule, path, actual, allowed:" + Environment.NewLine + string.Join(Environment.NewLine, overruns));
    }

    /// <summary>A baseline never allows more than the source has: a fix lowers the baseline in the same change.</summary>
    [Fact]
    public void Baseline_IsNoLooserThanTheSource()
    {
        IReadOnlyList<string> loose =
            UiConventionSource.Loose(Scanned.Value, UiConventionSource.ReadBaselines(BaselineDirectory()), UiConventionSource.ReadExceptions(BaselineDirectory()));

        Assert.True(loose.Count == 0, "Lower these baseline entries:" + Environment.NewLine + string.Join(Environment.NewLine, loose));
    }

    /// <summary>The generator renders one text per area, and every baseline file on disk is a known area.</summary>
    [Fact]
    public void Generator_RendersAnAreaFileForEveryArea()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("HUDDLE_WRITE_BASELINES"), "1", StringComparison.Ordinal))
        {
            UiConventionSource.WriteBaselines(Scanned.Value, BaselineDirectory());
        }

        IReadOnlyDictionary<string, string> rendered = UiConventionSource.RenderBaselines(Scanned.Value);
        string[] onDisk = Directory.EnumerateFiles(BaselineDirectory(), "*.txt")
            .Select(static f => Path.GetFileNameWithoutExtension(f))
            .Where(static n => !string.Equals(n, "Exceptions", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(UiConventionSource.Areas.Order(StringComparer.Ordinal), rendered.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(rendered.Keys.Order(StringComparer.Ordinal), onDisk);
    }

    /// <summary>
    /// Mutation proof: one violation of the rule added to a copy of a real file is counted by the
    /// scanner and overruns a baseline taken from the unmodified file.
    /// </summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="snippet">One violation of that rule.</param>
    [Theory]
    [InlineData("CV1", """<MudButton OnClick="X">x</MudButton>""")]
    [InlineData("CV2", """<MudTextField @bind-Value="x" />""")]
    [InlineData("CV3", """<MudAlert Severity="Severity.Info">x</MudAlert>""")]
    [InlineData("CV4", """this.Snackbar.Add("x", Severity.Info);""")]
    [InlineData("CV5", """<MudTextField @bind-Value="x" Variant="Variant.Outlined" Margin="Margin.Dense" ReadOnly="true" />""")]
    [InlineData("CV6", "<h3>x</h3>")]
    [InlineData("CV7", "private string x = default" + "!" + ";")]
    [InlineData("CV8", """<MudIcon Icon="@Icons.Material.Outlined.Add" />""")]
    [InlineData("CV9", """<div style="width: 1px"></div>""")]
    [InlineData("CV10", "private Task DispatchAsync(Func<Task> work) => work();")]
    [InlineData("CV11", ".zz { font-size: 13px; }")]
    [InlineData("CV11", ".zz { height: 30px; }")]
    [InlineData("CV11", ".zz .mud-icon-root { width: 12px; }")]
    [InlineData("CV11", ".zz { border-radius: calc(var(--mud-default-borderradius) * 0.75); }")]
    [InlineData("CV11", "@media (max-width: 1500px) { .zz { display: none; } }")]
    [InlineData("CV13", """<MudChip T="string" Size="Size.Small">x</MudChip>""")]
    [InlineData("CV14", """<MudToggleGroup T="string" Size="Size.Small"><MudToggleItem Value="@("x")" /></MudToggleGroup>""")]
    [InlineData("CV15", """<MudButton Size="Size.Small" Variant="Variant.Text">x</MudButton>""")]
    [InlineData("CV16", """<MudAlert Severity="Severity.Info" Dense="true" role="status">x</MudAlert>""")]
    [InlineData("CV17", """<MudTextField T="string" Label="x" Variant="Variant.Outlined" Margin="Margin.Dense" />""")]
    [InlineData("CV18", """<MudSelect T="string" Label="x" Variant="Variant.Outlined" Margin="Margin.Dense"><MudSelectItem Value="@("x")" /></MudSelect>""")]
    [InlineData("CV23", "<dl><dt>x</dt><dd>y</dd></dl>")]
    public void Mutation_AddedViolation_IsCountedAndOverrunsTheBaseline(string rule, string snippet)
    {
        string path = string.Equals(rule, "CV11", StringComparison.Ordinal)
            ? "src/Huddle.App/wwwroot/css/base.css"
            : "src/Huddle.App/Components/Shared/Composer.razor";
        string original = File.ReadAllText(CssSource.RepoPath(path.Split('/')));
        string mutated = original + "\n" + snippet + "\n";

        RuleCount before = UiConventionScanner.Scan(rule, path, original);
        RuleCount after = UiConventionScanner.Scan(rule, path, mutated);
        Dictionary<string, int> baseline = new(StringComparer.Ordinal) { [UiConventionSource.Key(rule, path)] = before.Count };
        IReadOnlyList<string> overruns = UiConventionSource.Overruns([after], baseline, new Dictionary<string, int>(StringComparer.Ordinal));

        Assert.Equal(before.Count + 1, after.Count);
        Assert.Single(overruns);
    }
}
