using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Services;

public sealed record ComposerResult(ChatMessage? Posted, string? Info);