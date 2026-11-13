using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins R11 for the file endpoint: an old upload link under a renamed Team's folder is served from the new folder.</summary>
public sealed class LibraryFilesEndpointRenameTests
{
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    /// <summary>A request for <c>Teams/Marketing/raw/x.png</c> after Marketing became Growth is served from <c>Teams/Growth/raw/x.png</c>.</summary>
    [Fact]
    public async Task Get_OldTeamNameUnderRaw_ServedFromRenamedFolder()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        string rawFolder = Path.Combine(factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir, "Teams", "Growth", "raw");
        Directory.CreateDirectory(rawFolder);
        await File.WriteAllBytesAsync(Path.Combine(rawFolder, "x.png"), MinimalPng, ct);
        ITeamRenames journal = factory.Services.GetRequiredService<ITeamRenames>();
        journal.Complete(journal.Begin(TeamRenameKind.Team, "Marketing", "Marketing", "Growth"));

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/library-files/teams/Marketing/raw/x.png", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>A name that was never journalled keeps answering 404.</summary>
    [Fact]
    public async Task Get_NeverJournalledTeam_NotFound()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
        CancellationToken ct = cts.Token;

        await using TeamWebApplicationFactory factory = new();
        string rawFolder = Path.Combine(factory.Services.GetRequiredService<IOptions<TeamOptions>>().Value.DataDir, "Teams", "Growth", "raw");
        Directory.CreateDirectory(rawFolder);
        await File.WriteAllBytesAsync(Path.Combine(rawFolder, "x.png"), MinimalPng, ct);

        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/library-files/teams/Marketing/raw/x.png", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
