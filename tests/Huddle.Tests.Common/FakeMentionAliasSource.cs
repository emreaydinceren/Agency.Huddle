using Agency.Huddle.App.Services;

namespace Agency.Huddle.Tests;

/// <summary>
/// A minimal, hand-written stand-in for <see cref="IMentionAliasSource"/>, for tests that construct a
/// <see cref="ChatService"/> directly and want to supply an explicit set of Aliases (or none at all).
/// Mirrors <see cref="Agency.Huddle.Tests.Acp.Tools.FakeAgentGateway"/>'s role for
/// <see cref="Agency.Huddle.App.Pipes.IAgentGateway"/>: not a mocking framework, just the smallest
/// class satisfying the interface.
/// </summary>
public sealed class FakeMentionAliasSource : IMentionAliasSource
{
    /// <summary>Every alias currently in force. Empty unless a test supplies otherwise.</summary>
    public IReadOnlyList<MentionAlias> Aliases { get; init; } = [];
}
