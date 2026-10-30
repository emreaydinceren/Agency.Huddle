using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor.Utilities;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Seeder.Model;
using Agency.Huddle.Seeder.Scenarios;
using Agency.Huddle.Seeder.Writing;

namespace Agency.Huddle.Tests.Seeder;

/// <summary>
/// Pins Seeder spec §6.1 for every scenario in the catalog: each Agent names a picture of the bundled Avatar Library whose
/// gender matches the Agent's name, and no two Agents share one. No files are written.
/// </summary>
public sealed class SeedAvatarTests
{
    /// <summary>The name of every registered scenario.</summary>
    public static TheoryData<string> Scenarios { get; } = new(ScenarioCatalog.All.Select(static s => s.Name));

    /// <summary>SEED-AV-1: every Agent names a picture that is in the bundled library's <c>master.json</c>.</summary>
    /// <param name="scenario">The scenario name.</param>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Plan_EveryAgentNamesALibraryPicture(string scenario)
    {
        AvatarCatalog library = await LibraryAsync(TestContext.Current.CancellationToken);
        HashSet<string> files = new(library.All.Select(static a => a.File), StringComparer.Ordinal);

        Assert.All(Plan(scenario).Agents, agent => Assert.Contains(agent.AvatarFile, files));
    }

    /// <summary>SEED-AV-2: the picture's <c>gender_presentation</c> is the gender the scenario reads from the Agent's name.</summary>
    /// <param name="scenario">The scenario name.</param>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Plan_EveryPictureMatchesItsAgentsGender(string scenario)
    {
        AvatarCatalog library = await LibraryAsync(TestContext.Current.CancellationToken);

        Assert.All(Plan(scenario).Agents, agent =>
        {
            LibraryAvatar picture = Assert.Single(library.All, a => string.Equals(a.File, agent.AvatarFile, StringComparison.Ordinal));
            Assert.Equal(AvatarWriter.LibraryWord(agent.Gender), picture.Gender);
        });
    }

    /// <summary>SEED-AV-3: no two Agents in one scenario share a picture.</summary>
    /// <param name="scenario">The scenario name.</param>
    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Plan_NoTwoAgentsSharePicture(string scenario)
    {
        string[] files = [.. Plan(scenario).Agents.Select(static a => a.AvatarFile)];

        Assert.Equal(files.Length, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>A mismatched gender stops the seeder before it writes anything: the check is in the writer, not only here.</summary>
    [Fact]
    public async Task Write_GenderMismatch_IsRefused()
    {
        SeedPlan plan = Plan(ScenarioCatalog.DefaultName);
        SeedAgent first = plan.Agents[0];
        SeedAgent wrong = first with { Gender = first.Gender == SeedGender.Man ? SeedGender.Woman : SeedGender.Man };
        SeedPlan broken = plan with { Agents = [wrong, .. plan.Agents.Skip(1)] };
        string dataDir = Path.Combine(Path.GetTempPath(), "seed-tests", Guid.NewGuid().ToString("N"));

        try
        {
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AvatarWriter.WriteAsync(broken, dataDir, AvatarLibraryLocator.BundledDir, TestContext.Current.CancellationToken));

            Assert.Equal(
                $"{first.Name} reads as a {AvatarWriter.LibraryWord(wrong.Gender)}, but '{first.AvatarFile}' shows a {AvatarWriter.LibraryWord(first.Gender)}.",
                error.Message);
            Assert.False(File.Exists(Path.Combine(dataDir, "avatars.json")));
        }
        finally
        {
            if (Directory.Exists(dataDir))
            {
                Directory.Delete(dataDir, recursive: true);
            }
        }
    }

    /// <summary>SEED-AV-5: the Team colours of <c>software-co</c> are palette colours no two of which are within 30 degrees of hue.</summary>
    [Fact]
    public void Spread_KeepsTeamColoursApartInHue()
    {
        int teams = Plan(ScenarioCatalog.DefaultName).Teams.Count;

        double[] hues = [.. AvatarWriter.Spread(teams).Select(static hex => new MudColor(hex).H)];

        Assert.Equal(teams, hues.Length);
        Assert.All(
            hues.SelectMany((first, i) => hues.Skip(i + 1).Select(second => HueDistance(first, second))),
            distance => Assert.InRange(distance, 30, 180));
    }

    private static double HueDistance(double first, double second)
    {
        double difference = Math.Abs(first - second) % 360;
        return Math.Min(difference, 360 - difference);
    }

    private static SeedPlan Plan(string scenario) =>
        (ScenarioCatalog.Find(scenario) ?? throw new InvalidOperationException($"No scenario '{scenario}'.")).Build(SeededFolder.Today);

    private static async Task<AvatarCatalog> LibraryAsync(CancellationToken ct)
    {
        AvatarLibraryLocator locator = new(Options.Create(new TeamOptions()), AvatarLibraryLocator.BundledDir);
        AvatarLibraryLoadResult result = await new AvatarLibraryLoader(locator, NullLogger<AvatarLibraryLoader>.Instance).LoadAsync(ct);
        return Assert.IsType<AvatarCatalog>(result.Catalog);
    }
}
