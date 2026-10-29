using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Factories;

/// <summary>A <see cref="ChatService"/> with the collaborators it was built from, so a test can drive and observe them.</summary>
/// <param name="Chat">The service under test.</param>
/// <param name="Store">The transcript store the service writes to.</param>
/// <param name="Events">The hub the service raises Room events on.</param>
/// <param name="Proposals">The Proposal store the service was built with.</param>
/// <param name="Questions">The Question store the service was built with.</param>
internal sealed record ChatStack(ChatService Chat, IChatStore Store, RoomEvents Events, ProposalStore Proposals, QuestionStore Questions);

/// <summary>Builds a <see cref="ChatService"/> over real or supplied collaborators.</summary>
internal static class ChatServiceFactory
{
    /// <summary>Builds a <see cref="ChatService"/>, creating any collaborator that is not supplied.</summary>
    /// <param name="directory">The Team Directory the service reads.</param>
    /// <param name="options">The options the service and a default store are built from.</param>
    /// <param name="store">The transcript store; <see langword="null"/> creates a <see cref="FileChatStore"/> over <paramref name="options"/>.</param>
    /// <param name="events">The Room event hub; <see langword="null"/> creates a new one.</param>
    /// <param name="aliases">The mention-alias source; <see langword="null"/> uses a <see cref="FakeMentionAliasSource"/>.</param>
    /// <param name="teams">The Team catalog the service resolves Team labels with; <see langword="null"/> leaves it unset.</param>
    public static ChatStack Create(
        ITeamDirectory directory,
        IOptions<TeamOptions> options,
        IChatStore? store = null,
        RoomEvents? events = null,
        IMentionAliasSource? aliases = null,
        ITeamCatalog? teams = null)
    {
        IChatStore chatStore = store ?? options.CreateChatStore();
        RoomEvents roomEvents = events ?? new RoomEvents(NullLogger<RoomEvents>.Instance);
        ProposalStore proposals = new(roomEvents);
        QuestionStore questions = new(roomEvents);
        ChatService chat = new(directory, chatStore, roomEvents, aliases ?? new FakeMentionAliasSource(), options, proposals, questions, NullLogger<ChatService>.Instance)
        {
            TeamCatalog = teams,
        };
        return new ChatStack(chat, chatStore, roomEvents, proposals, questions);
    }
}
