namespace Agency.Huddle.App.FileChanges;

/// <summary>Bound from <c>Team:FileChanges</c>, per FC §6.14.</summary>
public sealed class FileChangesOptions
{
    /// <summary>
    /// The directory names pruned when <see cref="Ignore"/> is not set, compared
    /// case-insensitively: <c>.git</c>, <c>node_modules</c>, <c>bin</c>, <c>obj</c>.
    /// </summary>
    private static readonly IReadOnlyList<string> DefaultIgnore = [".git", "node_modules", "bin", "obj"];

    /// <summary>Whether File Changes runs at all. <see langword="false"/> passes a <see langword="null"/> tracker to every runner and offers neither tool.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The directory names <see cref="FolderScanner"/> prunes, compared case-insensitively.
    /// Nullable, with no initialiser: <c>ConfigurationBinder</c> appends bound array elements to an
    /// already-populated list/array property instead of replacing it (the "Collection options need
    /// no initialiser" rule), so a configured value must be read through
    /// <see cref="EffectiveIgnore"/> rather than off this property directly.
    /// </summary>
    public IReadOnlyList<string>? Ignore { get; set; }

    /// <summary><see cref="Ignore"/> when set and non-empty; otherwise <see cref="DefaultIgnore"/>.</summary>
    public IReadOnlyList<string> EffectiveIgnore => this.Ignore is { Count: > 0 } ignore ? ignore : DefaultIgnore;

    /// <summary>The most files one folder may hold before a scan of it reports <c>TooLarge</c> instead of walking it.</summary>
    public int MaxFilesPerFolder { get; set; } = 5000;

    /// <summary>The most File Changes lines listed per Turn, across every Watched Folder.</summary>
    public int MaxListed { get; set; } = 50;

    /// <summary>The most Memory lines shown in the system prompt; the rest are counted rather than listed.</summary>
    public int MaxMemoryEntries { get; set; } = 100;
}
