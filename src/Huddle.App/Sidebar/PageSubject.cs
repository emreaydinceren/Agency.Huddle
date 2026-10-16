using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Sidebar;

/// <summary>What a page is showing (Spec D-2): a closed set of immutable records, published by the page.</summary>
#pragma warning disable S1694 // Spec D-2 fixes a closed record hierarchy; an interface would lose the base record's value equality that Subject comparison relies on.
public abstract record PageSubject
{
    /// <summary>What the Subject is about, without its live state. Keys a mounted panel (Spec D-5).</summary>
    public abstract string Key { get; }
}
#pragma warning restore S1694

/// <summary>A Room is open.</summary>
/// <param name="RoomId">The Room's id.</param>
public sealed record RoomSubject(string RoomId) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "room:" + this.RoomId;
}

/// <summary>A Library file is open.</summary>
/// <param name="Path">The file.</param>
/// <param name="Kind">What kind of file it is, known once it has loaded.</param>
/// <param name="Text">Its text as the page shows it now, or null for a file with no text.</param>
public sealed record DocumentSubject(LibraryPath Path, LibraryFileKind Kind, string? Text) : PageSubject
{
    /// <inheritdoc />
    public override string Key => "doc:" + this.Path.Root.Id + "/" + this.Path.RelativePath;
}
