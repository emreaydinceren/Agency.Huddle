using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Pages;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The Room page's header draws each Agent Member's avatar with the Room and Agent ids the popover needs
/// (LRR 6.4): clicking that avatar offers <c>Start afresh</c>. Pins the wiring in <c>Chat.razor</c>, which
/// the popover's own tests cannot see.
/// </summary>
public sealed class ChatHeaderStartAfreshTests
{
    /// <summary>Clicking an Agent Member's header avatar shows the Start afresh button in the popover.</summary>
    [Fact]
    public async Task ChatHeader_ClickingAnAgentAvatar_ShowsStartAfresh()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(60));
        CancellationToken ct = cts.Token;
        await using TeamWebApplicationFactory factory = new();
        factory.Services.GetRequiredService<PersonaStore>().Add(
            new PersonaIdentity("alpha", "Lead", "alpha", Teams: [], Adapter: null, Skills: ["lint"], Role: null),
            "You help.",
            null,
            null);
        ITeamDirectory directory = factory.Services.GetRequiredService<ITeamDirectory>();
        User? agent = await directory.UpsertAgentUserAsync("alpha", null, ct);
        Assert.NotNull(agent);
        Room room = await factory.Services.GetRequiredService<ChatService>().EnsureRoomForAsync(agent, ct);

        await using MudBunitContext ctx = UiTestHost.Create(services => services.CopyFrom(factory.Services, ServicePresets.Chat));
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddFakeCircuitConnection();
        ctx.Services.CopyFrom(
            factory.Services,
            typeof(AgentProfiles),
            typeof(AdapterCatalog),
            typeof(Agency.Huddle.App.Pipes.IAgentGateway));
        using IServiceScope scope = factory.Services.CreateScope();
        ctx.Services.AddSingleton(scope.ServiceProvider.GetRequiredService<AgentProfileCardOpener>());

        IRenderedComponent<ContainerFragment> cut = ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Chat>(0);
            builder.AddAttribute(1, nameof(Chat.RoomId), room.Id);
            builder.CloseComponent();
        });

        await cut.InvokeAsync(() => cut.Find("button.profile-avatar-button").ClickAsync());

        cut.WaitForAssertion(
            () => Assert.Single(cut.FindAll(".profile-popover button[aria-label='Start alpha afresh in this Room']")),
            TimeSpan.FromSeconds(10));

        // Confirming sends the Room and Agent ids the header passed, so a literal in their place is caught.
        await cut.InvokeAsync(() => cut.Find(".profile-popover button[aria-label='Start alpha afresh in this Room']").ClickAsync());
        await cut.InvokeAsync(() => cut.Find(".profile-popover .inline-confirm-confirm").ClickAsync());

        cut.WaitForAssertion(() => Assert.Single(factory.FakeAgentGateway.StartAfreshCalls), TimeSpan.FromSeconds(10));
        (string AgentId, string RoomId) call = factory.FakeAgentGateway.StartAfreshCalls[0];
        Assert.Equal(agent.Id, call.AgentId);
        Assert.Equal(room.Id, call.RoomId);
    }
}
