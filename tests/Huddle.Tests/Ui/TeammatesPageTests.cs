namespace Agency.Huddle.Tests.Ui;

public sealed class TeammatesPageTests
{
    [Fact]
    public async Task TeammatesPage_ListsPersonasFromTheLibrary()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.PersonaDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.PersonaDirPath, "coo.md"), "You are the Chief of Staff.", ct);

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

        Directory.CreateDirectory(factory.PersonaDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.PersonaDirPath, "coo.md"), "You are the Chief of Staff.", ct);

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

        Directory.CreateDirectory(factory.PersonaDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.PersonaDirPath, "coo.md"), "You are the Chief of Staff.", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("agent-dot", html, StringComparison.Ordinal);
        Assert.Contains("Offline", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeammatesPage_ShowsANameThatContainsSpaces()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = cts.Token;

        await using var factory = new TeamWebApplicationFactory();

        Directory.CreateDirectory(factory.PersonaDirPath);
        await File.WriteAllTextAsync(Path.Combine(factory.PersonaDirPath, "Chief of Staff.md"), "You keep the team honest.", ct);

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/teammates", ct);

        Assert.Contains("Chief of Staff", html, StringComparison.Ordinal);

        // The monogram is built from the words of the Name, so it is also the page's own evidence
        // that the spaces survived the trip from disk.
        Assert.Contains(">CS<", html, StringComparison.Ordinal);
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
}