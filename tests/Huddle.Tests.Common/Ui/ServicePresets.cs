using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Appearance;
using Agency.Huddle.App.Avatars;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Elicitation;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Profile;
using Agency.Huddle.App.Prompts;
using Agency.Huddle.App.Questions;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Skills;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Agents;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The service lists a bUnit context copies out of a <see cref="TeamWebApplicationFactory"/> to render one
/// page, each with the reason every service is there. Pass one to <see cref="CopyFrom"/>; a test that needs
/// something extra adds it beside the call.
/// </summary>
internal static class ServicePresets
{
    /// <summary>
    /// What <c>Chat.razor</c> and its children inject: the Room services, then one group per child
    /// component. Not included, because the factory does not own them: the fake <c>ITaskReferenceResolver</c>
    /// (<c>MessageList</c> injects it to link Task ids), the fake <c>ILibraryReferenceResolver</c>
    /// (<c>MessageList</c> injects it to link Library paths) and the circuit connection <c>ServerNavigator</c>
    /// uses (<c>Chat.razor</c> navigates through it when a Rooms event, not the Human, moves the Human off a
    /// Room). The caller registers those beside <see cref="CopyFrom"/>.
    /// </summary>
    internal static readonly Type[] Chat =
    [
        typeof(ITeamDirectory),
        typeof(IChatStore),
        typeof(RoomEvents),
        typeof(ChatService),
        typeof(Drafts),
        typeof(IAgentGateway),
        typeof(PersonaHealth),
        typeof(RoomFollows),

        // Chat.razor renders InviteAgent as a child, which injects PersonaStore for its Team
        // filter - needed even though these tests never open that panel.
        typeof(PersonaStore),

        // Chat.razor injects AvatarStore itself (to subscribe to AvatarsChanged), and its child
        // MessageList resolves every row's avatar through the same singleton (Task 6.1) - needed
        // even though these tests never customise one.
        typeof(AvatarStore),

        // MessageList shows each Agent's context fill on its newest Message, and redraws when it changes.
        typeof(ContextFill),

        // The Room's Mention pills and the Human's own row read the Human and the aliases (ADR-0057's markup).
        typeof(IHumanIdentity),
        typeof(IMentionAliasSource),

        // Chat.razor now renders ProposalCard directly above the Budget prompt (Task 12.1), which
        // injects these three even though these tests never propose an Agent - PersonaStore and
        // RoomEvents are already copied above, for the same reason.
        typeof(ProposalStore),
        typeof(ProposalService),
        typeof(HireLimitGuard),

        // Chat.razor renders SprintProposalCard beside ProposalCard (Task 63), which injects these three even
        // though these tests never propose a Sprint.
        typeof(SprintProposalStore),
        typeof(SprintProposalService),

        // Chat.razor's Composer child also injects TaskStore for the # picker (Task 15.3) - needed even
        // though these tests never open it.
        typeof(TaskStore),

        // Chat.razor renders RetroProposalCard beside them (Task 76), which injects these two.
        typeof(RetroProposalStore),
        typeof(RetroProposalService),

        // Chat.razor renders QuestionCard beside ProposalCard (Questions spec section 6.7), which injects these
        // two even though these tests never ask a Question - RoomEvents, Drafts and ITeamDirectory are
        // already registered above, for Chat.razor's own sake. IOptions<TeamOptions> is the Composer's too.
        typeof(QuestionStore),
        typeof(QuestionService),
        typeof(IOptions<TeamOptions>),

        // Chat.razor renders ElicitationCard beside QuestionCard (elicitation bridge, E-5), which injects
        // these two even though most of these tests never put a form to the Human - RoomEvents and
        // ITeamDirectory are already registered above, for Chat.razor's own sake.
        typeof(ElicitationStore),
        typeof(ElicitationService),

        // Chat.razor draws a Team Room's Team avatar in its header, read from the Team catalog.
        typeof(ITeamCatalog),

        // Chat.razor shows the Room's fresh-start note strip (LR-R21) and the reread text behind it (LR-R22).
        typeof(FreshStartNotes),
        typeof(Agency.Huddle.App.Acp.Sessions.LastCatchUpStore),
    ];

    /// <summary>
    /// What the <c>Agents</c> page and its card inject: the Agent stores, the gateway and health that
    /// drive a card's status, and the catalogs its Skill and adapter pickers read.
    /// </summary>
    internal static readonly Type[] Agents =
    [
        typeof(PersonaStore),
        typeof(ITeamDirectory),
        typeof(IAgentGateway),
        typeof(PersonaHealth),
        typeof(PersonaSpend),
        typeof(PersonaCommands),
        typeof(PersonaPromptPreview),
        typeof(PersonaSupervisor),
        typeof(RoomEvents),
        typeof(IModelCatalog),
        typeof(AdapterCatalog),
        typeof(AvatarStore),
        typeof(SkillStore),
        typeof(BuiltinAgentReset),
    ];

    /// <summary>
    /// What the <c>Settings</c> page injects: the prompt and appearance stores its sections edit, the Skill
    /// store and the Library root. The profile and Skill-pack services it also needs come from
    /// <see cref="ProfileTestServices"/> and <see cref="SkillsTestServices"/>, which the caller adds beside
    /// <see cref="CopyFrom"/>.
    /// </summary>
    internal static readonly Type[] Settings =
    [
        typeof(PromptStore),
        typeof(AppearanceStore),
        typeof(SkillStore),
        typeof(LibraryRootStore),
        typeof(Agency.Huddle.App.Acp.Sessions.SessionSettingsStore),
        typeof(Agency.Huddle.App.Gateway.GatewayAdminService),
        typeof(Agency.Huddle.App.Gateway.IGatewayRuntimeView),
    ];

    /// <summary>Registers, as singletons in <paramref name="services"/>, the instance <paramref name="source"/> holds for each of <paramref name="types"/>.</summary>
    /// <param name="services">The service collection of the bUnit context under construction.</param>
    /// <param name="source">The provider that owns the real services, usually a <see cref="TeamWebApplicationFactory"/>'s.</param>
    /// <param name="types">The service types to copy, for example <see cref="Chat"/>.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A type in <paramref name="types"/> is not registered in <paramref name="source"/>.</exception>
    internal static IServiceCollection CopyFrom(this IServiceCollection services, IServiceProvider source, params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(types);

        foreach (Type type in types)
        {
            services.AddSingleton(type, source.GetRequiredService(type));
        }

        return services;
    }
}
