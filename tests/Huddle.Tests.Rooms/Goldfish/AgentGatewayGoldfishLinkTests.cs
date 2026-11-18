using Agency.Huddle.App.Goldfish;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Services;
using Agency.Huddle.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Goldfish;

/// <summary>Pins <c>AgentGatewayGoldfishLink</c> (correction §2): an Agent with no connection is not an error.</summary>
public sealed class AgentGatewayGoldfishLinkTests
{
    /// <summary>Telling the runner of an offline Agent that a possession ended completes without throwing.</summary>
    [Fact]
    public async Task SendEndAsync_AgentOffline_CompletesSilently()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using AgentGateway gateway = new(new RoomEvents(NullLogger<RoomEvents>.Instance), new Drafts(), NullLogger<AgentGateway>.Instance);
        AgentGatewayGoldfishLink link = new(gateway);

        Task send = link.SendEndAsync("nobody", "p1", GoldfishExit.CalledBack, ct);
        await send;

        Assert.False(gateway.IsOnline("nobody"));
        Assert.True(send.IsCompletedSuccessfully);
    }
}
