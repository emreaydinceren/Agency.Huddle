namespace Agency.Huddle.App.Components.Feed;

/// <summary>The kind badge on a Feed card's avatar.</summary>
/// <param name="Name">The kind it names: <c>check</c>, <c>document</c>, <c>person</c>, <c>gauge</c>, <c>lock</c>, <c>mention</c>, <c>question</c>, <c>form</c>, <c>plus</c>, <c>joined</c>, <c>archived</c>, <c>request</c> or <c>alert</c>.</param>
/// <param name="Icon">The Material icon that draws it.</param>
internal sealed record FeedBadge(string Name, string Icon);
