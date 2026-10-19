using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Sidebar;

/// <summary>A Library file is open.</summary>
/// <param name="Path">The file.</param>
/// <param name="Kind">What kind of file it is, known once it has loaded.</param>
/// <param name="Text">Its text as the page shows it now, or null for a file with no text.</param>
public sealed record DocumentSubject(LibraryPath Path, LibraryFileKind Kind, string? Text) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "doc:" + this.Path.Root.Id + "/" + this.Path.RelativePath;
}
