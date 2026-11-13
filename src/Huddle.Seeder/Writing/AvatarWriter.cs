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
    /// <summary>
    /// One <see cref="AvatarPalette"/> colour from each family the eye names apart, in the order <see cref="Spread(int)"/> hands them
    /// out: crimson, dodger blue, lime green, purple, olive gold and slate. No computed distance kept a scenario's Teams this far
    /// apart: spreading by hue let a crimson and a magenta through, and CIE76 a turquoise beside a lime, each pair reading as one pink
    /// or one green at the avatar's tint. No two entries are closer than ΔE 50, which is why orange red (ΔE 37 from crimson) is not
    /// one. Each entry must stay in the palette; <see cref="Spread(int)"/> throws if one goes. Agents' backgrounds take the same order.
    /// </summary>
    internal static readonly IReadOnlyList<string> Families = ["#dc143c", "#1e90ff", "#32cd32", "#8e24aa", "#988919", "#5e7c8a"];

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
    /// <paramref name="count"/> distinct colours of <see cref="AvatarPalette"/>, spread so no two look alike (SEED-AV-5): first the
    /// <see cref="Families"/> in order, one palette colour per colour family, then each time the colour whose nearest already-chosen
    /// one is farthest away as the eye sees it (<see cref="Difference"/>, CIE L*a*b*), the earlier one winning a tie. The result is
    /// in the six-digit hex form the avatar store holds, and is the same on every run.
    /// </summary>
    /// <param name="count">How many colours, at most the palette's number of distinct colours.</param>
    /// <returns>The distinct colours, in the order they were chosen.</returns>
    /// <exception cref="InvalidOperationException">The palette has fewer distinct colours than <paramref name="count"/>.</exception>
    internal static IReadOnlyList<string> Spread(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        // The palette runs through tints of one hue before the next and repeats a few colours, so taking every n-th entry gives
        // neighbours of the same hue. Pick by perceived distance over the distinct colours instead.
        MudColor[] palette = [.. AvatarPalette.Colours.DistinctBy(static c => c.ToString(MudColorOutputFormats.Hex), StringComparer.OrdinalIgnoreCase)];
        if (count > palette.Length)
        {
            throw new InvalidOperationException($"The avatar palette has {palette.Length} distinct colours; {count} were asked for.");
        }

        List<MudColor> chosen = [];
        foreach (string hex in Families)
        {
            if (chosen.Count == count)
            {
                break;
            }

            chosen.Add(palette.FirstOrDefault(c => string.Equals(c.ToString(MudColorOutputFormats.Hex), hex, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"The avatar palette no longer holds {hex}; update {nameof(AvatarWriter)}.{nameof(Families)}."));
        }

        while (chosen.Count < count)
        {
            chosen.Add(chosen.Count == 0 ? palette[0] : FarthestApart(palette, chosen));
        }

        return [.. chosen.Select(static c => c.ToString(MudColorOutputFormats.Hex))];
    }

    /// <summary>
    /// The candidate whose nearest chosen colour is farthest away as the eye sees it (<see cref="Difference"/>), the earlier one
    /// winning a tie. Hue alone let a teal and a lime, or a crimson and a magenta, through as "different" while both read as the
    /// same green or pink at the avatar's tint.
    /// </summary>
    private static MudColor FarthestApart(MudColor[] palette, List<MudColor> chosen)
    {
        MudColor best = palette[0];
        double bestDistance = -1;
        foreach (MudColor candidate in palette.Where(c => !chosen.Contains(c)))
        {
            double nearest = chosen.Min(c => Difference(c, candidate));
            if (nearest > bestDistance)
            {
                best = candidate;
                bestDistance = nearest;
            }
        }

        return best;
    }

    /// <summary>How different two colours look: the CIE76 ΔE, the straight-line distance between them in CIE L*a*b* (D65).</summary>
    /// <param name="first">One colour.</param>
    /// <param name="second">The other.</param>
    /// <returns>The distance; about 2 is the smallest difference the eye notices, and over 30 reads as a different colour.</returns>
    internal static double Difference(MudColor first, MudColor second)
    {
        (double l1, double a1, double b1) = Lab(first);
        (double l2, double a2, double b2) = Lab(second);
        return Math.Sqrt(((l1 - l2) * (l1 - l2)) + ((a1 - a2) * (a1 - a2)) + ((b1 - b2) * (b1 - b2)));
    }

    /// <summary>Converts an sRGB colour to CIE L*a*b* under the D65 white point.</summary>
    private static (double L, double A, double B) Lab(MudColor colour)
    {
        double r = Linear(colour.R);
        double g = Linear(colour.G);
        double b = Linear(colour.B);
        double x = Pivot(((0.4124 * r) + (0.3576 * g) + (0.1805 * b)) / 0.95047);
        double y = Pivot((0.2126 * r) + (0.7152 * g) + (0.0722 * b));
        double z = Pivot(((0.0193 * r) + (0.1192 * g) + (0.9505 * b)) / 1.08883);
        return ((116 * y) - 16, 500 * (x - y), 200 * (y - z));
    }

    /// <summary>An sRGB channel (0-255) as linear light (0-1).</summary>
    private static double Linear(byte channel)
    {
        double value = channel / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>The CIE L*a*b* companding of a white-relative XYZ component.</summary>
    private static double Pivot(double value) =>
        value > 216.0 / 24389 ? Math.Cbrt(value) : ((24389.0 / 27 * value) + 16) / 116;

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
