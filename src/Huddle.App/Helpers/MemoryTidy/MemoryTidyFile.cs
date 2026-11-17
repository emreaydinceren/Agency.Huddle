using Agency.Huddle.App.Memory;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One memory file shown in full.</summary>
/// <param name="File">The file name.</param>
/// <param name="Version">The file's <see cref="MemoryVersion"/> when it was read.</param>
/// <param name="Text">The file's whole text, frontmatter included, without a byte-order mark.</param>
/// <param name="WrittenDay">The day of §6.5, which a merge carries forward.</param>
internal sealed record MemoryTidyFile(string File, string Version, string Text, DateOnly WrittenDay);
