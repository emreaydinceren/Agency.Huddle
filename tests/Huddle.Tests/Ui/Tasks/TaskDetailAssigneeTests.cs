namespace Agency.Huddle.Tests.Ui.Tasks;

using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.Components.Tasks;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using static Agency.Huddle.Tests.Ui.Tasks.TaskDetailFieldsTestSupport;

/// <summary>
/// Pins Spec §13.6's Assignee row (retro R7 scope addendum) at both <c>TaskDetail</c> entry points - split out of <see cref="TaskDetailFieldsTests"/> so xUnit can run the classes in parallel.
/// </summary>
public sealed class TaskDetailAssigneeTests
{
    // Assignee (Spec §13.6; scope addendum R7).
    // ---------------------------------------------------------------------------------------------

    /// <summary>The Assignee picker is a <c>Strict</c> autocomplete (Spec §13.6) - this alone forces the new <c>AssigneeOption</c> item type's red.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Assignee_AutocompleteIsStrict(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));

        Assert.True(assignee.Instance.Strict);
    }

    /// <summary>
    /// Settled (R7 addendum, J46, assigned to 14.2 explicitly): an Assignee option's <c>ItemTemplate</c>
    /// renders a <c>TeammateAvatar</c> nested inside a presence <c>MudBadge</c> whose <c>BadgeAriaLabel</c>
    /// states the presence in words - the exact <c>TaskCard.razor</c> wording, "{Name} is {Presence}" - and
    /// the option's own Name text exactly. Renders the template directly from a real
    /// <see cref="AssigneeOption"/> returned by the autocomplete's own <c>SearchFunc</c> (never
    /// hand-constructed), rather than opening the popover, since bUnit cannot easily drive a debounced
    /// popover open from a plain text change.
    /// </summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Assignee_OptionTemplate_ShowsTeammateAvatarInPresenceBadge_AndExactName(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search("Nova", Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        AssigneeOption nova = (await searchTask).Single();
        RenderFragment<AssigneeOption>? itemTemplate = assignee.Instance.ItemTemplate;
        Assert.NotNull(itemTemplate);

        var itemCut = ctx.Render(itemTemplate!(nova));

        IRenderedComponent<MudBadge> badge = itemCut.FindComponent<MudBadge>();
        IRenderedComponent<TeammateAvatar> avatar = badge.FindComponent<TeammateAvatar>();
        Assert.Equal("Nova", avatar.Instance.Name);
        Assert.Equal("Nova is Asleep", badge.Instance.BadgeAriaLabel);
        Assert.Equal("Nova", TextOf(itemCut, ".task-detail-assignee-option-name"));
    }

    /// <summary>Selecting a known Persona from the Assignee autocomplete's own <c>SearchFunc</c> results sets the pending Assignee, proven by saving it (never constructing an <c>AssigneeOption</c> by hand - its shape is 14.2.i's own decision).</summary>
    [Fact]
    public async Task Assignee_SelectingFromSearchFuncResults_SetsAssignee_ProvenBySaving()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        harness.Gateway.SetOnline(harness.NovaId ?? throw new InvalidOperationException("NovaId missing"));
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search("Nova", Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        AssigneeOption nova = (await searchTask).Single();

        await cut.InvokeAsync(() => assignee.Instance.ValueChanged.InvokeAsync(nova));
        await cut.InvokeAsync(() => FindButton(cut, "Save & Notify Nova").ClickAsync());

        Assert.Equal("Nova", harness.Store.Get(task.Id)?.Assignee);
    }

    /// <summary>
    /// The Human is a hardcoded candidate in the Assignee autocomplete's own <c>SearchFunc</c>, ahead of
    /// every Persona, so the Human can assign a Task to themselves - there was previously no way to.
    /// Selecting it sets the pending Assignee to <see cref="Agency.Huddle.App.TeamOptions.HumanName"/> itself (the value
    /// <c>TaskService.ResolveAssignee</c> compares against and stores, not a "Me" sentinel), proven by
    /// saving it. <see cref="WakeBlock.AssigneeIsHuman"/> also means the button reads plain "Save".
    /// </summary>
    [Fact]
    public async Task Assignee_HumanIsAlwaysAnOption_SelectingItSetsAssigneeToHumanName_ProvenBySaving()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search(null, Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        string humanName = harness.Options.Value.HumanName;
        AssigneeOption human = (await searchTask).Single(option => string.Equals(option.Name, humanName, StringComparison.Ordinal));

        await cut.InvokeAsync(() => assignee.Instance.ValueChanged.InvokeAsync(human));
        await cut.InvokeAsync(() => FindButton(cut, "Save").ClickAsync());

        Assert.Equal(humanName, harness.Store.Get(task.Id)?.Assignee);
    }

    /// <summary>
    /// The Human's Assignee option renders with no presence badge at all - unlike a Persona's, whose
    /// <c>ItemTemplate</c> branch always draws a <c>MudBadge</c> even when presence can't be resolved
    /// (Spec §13.6's null fallback to an Offline-styled badge). Awake/Asleep/Offline describes a
    /// Persona's Agent process; showing it for the Human, who is by definition using the tool right now,
    /// was reported as nonsensical and confirmed fixed here.
    /// </summary>
    [Fact]
    public async Task Assignee_HumanOptionTemplate_ShowsNoPresenceBadge()
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness);
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, TaskDetailMode.Panel);
        IRenderedComponent<MudAutocomplete<AssigneeOption>> assignee = cut.FindComponents<MudAutocomplete<AssigneeOption>>().Single(m => string.Equals(m.Instance.Class, "task-detail-assignee", StringComparison.Ordinal));
        Func<string?, CancellationToken, Task<IEnumerable<AssigneeOption>>?> search = assignee.Instance.SearchFunc
            ?? throw new InvalidOperationException("Assignee autocomplete has no SearchFunc.");
        Task<IEnumerable<AssigneeOption>>? searchTask = search(null, Xunit.TestContext.Current.CancellationToken);
        if (searchTask is null)
        {
            throw new InvalidOperationException("SearchFunc returned null.");
        }

        string humanName = harness.Options.Value.HumanName;
        AssigneeOption human = (await searchTask).Single(option => string.Equals(option.Name, humanName, StringComparison.Ordinal));
        RenderFragment<AssigneeOption>? itemTemplate = assignee.Instance.ItemTemplate;
        Assert.NotNull(itemTemplate);

        var itemCut = ctx.Render(itemTemplate!(human));

        Assert.Empty(itemCut.FindComponents<MudBadge>());
        Assert.Equal(humanName, itemCut.FindComponent<TeammateAvatar>().Instance.Name);
        Assert.Equal(humanName, TextOf(itemCut, ".task-detail-assignee-option-name"));
    }

    /// <summary>When the Task has a <c>LastWake</c>, a line underneath the Assignee reads exactly "Woken in Room: {name}" (Spec §13.6) and links to the Room.</summary>
    /// <param name="mode">Both entry points.</param>
    [Theory]
    [MemberData(nameof(TaskDetailFieldsTestSupport.BothModesData), MemberType = typeof(TaskDetailFieldsTestSupport))]
    public async Task Assignee_WithLastWake_ShowsWokenInRoomExactText(TaskDetailMode mode)
    {
        using TaskToolHarness harness = await TaskToolHarness.CreateAsync(Xunit.TestContext.Current.CancellationToken);
        TaskItem task = CreateTask(harness, assignee: "Nova");
        harness.TaskActivity.Record(new WakeRecord(task.Id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));
        await using MudBunitContext ctx = NewContext(harness);
        var cut = RenderDetail(ctx, task.Id, mode);

        Assert.Equal("Woken in Room: SAML Integration", TextOf(cut, ".task-detail-woken-link"));
        Assert.Equal("/rooms/room-1", cut.Find(".task-detail-woken-link").GetAttribute("href"));
    }

}
