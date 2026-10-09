using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Decides whether an Avatar Library is configured and where it is, from <c>Team:AvatarLibrary:Dir</c>. It
/// reads no file contents and caches nothing, so asking whether to show the picker never loads the index.
/// </summary>
internal sealed class AvatarLibraryLocator
{
    private const string MasterFileName = "master.json";

    /// <summary>Creates the locator over the Team options.</summary>
    /// <param name="options">The Team options whose <c>AvatarLibrary.Dir</c> names the library folder.</param>
    public AvatarLibraryLocator(IOptions<TeamOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? configured = options.Value.AvatarLibrary.Dir;
        this.Dir = string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
    }

    /// <summary>
    /// The library folder with surrounding whitespace removed, or null when no folder is configured.
    /// </summary>
    internal string? Dir { get; }

    /// <summary>
    /// True when a folder is configured and it contains <c>master.json</c>. This is one file-existence check, not a parse.
    /// </summary>
    internal bool IsConfigured => this.Dir is not null && File.Exists(Path.Combine(this.Dir, MasterFileName));
}
