namespace Agency.Huddle.Tests.Ui;

public sealed class TeammatesPageTests
{
    [Fact]
    public async Task TeammatesPage_ListsPersonasFromTheLibrary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("coo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_OffersAWayToCreateATeammate()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/teammates", ct);

        // The form itself lives in the card, which opens on click and so is absent from a plain
        // GET. What the page owes a first-time visitor is the way in. TeammateCardTests covers
        // what the card then shows.
        Assert.Contains("New teammate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ExplainsWhatAPersonaIs()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Markdown", html, StringComparison.Ordinal);

        // A Persona is the file AND the model it thinks with — the intro should say so, since the
        // card now lets a user change that model.
        Assert.Contains("model", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_DoesNotProbeForModelsOnAPlainLoad()
    {
        // This is the guard that keeps the whole suite free and fast, and the reason the model
        // catalog probe is safe to leave ungated on Team:Acp:Enabled: nothing about loading the
        // page opens a card, so nothing about loading the page may spawn an adapter process.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/teammates", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.ProbeCount);
    }

    [Fact]
    public async Task TeammatesPage_DoesNotProbeForEffortLevelsOnAPlainLoad()
    {
        // The sibling of the model-catalog guard above: the effort probe spawns the same real
        // adapter process, gated by the same "only when a Create/Edit form opens" rule, so a plain
        // GET must not trigger it either.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/teammates", ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, factory.FakeModelCatalog.EffortProbeCount);
    }

    [Fact]
    public async Task TeammatesPage_RendersATileThatOpensTheCard()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        // Edit, Open and Remove moved into the card, which a GET cannot open. The tile is the
        // affordance that reaches them, so the tile is what this page-level test can assert.
        Assert.Contains("teammate-tile", html, StringComparison.Ordinal);
        Assert.Contains("coo", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ShowsWhetherATeammateIsOnline()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("agent-dot", html, StringComparison.Ordinal);
        Assert.Contains("Offline", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stronger claim this phase makes: the front-matter Name beats the FILENAME all the way to
    /// the rendered page, not just through <see cref="Agency.Huddle.App.Acp.PersonaStore.ListNames"/>.
    /// The file is deliberately named "zzz.md" - nothing in it resembling "Chief of Staff" except
    /// the quoted <c>Name</c> field itself.
    /// </summary>
    [Fact]
    public async Task TeammatesPage_ShowsANameThatContainsSpaces()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "zzz.md"),
            "---\nName: 'Chief of Staff'\nTitle: Chief of Staff\nAlias: coo\n---\nYou keep the team honest.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Chief of Staff", html, StringComparison.Ordinal);

        // The monogram is built from the words of the Name, so it is also the page's own evidence
        // that the spaces survived the trip from disk.
        Assert.Contains(">CS<", html, StringComparison.Ordinal);
    }

    /// <summary>The test that proves Team sub-folders are cosmetic: a file nested under "Household" whose Teams field says "Business" must be headed "Business", never "Household".</summary>
    [Fact]
    public async Task TeammatesPage_GroupsByTheTeamsField_NotTheFolder()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        var nestedDir = Path.Combine(factory.TeamsDirPath, "Household");
        Directory.CreateDirectory(nestedDir);
        await File.WriteAllTextAsync(
            Path.Combine(nestedDir, "jarvis.md"),
            "---\nName: Jarvis\nTitle: Chief of Staff\nAlias: jar\nTeams: Business\n---\nYou are Jarvis.",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Business", html, StringComparison.Ordinal);

        // The folder name itself must never leak into a heading.
        Assert.DoesNotContain(">Household<", html, StringComparison.Ordinal);
    }

    /// <summary>A Persona in two Teams is not split across them - the same tile renders once under each heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateInTwoTeams_AppearsUnderBothHeadings()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "amy.md"),
            "---\nName: Amy\nTitle: Router\nAlias: amy\nTeams: Business, Household\n---\nbody",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("Household", html, StringComparison.Ordinal);

        // One tile per heading: Amy's Name has to appear exactly twice, once under each.
        Assert.Equal(2, CountOccurrences(html, "Amy"));
    }

    /// <summary>A Persona whose frontmatter has no Teams field at all falls under the synthetic "No team" heading.</summary>
    [Fact]
    public async Task TeammatesPage_ATeammateWithNoTeams_AppearsUnderTheNoTeamHeading()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.TeamsDirPath, "coo.md"), PersonaText("coo", "You are the Chief of Staff."), ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("No team", html, StringComparison.Ordinal);
    }

    /// <summary>A file missing a required identity field is listed by its path and reason, above the list, and never renders as a tile.</summary>
    [Fact]
    public async Task TeammatesPage_RejectedFile_IsListedByPathAndReason_AndDoesNotAppearAsATile()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        var path = Path.Combine(factory.TeamsDirPath, "broken.md");
        await File.WriteAllTextAsync(path, "---\nName: coo\nTitle: Chief of Staff\n---\nYou are the Chief of Staff.", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains(path, html, StringComparison.Ordinal);
        Assert.Contains("Alias", html, StringComparison.Ordinal);
        Assert.DoesNotContain("teammate-tile", html, StringComparison.Ordinal);
    }

    /// <summary>The filter's own options come off <see cref="Agency.Huddle.App.Acp.PersonaStore.Teams"/>: "All teams" plus every distinct Team name, regardless of how many Personas load.</summary>
    [Fact]
    public async Task TeammatesPage_OffersATeamFilter_ListingAllTeams()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.TeamsDirPath);
        await File.WriteAllTextAsync(
            Path.Combine(factory.TeamsDirPath, "coo.md"),
            "---\nName: coo\nTitle: Chief of Staff\nAlias: coo\nTeams: Business, Household\n---\nbody",
            ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("All teams", html, StringComparison.Ordinal);
        Assert.Contains("Business", html, StringComparison.Ordinal);
        Assert.Contains("Household", html, StringComparison.Ordinal);
    }

    /// <summary>Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.</summary>
    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public async Task Sidebar_LinksToTeammatesPage()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/", ct);

        Assert.Contains("href=\"/teammates\"", html, StringComparison.Ordinal);
    }

    /// <summary>Minimal valid Persona frontmatter (Name, Title and Alias all <paramref name="name"/>) wrapped around <paramref name="body"/> - identity is front-matter driven from this phase on, so every seeded Persona needs one to be listed at all.</summary>
    private static string PersonaText(string name, string body) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\n{body}";
}