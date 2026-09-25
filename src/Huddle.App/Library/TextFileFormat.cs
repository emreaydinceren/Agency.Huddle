namespace Agency.Huddle.App.Library;

/// <summary>The text encoding and line ending style of a file.</summary>
/// <param name="EncodingName">The encoding name (e.g., "utf-8").</param>
/// <param name="HasBom">Whether the file begins with a byte-order mark.</param>
/// <param name="LineEnding">The line ending style used.</param>
/// <param name="MixedLineEndings">Whether the file uses inconsistent line endings.</param>
public sealed record TextFileFormat(string EncodingName, bool HasBom, LineEnding LineEnding, bool MixedLineEndings);
