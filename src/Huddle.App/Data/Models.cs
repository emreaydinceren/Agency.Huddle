using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public sealed record User(string Id, string Name, UserKind Kind, string? Description);

public sealed record Room(string Id, string Name, DateTimeOffset Created);