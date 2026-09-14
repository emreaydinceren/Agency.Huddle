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

    [GeneratedRegex("<link\\s+rel=\"stylesheet\"\\s+href=\"(?<href>[^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex StylesheetLinkHref();
}
