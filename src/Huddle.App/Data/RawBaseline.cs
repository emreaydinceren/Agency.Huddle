namespace Agency.Huddle.App.Data;

/// <summary>The size and modified time of a raw upload as they were right after it was written (Spec 7.6).</summary>
/// <param name="Path">Path relative to the data folder, with <c>/</c> separators.</param>
/// <param name="SizeBytes">The file size in bytes.</param>
/// <param name="ModifiedUtc">The file's last-write time after the write.</param>
/// <param name="RecordedUtc">When the baseline was recorded.</param>
internal sealed record RawBaseline(string Path, long SizeBytes, DateTimeOffset ModifiedUtc, DateTimeOffset RecordedUtc);
