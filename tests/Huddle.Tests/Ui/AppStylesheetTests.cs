using System.Text.RegularExpressions;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Guards the two <c>&lt;link rel="stylesheet"&gt;</c> tags <c>App.razor</c> emits: that neither one
/// still names the pre-rename <c>Team.App.styles.css</c> scoped-CSS bundle key, and that every
/// stylesheet the shell links is actually served rather than 404ing behind an unresolved
/// <c>@Assets[...]</c> lookup.
/// </summary>
public sealed partial class AppStylesheetTests
{
    /// <summary>
    /// The rendered shell never contains the literal <c>Team.App</c> substring. <c>@Assets[...]</c>
    /// returns an unresolved key verbatim instead of throwing, so a stale key such as
    /// <c>Team.App.styles.css</c> would otherwise render as a 404ing href with no error anywhere.
    /// </summary>
    [Fact]
    public async Task AppShell_DoesNotReferenceTheRenamedProjectsBundle()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/", ct);

        Assert.DoesNotContain("Team.App", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every stylesheet the shell links resolves to a real, servable asset. This is the test that
    /// would have caught the <c>Team.App.styles.css</c> bug: that key fingerprints to nothing, so a
    /// broken <c>@Assets[...]</c> lookup only ever shows up as a failed HTTP round-trip, never as a
    /// build warning or a string an earlier test could match against.
    /// </summary>
    [Fact]
    public async Task AppShell_EveryLinkedStylesheetIsServed()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        using HttpClient client = factory.CreateClient();

        string html = await client.GetStringAsync("/", ct);
        List<string> hrefs = StylesheetLinkHref().Matches(html).Select(match => match.Groups["href"].Value).ToList();

        Assert.True(hrefs.Count >= 2, $"Expected at least two linked stylesheets, found {hrefs.Count}.");

        foreach (string href in hrefs)
        {
            using HttpResponseMessage response = await client.GetAsync("/" + href, ct);
            response.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// A real keyboard Tab used to leave nothing to see: MudBlazor.min.css resets <c>outline</c>
    /// to <c>none</c> for anchors and buttons, and <c>app.css</c> carried no <c>:focus-visible</c>
    /// rule at all to contest it. This is a source-text assertion, the same kind
    /// <see cref="CssSource"/> and <c>ThemeSourceTests</c> use for the same reason - nothing in
    /// this suite renders a browser, and a real Tab press is a manual check outside this suite.
    /// </summary>
    [Fact]
    public void AppCss_DeclaresFocusVisibleRule()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.Contains(":focus-visible", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The focus ring's colour must be <c>--mud-palette-text-primary</c>, not
    /// <c>--mud-palette-primary</c>: MudBlazor's dark palette darkens Primary rather than
    /// lightening it, so Primary-on-Background measures 1.63:1 in dark mode - under WCAG 1.4.11's
    /// 3:1 non-text contrast minimum for a focus indicator - while text-primary measures 13.79:1
    /// dark and 15.91:1 light. <see cref="AppCss_DeclaresFocusVisibleRule"/> would pass just as
    /// happily with the invisible primary-coloured version, so this asserts the property that
    /// actually matters: issue #25 was exactly this ring being present in the DOM but unseen.
    /// </summary>
    [Fact]
    public void AppCss_FocusRingUsesTextPrimaryForContrast()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.Contains("outline: 2px solid var(--mud-palette-text-primary);", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>.teammates-danger</c> was a dead rule: every destructive button is now a
    /// <c>MudButton Color="Color.Error"</c> (<c>ResetAllControl.razor</c>, <c>TeammateCard.razor</c>),
    /// so nothing in the app referenced the class any more. Asserts it stays deleted rather than
    /// creeping back in.
    /// </summary>
    [Fact]
    public void AppCss_DoesNotDeclareTeammatesDangerClass()
    {
        string text = File.ReadAllText(CssSource.RepoPath("src", "Huddle.App", "wwwroot", "app.css"));

        Assert.DoesNotContain(".teammates-danger", text, StringComparison.Ordinal);
    }

    [GeneratedRegex("<link\\s+rel=\"stylesheet\"\\s+href=\"(?<href>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex StylesheetLinkHref();
}
