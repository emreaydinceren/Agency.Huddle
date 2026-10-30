using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MudBlazor.Utilities;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.Seeder.Model;

namespace Agency.Huddle.Seeder.Writing;

/// <summary>
/// Gives every Agent its Avatar Library picture (Seeder spec §6.1). Pictures are read through the app's
/// <see cref="AvatarLibraryLoader"/> and stored through its <see cref="AvatarStore"/>, so the formats stay the app's business.
/// Team colours are written by <see cref="TeamFileWriter"/> with <see cref="Spread(int)"/>.
/// </summary>
internal static class AvatarWriter
{
    /// <summary>The word <c>master.json</c> uses for <paramref name="gender"/> in <c>gender_presentation</c>.</summary>
    /// <param name="gender">The gender a scenario read from a name.</param>
    /// <returns><c>man</c> or <c>woman</c>.</returns>
    internal static string LibraryWord(SeedGender gender) => gender switch
    {
        SeedGender.Man => "man",
        SeedGender.Woman => "woman",
        _ => throw new ArgumentOutOfRangeException(nameof(gender), "Not a gender the library describes."),
    };

    /// <summary>
    /// <paramref name="count"/> distinct colours of <see cref="AvatarPalette"/>, spread by hue (SEED-AV-5): the first palette
    /// colour, then each time the colour farthest in hue from every one already chosen, the earlier one winning a tie. The result
    /// is in the six-digit hex form the avatar store holds, and is the same on every run.
    /// </summary>
    /// <param name="count">How many colours, at most the palette's number of distinct colours.</param>
    /// <returns>The distinct colours, in the order they were chosen.</returns>
    /// <exception cref="InvalidOperationException">The palette has fewer distinct colours than <paramref name="count"/>.</exception>
    internal static IReadOnlyList<string> Spread(int count)
    {
        // The palette runs through tints of one hue before the next and repeats a few colours, so taking every n-th entry gives
        // neighbours of the same hue. Pick by hue distance over the distinct colours instead.
        MudColor[] palette = [.. AvatarPalette.Colours.DistinctBy(static c => c.ToString(MudColorOutputFormats.Hex), StringComparer.OrdinalIgnoreCase)];
        if (count > palette.Length)
        {
            throw new InvalidOperationException($"The avatar palette has {palette.Length} distinct colours; {count} were asked for.");
        }

        List<MudColor> chosen = [];
        while (chosen.Count < count)
        {
            chosen.Add(chosen.Count == 0 ? palette[0] : FarthestInHue(palette, chosen));
        }

        return [.. chosen.Select(static c => c.ToString(MudColorOutputFormats.Hex))];
    }

    private static MudColor FarthestInHue(MudColor[] palette, List<MudColor> chosen)
    {
        MudColor best = palette[0];
        double bestDistance = -1;
        foreach (MudColor candidate in palette.Where(c => !chosen.Contains(c)))
        {
            double nearest = chosen.Min(c => HueDistance(c.H, candidate.H));
            if (nearest > bestDistance)
            {
                best = candidate;
                bestDistance = nearest;
            }
        }

        return best;
    }

    private static double HueDistance(double first, double second)
    {
        double difference = Math.Abs(first - second) % 360;
        return Math.Min(difference, 360 - difference);
    }

    /// <summary>
    /// Copies each Agent's picture into <c>avatars/</c> under a stable name and writes <c>avatars.json</c> with the Human's avatar
    /// and one image entry per Agent. Every picture is checked first, so a wrong file or gender writes nothing.
    /// </summary>
    /// <param name="plan">The plan whose Agents and Human are written.</param>
    /// <param name="dataDir">The data folder.</param>
    /// <param name="libraryDir">The Avatar Library folder holding <c>master.json</c> and the pictures.</param>
    /// <param name="ct">Cancels the reads.</param>
    /// <exception cref="InvalidOperationException">The library is unreadable, a picture is missing, or its gender is not the Agent's.</exception>
    internal static async Task WriteAsync(SeedPlan plan, string dataDir, string libraryDir, CancellationToken ct)
    {
        AvatarLibraryLocator locator = new(Options.Create(new TeamOptions()), libraryDir);
        AvatarLibraryLoader loader = new(locator, NullLogger<AvatarLibraryLoader>.Instance);
        AvatarCatalog library = (await loader.LoadAsync(ct).ConfigureAwait(false)).Catalog
            ?? throw new InvalidOperationException($"The Avatar Library at '{libraryDir}' could not be read.");

        List<(SeedAgent Agent, AvatarLibraryImage Image)> pictures = [];
        foreach (SeedAgent agent in plan.Agents)
        {
            pictures.Add((agent, await ReadPictureAsync(agent, library, loader, ct).ConfigureAwait(false)));
        }

        using AvatarStore store = new(Options.Create(new TeamOptions { DataDir = dataDir, HumanName = plan.HumanName }), NullLogger<AvatarStore>.Instance);
        store.Save(plan.HumanName, plan.HumanAvatar);
        IReadOnlyList<string> backgrounds = Spread(pictures.Count);
        for (int index = 0; index < pictures.Count; index++)
        {
            (SeedAgent agent, AvatarLibraryImage image) = pictures[index];
            string fileName = StableImageName(agent.Name) + image.Extension;
            SeedIo.WriteBytes(Path.Combine(store.ImageDirectory, fileName), image.Bytes);
            store.Save(agent.Name, new Avatar(Label: null, Image: fileName, Background: backgrounds[index]));
        }
    }

    private static async Task<AvatarLibraryImage> ReadPictureAsync(SeedAgent agent, AvatarCatalog library, AvatarLibraryLoader loader, CancellationToken ct)
    {
        LibraryAvatar? picture = library.All.FirstOrDefault(a => string.Equals(a.File, agent.AvatarFile, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{agent.Name}'s picture '{agent.AvatarFile}' is not in the Avatar Library.");
        string expected = LibraryWord(agent.Gender);
        if (!string.Equals(picture.Gender, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{agent.Name} reads as a {expected}, but '{agent.AvatarFile}' shows a {picture.Gender}.");
        }

        return await loader.ReadImageAsync(agent.AvatarFile, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"{agent.Name}'s picture '{agent.AvatarFile}' could not be read.");
    }

    /// <summary>A 32-character hex name derived from the Agent's name, so every run writes the same file and the seed diffs cleanly.</summary>
    private static string StableImageName(string agentName)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(agentName.ToUpperInvariant()));
        return "5eed" + Convert.ToHexStringLower(hash.AsSpan(0, 14));
    }
}
