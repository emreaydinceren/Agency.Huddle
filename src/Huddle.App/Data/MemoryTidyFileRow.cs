namespace Agency.Huddle.App.Data;

/// <summary>One file's row in <c>memory_tidy_files</c>: the version it had and the local day it was written (Memory Tidy spec §6.5).</summary>
/// <param name="FolderKey">The folder key, <c>agent:Name</c> or <c>huddle</c>.</param>
/// <param name="File">The file's name in its memory folder.</param>
/// <param name="Version">The file's version token when the row was written.</param>
/// <param name="WrittenDay">The local day the file was written.</param>
internal sealed record MemoryTidyFileRow(string FolderKey, string File, string Version, DateOnly WrittenDay);
