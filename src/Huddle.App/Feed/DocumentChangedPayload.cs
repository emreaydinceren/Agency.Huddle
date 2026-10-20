namespace Agency.Huddle.App.Feed;

/// <summary>A Library document changed. Documents carry no Team in v1.</summary>
/// <param name="Path">The document's absolute path.</param>
/// <param name="RootName">The Library root's name.</param>
/// <param name="RelativePath">The path under the root.</param>
/// <param name="Kind">The kind of change.</param>
/// <param name="Summary">The change's summary.</param>
internal sealed record DocumentChangedPayload(string Path, string RootName, string RelativePath, string Kind, string Summary) : FeedPayload;
