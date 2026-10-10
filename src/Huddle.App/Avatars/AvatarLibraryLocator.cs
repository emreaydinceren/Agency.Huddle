using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Decides whether an Avatar Library is configured and where it is: the folder named by <c>Team:AvatarLibrary:Dir</c>, or,
/// when that is unset, the library that ships with the app. It reads no file contents and caches nothing, so asking whether
/// to show the picker never loads the index.
/// </summary>
/// <remarks>
/// A configured <c>Dir</c> that is not a library (the folder is missing, or has no <c>master.json</c>) is <b>not</b> replaced by
/// the bundled folder. That is the one way for an operator to switch the shipped library off, and falling back would remove it.
/// </remarks>
internal sealed class AvatarLibraryLocator
{
    private const string MasterFileName = "master.json";

    /// <summary>
    /// The library that ships with the app: the <c>AvatarLibrary</c> folder beside the application, where the build and a publish
    /// copy <c>src/Huddle.App/AvatarLibrary</c>. It is found from the application's base directory, not the working directory, so
    /// it is the same wherever the app is started from.
    /// </summary>
    internal static string BundledDir { get; } = Path.Combine(AppContext.BaseDirectory, "AvatarLibrary");

    /// <summary>Creates the locator over the Team options.</summary>
    /// <param name="options">The Team options whose <c>AvatarLibrary.Dir</c> names the library folder.</param>
    /// <param name="bundledDir">The library to use when <c>Dir</c> is unset or blank, or null for no default. The app passes <see cref="BundledDir"/>.</param>
    public AvatarLibraryLocator(IOptions<TeamOptions> options, string? bundledDir = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? configured = options.Value.AvatarLibrary.Dir;
        string? chosen = string.IsNullOrWhiteSpace(configured) ? bundledDir : configured;
        this.Dir = string.IsNullOrWhiteSpace(chosen) ? null : chosen.Trim();
    }

    /// <summary>
    /// The library folder with surrounding whitespace removed: the configured one, else the bundled one, or null when there is neither.
    /// </summary>
    internal string? Dir { get; }

    /// <summary>
    /// True when a folder is configured and it contains <c>master.json</c>. This is one file-existence check, not a parse.
    /// </summary>
    internal bool IsConfigured => this.Dir is not null && File.Exists(Path.Combine(this.Dir, MasterFileName));
}
