using Agency.Huddle.Contracts;

namespace Agency.Huddle.App.Data;

public interface IChatStore
{
    Task AppendAsync(string roomId, ChatMessage message, CancellationToken ct = default);

    Task<IReadOnlyList<ChatMessage>> ReadAllAsync(string roomId, CancellationToken ct = default);
}