using System.Text.RegularExpressions;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// The Avatar Library's records held in memory for one picker opening: filtering by an
/// <see cref="AvatarLibraryQuery"/> and the two chip pools. It has no static state and caches nothing
/// outside the instance, so dropping the instance releases everything.
/// </summary>
internal sealed partial class AvatarCatalog
{
    private readonly List<LibraryAvatar> avatars;
    private readonly List<string[]> haystacks;

    /// <summary>Copies the avatars once, keeps their order, and computes both chip pools.</summary>
    /// <param name="avatars">The library records in master order.</param>
    internal AvatarCatalog(IEnumerable<LibraryAvatar> avatars)
    {
        ArgumentNullException.ThrowIfNull(avatars);

        this.avatars = [.. avatars];
        this.haystacks = [.. this.avatars.Select(BuildHaystack)];
        this.Professions = BuildProfessions(this.avatars);
        this.Tags = BuildTags(this.avatars);
    }

    /// <summary>The number of avatars in the catalog.</summary>
    internal int Count => this.avatars.Count;

    /// <summary>Every avatar in master order.</summary>
    internal IReadOnlyList<LibraryAvatar> All => this.avatars;

    /// <summary>The distinct professions, alphabetical ignoring case, in the casing first seen.</summary>
    internal IReadOnlyList<string> Professions { get; }

    /// <summary>The tag chip pool, most frequent first then alphabetical; it ignores any query.</summary>
    internal IReadOnlyList<TagCount> Tags { get; }

    /// <summary>Returns the avatars that satisfy every control of <paramref name="query"/>, in master order.</summary>
    /// <param name="query">The filter state.</param>
    /// <returns>The matching avatars; empty when none match.</returns>
    internal IReadOnlyList<LibraryAvatar> Search(AvatarLibraryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        string[] words = Tokenise(query.Text);
        List<LibraryAvatar> result = [];
        for (int i = 0; i < this.avatars.Count; i++)
        {
            LibraryAvatar avatar = this.avatars[i];
            if (query.Skin is not null && avatar.Group != query.Skin)
            {
                continue;
            }

            if (query.Professions.Count > 0 && !query.Professions.Contains(avatar.Profession))
            {
                continue;
            }

            if (!query.Tags.All(tag => avatar.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            string[] haystack = this.haystacks[i];
            if (words.All(word => haystack.Any(token => token.StartsWith(word, StringComparison.OrdinalIgnoreCase))))
            {
                result.Add(avatar);
            }
        }

        return result;
    }

    private static string[] Tokenise(string text) =>
        text.Replace('-', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string[] BuildHaystack(LibraryAvatar avatar) =>
        [.. Tokenise(avatar.Profession), .. avatar.Tags.SelectMany(Tokenise)];

    private static List<string> BuildProfessions(List<LibraryAvatar> source)
    {
        List<string> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (LibraryAvatar avatar in source)
        {
            if (seen.Add(avatar.Profession))
            {
                result.Add(avatar.Profession);
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static List<TagCount> BuildTags(List<LibraryAvatar> source)
    {
        Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> casing = new(StringComparer.OrdinalIgnoreCase);
        foreach (LibraryAvatar avatar in source)
        {
            string slug = Slug(avatar.Profession);
            HashSet<string> own = new(StringComparer.OrdinalIgnoreCase);
            foreach (string tag in avatar.Tags)
            {
                if (IsExcluded(tag, slug) || !own.Add(tag))
                {
                    continue;
                }

                counts[tag] = counts.GetValueOrDefault(tag) + 1;
                casing.TryAdd(tag, tag);
            }
        }

        List<TagCount> result = [.. counts.Select(pair => new TagCount(casing[pair.Key], pair.Value))];
        result.Sort(static (a, b) => a.Count != b.Count
            ? b.Count.CompareTo(a.Count)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Tag, b.Tag));
        return result;
    }

    private static bool IsExcluded(string tag, string professionSlug) =>
        string.Equals(tag, professionSlug, StringComparison.OrdinalIgnoreCase)
        || tag.StartsWith("skin-", StringComparison.OrdinalIgnoreCase)
        || tag.StartsWith("bg-", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "man", StringComparison.OrdinalIgnoreCase)
        || string.Equals(tag, "woman", StringComparison.OrdinalIgnoreCase);

    private static string Slug(string profession) =>
        NonAlphanumeric().Replace(profession.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumeric();
}
