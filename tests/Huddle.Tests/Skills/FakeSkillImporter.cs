using Agency.Huddle.App.Skills;

namespace Agency.Huddle.Tests.Skills;

/// <summary>An <see cref="ISkillImporter"/> that answers from canned previews and records every call, so a test never touches the network or the disk.</summary>
internal sealed class FakeSkillImporter : ISkillImporter
{
    /// <summary>The previews returned by catalog id; an id that is not here previews as <see langword="null"/>.</summary>
    public Dictionary<string, ImportPreview> ByCatalogId { get; } = new(StringComparer.Ordinal);

    /// <summary>The preview returned for any URL, or <see langword="null"/> to answer that the text is not a tree URL.</summary>
    public ImportPreview? ForUrl { get; set; }

    /// <summary>What <see cref="Install"/> answers.</summary>
    public ImportOutcome Outcome { get; set; } = new(true, null, Path.Combine("skills", "alpha"));

    /// <summary>The catalog ids previewed, in order.</summary>
    public List<string> CatalogCalls { get; } = [];

    /// <summary>The URLs previewed, in order.</summary>
    public List<string> UrlCalls { get; } = [];

    /// <summary>The previews passed to <see cref="Install"/>, in order.</summary>
    public List<ImportPreview> Installs { get; } = [];

    /// <inheritdoc />
    public Task<ImportPreview?> PreviewFromCatalogAsync(string catalogId, CancellationToken ct)
    {
        this.CatalogCalls.Add(catalogId);
        return Task.FromResult(this.ByCatalogId.GetValueOrDefault(catalogId));
    }

    /// <inheritdoc />
    public Task<ImportPreview?> PreviewFromUrlAsync(string githubTreeUrl, CancellationToken ct)
    {
        this.UrlCalls.Add(githubTreeUrl);
        return Task.FromResult(this.ForUrl);
    }

    /// <inheritdoc />
    public ImportOutcome Install(ImportPreview preview)
    {
        this.Installs.Add(preview);
        return this.Outcome;
    }

    /// <summary>Builds a preview for <c>acme/skills</c> <c>skills/alpha</c>; every field can be overridden.</summary>
    public static ImportPreview MakePreview(
        string name = "alpha",
        string license = "MIT",
        IReadOnlyList<ReviewFlag>? flags = null,
        IReadOnlyList<string>? problems = null,
        IReadOnlyList<string>? differences = null,
        bool? canInstall = null,
        SkillsCatalogEntry? entry = null)
    {
        IReadOnlyList<string> problemList = problems ?? [];
        return new ImportPreview(
            entry,
            "https://github.com/acme/skills",
            "skills/alpha",
            "0123456789abcdef0123456789abcdef01234567",
            "Acme",
            license,
            name,
            "Alpha does the alpha thing.",
            differences ?? [],
            flags ?? [],
            problemList,
            canInstall ?? problemList.Count == 0,
            new DownloadedSkill(new Dictionary<string, byte[]>(StringComparer.Ordinal) { ["SKILL.md"] = [1] }));
    }
}
