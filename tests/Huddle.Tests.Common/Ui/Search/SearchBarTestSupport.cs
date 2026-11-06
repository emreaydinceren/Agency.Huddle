using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Pipes;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Services;
using Agency.Huddle.App.Tabs;
using Agency.Huddle.App.Agents;
using Agency.Huddle.Tests.Search;
using Agency.Huddle.Tests.Ui.Layout;
using Agency.Huddle.Tests.Waiting;

namespace Agency.Huddle.Tests.Ui.Search;

/// <summary>
/// The wiring every <c>SearchBarTests</c> test shares: a bUnit context holding a real <see cref="TabService"/>,
/// a <see cref="SearchOrchestrator"/> over one <see cref="GatedSearchProvider"/> per filter (so a test decides
/// when each group answers), and the Agent card opener over a shared test app. It renders the search box
/// beside the popover and dialog providers.
/// </summary>
internal sealed class SearchBarHarness : IAsyncDisposable
{
    /// <summary>The filters that have a provider, in the order All asks for them.</summary>
    public static readonly SearchFilter[] AllFilters =
    [
        SearchFilter.Teams,
        SearchFilter.Agents,
        SearchFilter.Library,
        SearchFilter.Tasks,
        SearchFilter.Rooms,
    ];

    /// <summary>The longest a test waits for the search to reach a provider.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>Creates the context, the providers, the orchestrator and the Tab service.</summary>
    /// <param name="factory">The test app whose Agent services the card opener uses.</param>
    public SearchBarHarness(TeamWebApplicationFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        this.Ctx = new MudBunitContext();
        foreach (SearchFilter filter in AllFilters)
        {
            this.Providers[filter] = new GatedSearchProvider(filter, false);
        }

        this.Ctx.Services.AddSingleton<TabService>();
        this.Ctx.Services.AddSingleton(StubPages.Routes());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaStore>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<ITeamDirectory>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<IAgentGateway>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaHealth>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSpend>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaCommands>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<PersonaSupervisor>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<RoomEvents>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<IModelCatalog>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<AdapterCatalog>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<Agency.Huddle.App.Avatars.AvatarStore>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<Agency.Huddle.App.Skills.SkillStore>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<BuiltinAgentReset>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<WorkModePolicy>());
        this.Ctx.Services.AddSingleton(factory.Services.GetRequiredService<Agency.Huddle.App.Profile.IHumanIdentity>());
        this.Ctx.Services.AddTransient(provider => new AgentProfileCardOpener(
            provider.GetRequiredService<PersonaStore>(),
            provider.GetRequiredService<ITeamDirectory>(),
            provider.GetRequiredService<IAgentGateway>(),
            provider.GetRequiredService<PersonaHealth>(),
            provider.GetRequiredService<IDialogService>()));
        this.Orchestrator = new SearchOrchestrator(this.Providers.Values, NullLogger<SearchOrchestrator>.Instance);
        this.Ctx.Services.AddSingleton(this.Orchestrator);
        this.Tabs = this.Ctx.Services.GetRequiredService<TabService>();
    }

    /// <summary>The bUnit context everything renders in.</summary>
    public MudBunitContext Ctx { get; }

    /// <summary>The orchestrator the search box talks to.</summary>
    public SearchOrchestrator Orchestrator { get; }

    /// <summary>The Tab service the search box opens hits in.</summary>
    public TabService Tabs { get; }

    /// <summary>The gated provider of each filter.</summary>
    public Dictionary<SearchFilter, GatedSearchProvider> Providers { get; } = [];

    /// <summary>Renders the search box with the popover and dialog providers beside it.</summary>
    /// <returns>The rendered fragment holding all three.</returns>
    public IRenderedComponent<ContainerFragment> Render()
    {
        return this.Ctx.RenderWithPopovers(static builder =>
        {
            builder.OpenComponent<Agency.Huddle.App.Components.Search.SearchBar>(0);
            builder.CloseComponent();
        });
    }

    /// <summary>The box's input element.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <returns>The input.</returns>
    public static IElement Input(IRenderedComponent<ContainerFragment> cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        return cut.Find("input[role='combobox']");
    }

    /// <summary>Types the whole text into the box as one input event.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <param name="text">The new box text.</param>
    /// <returns>A task that ends when the component has handled the input.</returns>
    public static Task TypeAsync(IRenderedComponent<ContainerFragment> cut, string text)
    {
        ArgumentNullException.ThrowIfNull(cut);
        return cut.InvokeAsync(() => SearchBarHarness.Input(cut).InputAsync(new ChangeEventArgs { Value = text }));
    }

    /// <summary>Focuses the box, which opens the dropdown.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <returns>A task that ends when the dropdown is open.</returns>
    public static Task OpenAsync(IRenderedComponent<ContainerFragment> cut)
    {
        ArgumentNullException.ThrowIfNull(cut);
        return cut.InvokeAsync(() => SearchBarHarness.Input(cut).FocusInAsync());
    }

    /// <summary>Presses a key in the box.</summary>
    /// <param name="cut">The rendered fragment.</param>
    /// <param name="key">The <c>KeyboardEventArgs.Key</c> value.</param>
    /// <param name="ctrl">Whether Ctrl is held.</param>
    /// <returns>A task that ends when the component has handled the key.</returns>
    public static Task PressAsync(IRenderedComponent<ContainerFragment> cut, string key, bool ctrl = false)
    {
        ArgumentNullException.ThrowIfNull(cut);
        return cut.InvokeAsync(() => SearchBarHarness.Input(cut).KeyDownAsync(new KeyboardEventArgs { Key = key, CtrlKey = ctrl }));
    }

    /// <summary>Answers the Active and then the Archived call of each filter, with the given hits on the Active side only.</summary>
    /// <param name="filters">The filters whose providers were asked.</param>
    /// <param name="hits">The hits each filter answers with; a filter missing here answers with none.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>A task that ends when every call has been answered.</returns>
    public async Task FinishAsync(IEnumerable<SearchFilter> filters, IReadOnlyDictionary<SearchFilter, SearchHit[]> hits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(hits);

        SearchFilter[] selected = [.. filters];
        foreach (SearchScope scope in new[] { SearchScope.Active, SearchScope.Archived })
        {
            foreach (SearchFilter filter in selected)
            {
                SearchHit[] answer = scope == SearchScope.Active && hits.TryGetValue(filter, out SearchHit[]? found) ? found : [];
                GatedCall call = await this.NextOpenCallAsync(filter, scope, ct);
                call.Complete(new SearchResult(answer, false));
            }
        }
    }

    /// <summary>Waits for the newest call of a provider on one side that is still pending.</summary>
    /// <param name="filter">The provider's filter.</param>
    /// <param name="scope">The side of the pass.</param>
    /// <param name="ct">The test's cancellation token.</param>
    /// <returns>The call, ready to be completed.</returns>
    public async Task<GatedCall> NextOpenCallAsync(SearchFilter filter, SearchScope scope, CancellationToken ct)
    {
        GatedCall? call = await Eventually.ValueAsync<GatedCall?>(
            _ => Task.FromResult(this.Providers[filter].Calls.LastOrDefault(candidate =>
                candidate.Query.Scope == scope && !candidate.Token.IsCancellationRequested && !candidate.Completion.Task.IsCompleted)),
            candidate => candidate is not null,
            ct,
            timeout: Patience,
            interval: TimeSpan.FromMilliseconds(10),
            what: $"a {filter} provider asked on the {scope} side");
        Assert.NotNull(call);
        return call;
    }

    /// <summary>Raises a group update on the orchestrator's event as a pool thread would, without a search behind it.</summary>
    /// <param name="update">The update to raise.</param>
    public void Raise(SearchGroupUpdate update)
    {
        System.Reflection.FieldInfo field = typeof(SearchOrchestrator).GetField(nameof(SearchOrchestrator.GroupUpdated), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The GroupUpdated backing field was not found.");
        Action<SearchGroupUpdate>? handlers = (Action<SearchGroupUpdate>?)field.GetValue(this.Orchestrator);
        handlers?.Invoke(update);
    }

    /// <summary>The number of subscribers on the orchestrator's <c>GroupUpdated</c> event.</summary>
    /// <returns>The count.</returns>
    public int SubscriberCount()
    {
        System.Reflection.FieldInfo field = typeof(SearchOrchestrator).GetField(nameof(SearchOrchestrator.GroupUpdated), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The GroupUpdated backing field was not found.");
        return ((Delegate?)field.GetValue(this.Orchestrator))?.GetInvocationList().Length ?? 0;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await this.Ctx.DisposeAsync();
        this.Orchestrator.Dispose();
    }
}
