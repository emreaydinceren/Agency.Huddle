using System.Text.Json;
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

    /// <summary>
    /// A redrawn library picture gets a new file name, and an unchanged one keeps its name. The app caches <c>/agent-avatars</c>
    /// as immutable for a year, so a name that ignored the bytes left browsers showing the old art after a reseed.
    /// </summary>
    [Fact]
    public async Task Write_RedrawnPicture_GetsNewFileName()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        SeedPlan plan = Plan(ScenarioCatalog.DefaultName);
        SeedAgent redrawn = plan.Agents[0];
        SeedAgent unchanged = plan.Agents[1];
        string root = Path.Combine(Path.GetTempPath(), "seed-tests", Guid.NewGuid().ToString("N"));
        string libraryDir = Path.Combine(root, "library");

        try
        {
            CopyFolder(AvatarLibraryLocator.BundledDir, libraryDir);
            await AvatarWriter.WriteAsync(plan, Path.Combine(root, "before"), libraryDir, ct);
            File.Copy(Path.Combine(libraryDir, plan.Agents[2].AvatarFile), Path.Combine(libraryDir, redrawn.AvatarFile), overwrite: true);
            await AvatarWriter.WriteAsync(plan, Path.Combine(root, "after"), libraryDir, ct);

            Dictionary<string, string> before = ImageNames(Path.Combine(root, "before"));
            Dictionary<string, string> after = ImageNames(Path.Combine(root, "after"));
            Assert.NotEqual(before[redrawn.Name], after[redrawn.Name]);
            Assert.Equal(before[unchanged.Name], after[unchanged.Name]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static Dictionary<string, string> ImageNames(string dataDir)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(dataDir, "avatars.json")));
        return document.RootElement.EnumerateObject()
            .Where(static p => p.Value.TryGetProperty("image", out _))
            .ToDictionary(static p => p.Name, static p => p.Value.GetProperty("image").GetString() ?? string.Empty, StringComparer.Ordinal);
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }

    /// <summary>
    /// SEED-AV-5: the Team colours of <c>software-co</c> are distinct palette colours, one per colour family, and no two are closer
    /// than ΔE 50 in CIE L*a*b*. The old hue-only spread put its closest pair (violet beside magenta) at ΔE 26, which reads as one colour.
    /// </summary>
    [Fact]
    public void Spread_KeepsTeamColoursApartAsTheEyeSeesThem()
    {
        int teams = Plan(ScenarioCatalog.DefaultName).Teams.Count;

        IReadOnlyList<string> colours = AvatarWriter.Spread(teams);

        Assert.Equal(AvatarWriter.Families.Take(teams), colours);
        MudColor[] parsed = [.. colours.Select(static hex => new MudColor(hex))];
        Assert.All(
            parsed.SelectMany((first, i) => parsed.Skip(i + 1).Select(second => AvatarWriter.Difference(first, second))),
            distance => Assert.True(distance >= 50, $"Two Team colours are only ΔE {distance:0.0} apart."));
    }

    /// <summary>
    /// Every pair of named colour families is at least ΔE 50 apart, not only the ones today's scenario uses, so a scenario that adds
    /// Teams up to the number of families still never gets two that look alike.
    /// </summary>
    [Fact]
    public void Families_AreAllAtLeastDeltaE50Apart()
    {
        MudColor[] families = [.. AvatarWriter.Families.Select(static hex => new MudColor(hex))];

        Assert.All(
            families.SelectMany((first, i) => families.Skip(i + 1).Select(second => (first, second))),
            pair => Assert.True(AvatarWriter.Difference(pair.first, pair.second) >= 50, $"{pair.first} and {pair.second} are only ΔE {AvatarWriter.Difference(pair.first, pair.second):0.0} apart."));
    }

    /// <summary>The colour distance is CIE76 in L*a*b*: black and white are ΔE 100 apart, a colour is 0 from itself, and pure red to pure green is about 170.</summary>
    [Fact]
    public void Difference_MatchesKnownLabDistances()
    {
        Assert.Equal(100, AvatarWriter.Difference(new MudColor("#000000"), new MudColor("#ffffff")), precision: 0);
        Assert.Equal(0, AvatarWriter.Difference(new MudColor("#dc143c"), new MudColor("#dc143c")), precision: 6);
        Assert.InRange(AvatarWriter.Difference(new MudColor("#ff0000"), new MudColor("#00ff00")), 169, 171);
    }

    /// <summary>Every colour family the seeder hands out is still an <see cref="AvatarPalette"/> colour, so a Team's colour is one the editor's swatches can show as chosen.</summary>
    [Fact]
    public void Families_AreAllPaletteColours()
    {
        string[] palette = [.. AvatarPalette.Colours.Select(static c => c.ToString(MudColorOutputFormats.Hex))];

        Assert.All(AvatarWriter.Families, hex => Assert.Contains(hex, palette, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Past the named families the spread still hands out distinct palette colours, so a plan with more Agents than families never repeats one.</summary>
    [Fact]
    public void Spread_PastTheFamilies_StaysDistinct()
    {
        int count = AvatarWriter.Families.Count + 5;

        IReadOnlyList<string> colours = AvatarWriter.Spread(count);

        Assert.Equal(count, colours.Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
