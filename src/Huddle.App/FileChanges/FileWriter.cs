namespace Agency.Huddle.App.FileChanges;

/// <summary>This Agent's last own write to a file: the Room whose Turn touched it, and the file as that left it.</summary>
/// <param name="RoomId">The id of the Room whose Turn last wrote the file.</param>
/// <param name="Entry">The file as that write left it.</param>
internal sealed record FileWriter(string RoomId, FileEntry Entry);
