namespace Agency.Huddle.App.Avatars;

/// <summary>
/// Configuration for the Avatar Library, bound from <c>Team:AvatarLibrary</c>.
/// </summary>
public sealed class AvatarLibraryOptions
{
    /// <summary>The folder holding <c>master.json</c> and the images, or null for no library.</summary>
    public string? Dir { get; set; }
}
