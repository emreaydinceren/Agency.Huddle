using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A bUnit context for rendering the Room page (<c>Chat</c>) against a composed
/// <see cref="TeamWebApplicationFactory"/>: every service the page injects, taken from the factory's
/// own singletons, plus fakes for the reference resolvers and the circuit connection. One list, so a
/// service the page starts to inject is added in one place.
/// </summary>
internal static class ChatPageContext
{
    /// <summary>Builds the context; the caller owns and disposes it.</summary>
    /// <param name="factory">The factory whose composed singletons to reuse.</param>
    /// <returns>The context, ready to render <c>Chat</c>.</returns>
    public static MudBunitContext Create(TeamWebApplicationFactory factory)
    {
        MudBunitContext ctx = new();
        IServiceProvider source = factory.Services;
        ctx.Services.AddSingleton(source.GetRequiredService<ITeamDirectory>());
        ctx.Services.AddSingleton(source.GetRequiredService<IChatStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomEvents>());
        ctx.Services.AddSingleton(source.GetRequiredService<ChatService>());
        ctx.Services.AddSingleton(source.GetRequiredService<Drafts>());
        ctx.Services.AddSingleton(source.GetRequiredService<IAgentGateway>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaHealth>());
        ctx.Services.AddSingleton(source.GetRequiredService<RoomFollows>());
        ctx.Services.AddSingleton(source.GetRequiredService<PersonaStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<AvatarStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ContextFill>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Profile.IHumanIdentity>());
        ctx.Services.AddSingleton(source.GetRequiredService<IMentionAliasSource>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<HireLimitGuard>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<SprintProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<TaskStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.RetroProposalService>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<QuestionService>());
        ctx.Services.AddSingleton(source.GetRequiredService<IOptions<TeamOptions>>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationStore>());
        ctx.Services.AddSingleton(source.GetRequiredService<ElicitationService>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Teams.ITeamCatalog>());
        ctx.Services.AddSingleton(source.GetRequiredService<FreshStartNotes>());
        ctx.Services.AddSingleton(source.GetRequiredService<Agency.Huddle.App.Acp.Sessions.LastCatchUpStore>());
        ctx.Services.AddSingleton<ITaskReferenceResolver>(new FakeTaskReferenceResolver());
        ctx.Services.AddSingleton<ILibraryReferenceResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddSingleton(new TabService());

        // Chat navigates through ServerNavigator when a Rooms event moves the Human off a Room.
        ctx.Services.AddFakeCircuitConnection();
        return ctx;
    }
}
