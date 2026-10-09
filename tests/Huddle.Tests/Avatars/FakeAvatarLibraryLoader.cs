using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// A hand-built <see cref="IAvatarLibraryLoader"/> for the picker tests: it returns a fresh deterministic
/// <see cref="AvatarCatalog"/> of 120 records on every call, or an unreadable result. It reads no file and
/// keeps only a weak reference to the last catalogue it handed out.
/// <para>
/// Record <c>i</c> (0 to 119): file <c>av-{i:D3}.png</c>; profession <c>i % 6</c> of Accountant, Artist, Butler,
/// Chef, Nurse, Pilot (20 each); skin tone <c>i % 5</c> of fair, light, tan, dark, brown (Light group 48,
/// Tan group 48, Dark group 24); gender man for even and woman for odd <c>i</c>. Tags: the excluded
/// <c>skin-*</c>, <c>bg-blue</c>, gender and profession slug, plus <c>glasses</c> when <c>i % 3 == 0</c> (40),
/// <c>vest</c> when <c>i % 7 == 0</c> (18) and <c>tagNN</c> with NN = <c>i % 50</c> (3 each for 00 to 19, 2 each for
/// 20 to 49). The tag pool is therefore 52 entries: glasses, vest, tag00 to tag19, tag20 to tag49.
/// </para>
/// </summary>
internal sealed class FakeAvatarLibraryLoader(bool unreadable = false) : IAvatarLibraryLoader
{
    private static readonly string[] Professions = ["Accountant", "Artist", "Butler", "Chef", "Nurse", "Pilot"];

    private static readonly string[] Tones = ["fair", "light", "tan", "dark", "brown"];

    /// <summary>A weak reference to the catalogue returned by the latest <see cref="LoadAsync"/>, or null before the first call.</summary>
    public WeakReference? LastCatalog { get; private set; }

    /// <summary>Builds the 120-record fixture catalogue described on the class.</summary>
    /// <returns>A new catalogue; every call returns a different instance with equal content.</returns>
    public static AvatarCatalog BuildCatalog()
    {
        List<LibraryAvatar> avatars = [];
        for (int i = 0; i < 120; i++)
        {
            string profession = Professions[i % 6];
            string tone = Tones[i % 5];
            string gender = (i % 2) == 0 ? "man" : "woman";
            List<string> tags = ["skin-" + tone, "bg-blue", gender, profession.ToLowerInvariant()];
            if ((i % 3) == 0)
            {
                tags.Add("glasses");
            }

            if ((i % 7) == 0)
            {
                tags.Add("vest");
            }

            tags.Add("tag" + (i % 50).ToString("D2", System.Globalization.CultureInfo.InvariantCulture));
            avatars.Add(new LibraryAvatar("av-" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".png", profession, tone, gender, tags));
        }

        return new AvatarCatalog(avatars);
    }

    /// <inheritdoc />
    public Task<AvatarLibraryLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (unreadable)
        {
            return Task.FromResult(new AvatarLibraryLoadResult(null, 0, true));
        }

        AvatarCatalog catalog = BuildCatalog();
        this.LastCatalog = new WeakReference(catalog);
        return Task.FromResult(new AvatarLibraryLoadResult(catalog, 0, false));
    }

    /// <inheritdoc />
    public Task<AvatarLibraryImage?> ReadImageAsync(string fileName, CancellationToken cancellationToken) =>
        Task.FromResult<AvatarLibraryImage?>(null);
}
