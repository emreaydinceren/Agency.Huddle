namespace Agency.Huddle.Tests.Ui.References;

using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Agency.Huddle.App;
using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.Tests.Acp.Tools;
using Agency.Huddle.Tests.Search;

/// <summary>
/// Pins the Blazor side of the Reference picker in the <see cref="Composer"/> (AT-T11, spec §5.1, §5.4, §5.5, §6, §11,
/// plan T7.2): the <c>@</c> and <c>#</c> queries, the combobox roles, the text a pick returns per kind, the move
/// that wraps, the close on a spaced token with no hits, the <c>/invite</c> pin, Tasks off, and the picker's own
/// search being disposed with the Composer. The script side is pinned by <c>AppJsSourceTests</c>.
/// </summary>
public sealed class ComposerReferencePickerTests
{
    private static readonly TeammateHit emily = new("Emily Lee", "Designer", "emily", [], MatchTier.NamePrefix);
    private static readonly TaskHit task = new(new TaskId("PLAT", 1), "Sample import", TaskState.ToDo, null, "Platform", false, MatchTier.NamePrefix);
    private static readonly RoomHit room = new("room-1", "Findings", 0, null, null, null, null, false, MatchTier.NamePrefix);
    private static readonly TeamHit team = new("Growth Ops", null, 2, MatchTier.NamePrefix);

    /// <summary>An <c>@</c> query renders one <c>role="group"</c> per group that has rows, labelled by its heading.</summary>
    [Fact]
    public async Task AtQuery_RendersGroups()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("e", "@", false));

        cut.WaitForAssertion(() => Assert.Equal(
            ["Teammates", "Tasks", "Rooms", "Teams"],
            cut.FindAll("[role='group']").Select(static group => group.GetAttribute("aria-label")).ToArray()));
        Assert.Equal("true", cut.Find("textarea").GetAttribute("aria-expanded"));
    }

    /// <summary>A <c>#</c> query shows only the Tasks group, and with no Task it keeps today's text.</summary>
    [Fact]
    public async Task HashQuery_ShowsOnlyTasks_WithTodaysEmptyText()
    {
        using TaskToolHarness harness = new();
        EmptyProvider tasks = new(SearchFilter.Tasks);
        await using MudBunitContext ctx = NewContext(harness, [tasks, new StubProvider(SearchFilter.Teammates, emily)]);
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("zzz", "#", false));

        cut.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement[] options = [.. cut.FindAll("[role='option']")];
            Assert.Equal("No task matches '#zzz'", Assert.Single(options).TextContent.Trim());
        });
        Assert.Empty(cut.FindAll("[role='group']"));
    }

    /// <summary>The textarea is a combobox with list autocomplete (spec §11, OP-10).</summary>
    [Fact]
    public async Task Textarea_HasComboboxRoleAndAutocomplete()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());

        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);

        AngleSharp.Dom.IElement textarea = cut.Find("textarea");
        Assert.Equal("combobox", textarea.GetAttribute("role"));
        Assert.Equal("list", textarea.GetAttribute("aria-autocomplete"));
    }

    /// <summary>A pick returns the spec §6 insert text of the highlighted row, for each kind.</summary>
    [Fact]
    public async Task Pick_ReturnsInsertText_EachKind()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        Dictionary<string, string?> picked = [];
        foreach (string prefix in new[] { "teammate:", "task:", "room:", "team:" })
        {
            await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync(prefix, "@", false));
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='option']")));
            picked[prefix] = await cut.InvokeAsync(() => composer.Instance.PickAsync());
        }

        Assert.Equal("@Emily Lee", picked["teammate:"]);
        Assert.Equal("PLAT-0001", picked["task:"]);
        Assert.Equal("[Findings](/rooms/room-1)", picked["room:"]);
        Assert.Equal("[Growth Ops](/teams/Growth%20Ops)", picked["team:"]);
    }

    /// <summary>A pick with no rows returns null and closes the picker, so Enter never sends.</summary>
    [Fact]
    public async Task Pick_NoRows_ReturnsNull()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("task:zzz", "@", false));
        cut.WaitForAssertion(() => Assert.Equal("Nothing matches '@task:zzz'.", Assert.Single(cut.FindAll("[role='option']")).TextContent.Trim()));

        string? picked = await cut.InvokeAsync(() => composer.Instance.PickAsync());

        Assert.Null(picked);
        Assert.Empty(cut.FindAll("[role='listbox']"));
    }

    /// <summary>The highlight wraps at both ends over the rows only, never landing on a heading or the footer.</summary>
    [Fact]
    public async Task Move_Wraps_SkipsHeadings()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();
        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("e", "@", false));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("[role='option']").Count));

        await cut.InvokeAsync(() => composer.Instance.MoveAsync(-1));
        string? last = cut.Find("[aria-selected='true']").GetAttribute("id");
        await cut.InvokeAsync(() => composer.Instance.MoveAsync(1));
        string? first = cut.Find("[aria-selected='true']").GetAttribute("id");

        string? listbox = cut.Find("[role='listbox']").GetAttribute("id");
        Assert.Equal($"{listbox}-3", last);
        Assert.Equal($"{listbox}-0", first);
        Assert.Equal(first, cut.Find("textarea").GetAttribute("aria-activedescendant"));
    }

    /// <summary>A token with a space and no hits closes the picker and tells the script through <c>teamComposer.pickerClosed</c>.</summary>
    [Fact]
    public async Task SpacedTokenWithNoHits_Closes()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, [new StubProvider(SearchFilter.Teammates)]);
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("Nova thanks for", "@", false));

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role='listbox']")));
        Assert.Equal("false", cut.Find("textarea").GetAttribute("aria-expanded"));
        _ = ctx.JSInterop.VerifyInvoke("teamComposer.pickerClosed");
    }

    /// <summary>A filter prefix followed by a space (<c>lib: </c>, <c>lib: my</c>) is not a Name with a space: the search is not yet possible, so the picker stays open and never tells the script it closed (spec §5.1, §5.5).</summary>
    /// <param name="token">The token as the script reports it.</param>
    [Theory]
    [InlineData("lib: ")]
    [InlineData("lib: my")]
    public async Task FilterPrefixThenSpace_StaysOpen(string token)
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync(token, "@", false));
        await composer.Instance.PickerSettled.WaitAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("true", cut.Find("textarea").GetAttribute("aria-expanded"));
        ctx.JSInterop.VerifyNotInvoke("teamComposer.pickerClosed");
    }

    /// <summary>Prose after a searchable filter prefix (<c>team: standup in 5 min</c>) with no row closes the picker, so the next Enter sends instead of needing a second one.</summary>
    /// <param name="token">The token as the script reports it.</param>
    [Theory]
    [InlineData("team: standup in 5 min")]
    [InlineData("task: zzz here")]
    public async Task FilterPrefixWithProseAndNoRows_Closes(string token)
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, [new StubProvider(SearchFilter.Teams), new StubProvider(SearchFilter.Tasks)]);
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync(token, "@", false));
        await composer.Instance.PickerSettled.WaitAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("false", cut.Find("textarea").GetAttribute("aria-expanded"));
        _ = ctx.JSInterop.VerifyInvoke("teamComposer.pickerClosed");
    }

    /// <summary>After <c>/invite </c> the picker lists the Teammates group alone.</summary>
    [Fact]
    public async Task InviteCommand_PinsTeammates()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All());
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("", "@", true));

        cut.WaitForAssertion(() => Assert.Equal(
            ["Teammates"],
            cut.FindAll("[role='group']").Select(static group => group.GetAttribute("aria-label")).ToArray()));
    }

    /// <summary>With Tasks off, a <c>#</c> query never opens the picker.</summary>
    [Fact]
    public async Task TasksDisabled_HashNeverOpens()
    {
        using TaskToolHarness harness = new();
        await using MudBunitContext ctx = NewContext(harness, All(), tasksEnabled: false);
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();

        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("sa", "#", false));

        Assert.Empty(cut.FindAll("[role='listbox']"));
    }

    /// <summary>Disposing the Composer cancels the picker's own search in flight.</summary>
    [Fact]
    public async Task PickerSearch_IsDisposedWithTheComposer()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        using TaskToolHarness harness = new();
        GatedSearchProvider gated = new(SearchFilter.Teammates, isExpensive: false);
        await using MudBunitContext ctx = NewContext(harness, [gated]);
        IRenderedComponent<ContainerFragment> cut = RenderComposer(ctx);
        IRenderedComponent<Composer> composer = cut.FindComponent<Composer>();
        await cut.InvokeAsync(() => composer.Instance.ReferenceQueryAsync("e", "@", false));
        IReadOnlyList<GatedCall> calls = await gated.WaitForCallsAsync(1, ct);
        Assert.False(calls[0].Token.IsCancellationRequested);

        // The context (and its container, which would also dispose a transient) stays alive: only the Composer's own Dispose can cancel.
        composer.Instance.Dispose();

        Assert.True(calls[0].Token.IsCancellationRequested);
    }

    /// <summary>One stub provider per group, each with a hit that the picker can pick.</summary>
    private static ISearchProvider[] All() =>
    [
        new StubProvider(SearchFilter.Teammates, emily),
        new StubProvider(SearchFilter.Tasks, task),
        new StubProvider(SearchFilter.Rooms, room),
        new StubProvider(SearchFilter.Teams, team),
    ];

    /// <summary>Renders a bare <see cref="Composer"/> in a Room.</summary>
    private static IRenderedComponent<ContainerFragment> RenderComposer(MudBunitContext ctx) =>
        ctx.RenderWithPopovers(builder =>
        {
            builder.OpenComponent<Composer>(0);
            builder.AddAttribute(1, nameof(Composer.RoomId), "any-room");
            builder.CloseComponent();
        });

    /// <summary>A context with the Composer's services and the picker's own search over <paramref name="providers"/>.</summary>
    private static MudBunitContext NewContext(TaskToolHarness harness, ISearchProvider[] providers, bool tasksEnabled = true)
    {
        MudBunitContext ctx = new();
        ctx.Services.AddSingleton(harness.Chat);
        ctx.Services.AddSingleton(harness.Store);
        ctx.Services.AddSingleton(harness.Events);
        ctx.Services.AddSingleton(harness.Avatars);
        TeamOptions options = new() { Tasks = new TasksOptions { Enabled = tasksEnabled } };
        ctx.Services.AddSingleton<IOptions<TeamOptions>>(Options.Create(options));
        ctx.Services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        foreach (ISearchProvider provider in providers)
        {
            ctx.Services.AddSingleton(provider);
        }

        ctx.Services.AddSingleton<ReferenceSearchFactory>();
        return ctx;
    }

    /// <summary>A provider that answers every query with the same hits, for any phrase.</summary>
    /// <param name="group">The group it serves.</param>
    /// <param name="hits">The hits it returns on the active pass; the archived pass is empty.</param>
    private sealed class StubProvider(SearchFilter group, params SearchHit[] hits) : ISearchProvider
    {
        /// <inheritdoc/>
        public SearchFilter Group { get; } = group;

        /// <inheritdoc/>
        public bool IsExpensive => false;

        /// <inheritdoc/>
        public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct) =>
            Task.FromResult(query.Scope == SearchScope.Active && !query.Text.Contains("zzz", StringComparison.Ordinal)
                ? new SearchResult(hits, false)
                : SearchResult.Empty);
    }

    /// <summary>A provider that never has a hit.</summary>
    /// <param name="group">The group it serves.</param>
    private sealed class EmptyProvider(SearchFilter group) : ISearchProvider
    {
        /// <inheritdoc/>
        public SearchFilter Group { get; } = group;

        /// <inheritdoc/>
        public bool IsExpensive => false;

        /// <inheritdoc/>
        public Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct) => Task.FromResult(SearchResult.Empty);
    }
}
