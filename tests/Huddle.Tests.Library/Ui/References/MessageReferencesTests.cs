using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TestContext = Xunit.TestContext;
using Agency.Huddle.App.Components.References;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Tasks;
using Agency.Huddle.App.Teams;
using Agency.Huddle.Contracts;
using Agency.Huddle.Tests.References;
using Agency.Huddle.Tests.Tasks;

namespace Agency.Huddle.Tests.Ui.References;

/// <summary>
/// An <see cref="ITeamCatalog"/> whose every member throws. It proves the Room view never throws over a snapshot
/// (spec §8.4): the lookup fails, the row draws nothing and the failure is logged.
/// </summary>
public class ThrowingTeamCatalog : DispatchProxy
{
    /// <summary>Creates the catalog that throws on every call.</summary>
    /// <returns>The catalog to hand the resolver.</returns>
    internal static ITeamCatalog Create() => Create<ITeamCatalog, ThrowingTeamCatalog>();

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        throw new InvalidOperationException("The team catalog is unavailable.");
    }
}

/// <summary>
/// The snapshot chips a Message's reference row draws in the Room view (spec §8.4, D-15, AT-T14): nothing for an
/// unchanged target, a dimmed unavailable chip with its tooltip for a gone one, and a changed chip that links to the
/// current target and says what it was when sent.
/// </summary>
public sealed class MessageReferencesTests
{
    private static readonly DateTimeOffset Sent = DateTimeOffset.UnixEpoch;

    private static TaskItem PlatformTask() => TestTasks.Make(
        id: "PLAT-0001",
        title: "Fix login",
        status: TaskState.InProgress,
        assignee: "Nova",
        location: new TaskLocation("Platform", null, false));

    /// <summary>A Message carrying the given references, as it was sent.</summary>
    /// <param name="references">The snapshot entries.</param>
    /// <returns>The Message.</returns>
    private static ChatMessage MessageWith(params MessageReference[] references) =>
        new("m-1", MessageReferencesTests.Sent, "u-1", "echo", "see above") { References = references };

    /// <summary>Renders the reference row for a Message against the fixture's stores.</summary>
    /// <param name="ctx">The bUnit context to render in.</param>
    /// <param name="fixture">The stores the resolver reads.</param>
    /// <param name="message">The Message whose snapshot is drawn.</param>
    /// <param name="catalog">The Team list, or null for the fixture's own.</param>
    /// <returns>The rendered component.</returns>
    private static IRenderedComponent<MessageReferences> Render(MudBunitContext ctx, ReferenceFixture fixture, ChatMessage message, ITeamCatalog? catalog = null)
    {
        ctx.Services.AddSingleton(fixture.Resolver(catalog));
        return ctx.Render<MessageReferences>(parameters => parameters.Add(p => p.Message, message));
    }

    /// <summary>A Message with no snapshot (sent before references existed) draws no row.</summary>
    [Fact]
    public async Task NoSnapshot_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = new("m-1", MessageReferencesTests.Sent, "u-1", "echo", "plain");

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>A Room and a Task that are both unchanged draw no chip.</summary>
    [Fact]
    public async Task AllUnchanged_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(
            new MessageReference(ReferenceKind.Task, "PLAT-0001", "PLAT-0001", "Fix login", "InProgress"),
            new MessageReference(ReferenceKind.Room, room.Id, "Launch"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>A Task whose only change is its status is not changed, so it draws no chip (spec §8.4).</summary>
    [Fact]
    public async Task TaskStatusOnlyChanged_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Task, "PLAT-0001", "PLAT-0001", "Fix login", "Todo"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>A Task that no longer exists draws a dimmed, unlinked chip with the Task tooltip.</summary>
    [Fact]
    public async Task GoneTask_DimmedChipWithTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        _ = fixture.Prefixes.PrefixFor("Platform");
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Task, "PLAT-0009", "PLAT-0009", "Old task", "Todo"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal("PLAT-0009 (unavailable)", cut.Find(".message-reference-gone").TextContent.Trim());
        Assert.Equal("This Task no longer exists.", cut.Find(".message-reference-gone-wrap").GetAttribute("title"));
        Assert.DoesNotContain("href=", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A Room that no longer exists draws the dimmed chip with the Room tooltip.</summary>
    [Fact]
    public async Task GoneRoom_DimmedChipWithTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Room, "room-gone", "Launch"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal("Launch (unavailable)", cut.Find(".message-reference-gone").TextContent.Trim());
        Assert.Equal("This Room no longer exists.", cut.Find(".message-reference-gone-wrap").GetAttribute("title"));
    }

    /// <summary>A Team that no longer exists draws the dimmed chip with the Team tooltip.</summary>
    [Fact]
    public async Task GoneTeam_DimmedChipWithTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Team, "Marketing", "Marketing"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal("Marketing (unavailable)", cut.Find(".message-reference-gone").TextContent.Trim());
        Assert.Equal("This Team no longer exists.", cut.Find(".message-reference-gone-wrap").GetAttribute("title"));
    }

    /// <summary>A Library file that is no longer in the Library draws the dimmed chip with the file tooltip.</summary>
    [Fact]
    public async Task GoneFile_DimmedChipWithTooltip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.LibraryFile, "/nowhere/Library/report.pdf", "report.pdf"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal("report.pdf (unavailable)", cut.Find(".message-reference-gone").TextContent.Trim());
        Assert.Equal("This file is no longer in the Library.", cut.Find(".message-reference-gone-wrap").GetAttribute("title"));
    }

    /// <summary>A renamed Room draws a changed chip with the current name, linking to the Room by its id.</summary>
    [Fact]
    public async Task RenamedRoom_ChangedChipLinksToRoom()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Plan", [KnownIds.Human, fixture.Nova.Id], ct);
        await fixture.Directory.RenameRoomAsync(room.Id, "Launch", ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Room, room.Id, "Plan"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        IElement link = cut.Find(".message-references a");
        Assert.Equal("Launch", link.TextContent);
        Assert.Equal("/rooms/" + room.Id, link.GetAttribute("href"));
        Assert.Equal("Was 'Plan' when sent.", link.GetAttribute("title"));
    }

    /// <summary>A scoped Room whose label is still the same draws no chip, though its Scope is in the label.</summary>
    [Fact]
    public async Task ScopedRoomUnchanged_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Nova", [KnownIds.Human, fixture.Nova.Id], team: "Marketing", ct: ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Room, room.Id, "Nova (Marketing)"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>An archived Room still exists, so it draws no gone chip and, when its label is the same, no chip at all.</summary>
    [Fact]
    public async Task ArchivedRoom_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Launch", [KnownIds.Human, fixture.Nova.Id], ct);
        await fixture.Directory.SetRoomArchivedAsync(room.Id, true, ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Room, room.Id, "Launch"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }

    /// <summary>An upload whose Room was deleted is recycled with it, so a reference to it shows the file-gone chip.</summary>
    [Fact]
    public async Task UploadOfADeletedRoom_UnavailableChip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.LibraryFile, "/nowhere/uploads/room-gone/notes.txt", "notes.txt"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        Assert.Equal("notes.txt (unavailable)", cut.Find(".message-reference-gone").TextContent.Trim());
        Assert.Equal("This file is no longer in the Library.", cut.Find(".message-reference-gone-wrap").GetAttribute("title"));
    }

    /// <summary>A retitled Task draws a changed chip with the current title, linking to the Task.</summary>
    [Fact]
    public async Task RetitledTask_ChangedChip()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct, null, PlatformTask());
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Task, "PLAT-0001", "PLAT-0001", "Login bug", "InProgress"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);

        IElement link = cut.Find(".message-references a");
        Assert.Equal("Fix login", link.TextContent);
        Assert.Equal("/tasks/item/PLAT-0001", link.GetAttribute("href"));
        Assert.Equal("Was 'Login bug' when sent.", link.GetAttribute("title"));
    }

    /// <summary>The lookup is made once per Message: re-rendering the same Message keeps the chip of the first lookup, not a fresh one (plan L9, cached by Message id).</summary>
    [Fact]
    public async Task SameMessageReRendered_KeepsTheFirstLookup()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        Room room = await fixture.Directory.CreateRoomAsync("Plan", [KnownIds.Human, fixture.Nova.Id], ct);
        await fixture.Directory.RenameRoomAsync(room.Id, "Launch", ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Room, room.Id, "Plan"));
        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message);
        Assert.Equal("Launch", cut.Find(".message-references a").TextContent);

        await fixture.Directory.RenameRoomAsync(room.Id, "Final", ct);
        cut.Render(parameters => parameters.Add(p => p.Message, message));

        Assert.Equal("Launch", cut.Find(".message-references a").TextContent);
    }

    /// <summary>A lookup that throws draws nothing; the Room view never throws over a snapshot (spec §8.4).</summary>
    [Fact]
    public async Task ThrowingResolver_RendersNothing()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        using ReferenceFixture fixture = await ReferenceFixture.BuildAsync(ct);
        await using MudBunitContext ctx = new();
        ChatMessage message = MessageWith(new MessageReference(ReferenceKind.Team, "Marketing", "Marketing"));

        IRenderedComponent<MessageReferences> cut = Render(ctx, fixture, message, ThrowingTeamCatalog.Create());

        Assert.Equal(string.Empty, cut.Markup.Trim());
    }
}
