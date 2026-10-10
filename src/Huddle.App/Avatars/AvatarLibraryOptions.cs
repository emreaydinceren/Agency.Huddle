namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Configuration for the Avatar Library, bound from <c>Team:AvatarLibrary</c>.
/// </summary>
public sealed class AvatarLibraryOptions
{
    /// <summary>
    /// The folder holding <c>master.json</c> and the images, or null to use the library that ships with the app. A folder set here
    /// replaces the bundled library and is never backed by it, so a folder that does not exist means no library.
    /// </summary>
    public string? Dir { get; set; }
}
