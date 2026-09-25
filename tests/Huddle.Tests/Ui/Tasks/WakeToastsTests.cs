namespace Agency.Huddle.Tests.Ui.Tasks;

using AngleSharp.Dom;
using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using Agency.Huddle.App;
using Agency.Huddle.App.Tasks;

/// <summary>
/// Pins Spec §13.8's toast rules: <see cref="WakeOutcome.Woken"/> adds one info toast keyed
/// <c>wake:{id}</c> and a burst of wakes for the same Task still shows one; <see cref="WakeOutcome.BudgetSpent"/>
/// adds a warning toast with the exact Spec §10.5 wording and its own Room link and
/// <c>wake-warn:{id}</c> key (corrections-B7 14.5.i item 3 - distinct from Woken's, so the two never
/// collapse into each other); <see cref="WakeOutcome.Failed"/> adds a warning toast with the
/// delivery manager's settled wording (J38) since <see cref="WakeRecord"/> carries no error text of
/// its own, plus a Room link when a Room was chosen; <see cref="WakeOutcome.Offline"/> and
/// <see cref="WakeOutcome.WakePaused"/> add none, because §13.8 shows those only in the Task panel;
/// a <see cref="WakeRecord"/> with no <c>RoomId</c> shows no Room link; and the component guards
/// against firing into a disposed page.
/// </summary>
public sealed class WakeToastsTests
{
    /// <summary>A <see cref="WakeOutcome.Woken"/> record adds exactly one info toast with the exact Spec §13.8 wording.</summary>
    [Fact]
    public async Task Woken_AddsOneInfoToast_WithTheSpecWording()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            Snackbar shown = snackbar.ShownSnackbars.Single();
            Assert.Equal(Severity.Info, shown.Severity);
            // The Woken toast's content is a RenderFragment (it also carries Task and Room links),
            // so Snackbar.Message is null; the text lives in the toast's own .wake-toast-text span.
            cut.WaitForAssertion(() => Assert.Equal("Nova woken for PLAT-0042 in Room: SAML Integration", cut.Find(".wake-toast-text").TextContent.Trim()));
        }
    }

    /// <summary>Two <see cref="WakeOutcome.Woken"/> records for the same Task in a row still show one toast (the <c>wake:{id}</c> key collapses the duplicate).</summary>
    [Fact]
    public async Task Woken_TwiceInARowForTheSameTask_StillShowsOneToast()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));
            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow.AddSeconds(1)));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
        }
    }

    /// <summary>A <see cref="WakeOutcome.BudgetSpent"/> record adds a warning toast with the exact Spec §10.5 wording.</summary>
    [Fact]
    public async Task BudgetSpent_AddsOneWarningToast_WithTheSpecWording()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.BudgetSpent, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            Snackbar shown = snackbar.ShownSnackbars.Single();
            Assert.Equal(Severity.Warning, shown.Severity);
            // BudgetSpent always carries a Room link, so its content is a RenderFragment and
            // Snackbar.Message is null - read the text from the toast's own .wake-toast-text span.
            cut.WaitForAssertion(() => Assert.Equal("Couldn't wake Nova: Room SAML is paused. Open the Room to continue", cut.Find(".wake-toast-text").TextContent.Trim()));
        }
    }

    /// <summary>A <see cref="WakeOutcome.BudgetSpent"/> record adds a Room link (corrections-B7 14.5.i item 3, fixing the defect where it had none).</summary>
    [Fact]
    public async Task BudgetSpent_AddsARoomLink()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.BudgetSpent, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".wake-toast-room-link")));
            IElement roomLink = cut.Find(".wake-toast-room-link");
            Assert.Equal("SAML", roomLink.TextContent.Trim());
            Assert.Equal("/rooms/room-1", roomLink.GetAttribute("href"));
        }
    }

    /// <summary>
    /// A <see cref="WakeOutcome.Woken"/> and a <see cref="WakeOutcome.BudgetSpent"/> for the same Task
    /// show as two separate toasts, not one: corrections-B7 14.5.i item 3 gives BudgetSpent its own
    /// "wake-warn:{id}" key, distinct from Woken's "wake:{id}", specifically so the two kinds of
    /// burst (kept being woken vs. kept hitting the spent budget) never collapse into each other.
    /// </summary>
    [Fact]
    public async Task Woken_ThenBudgetSpent_SameTask_ShowsTwoSeparateToasts()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow));
            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.BudgetSpent, DateTimeOffset.UtcNow.AddSeconds(1)));

            cut.WaitForAssertion(() => Assert.Equal(2, snackbar.ShownSnackbars.Count()));
        }
    }

    /// <summary>
    /// A <see cref="WakeOutcome.Failed"/> record adds a warning toast with exactly
    /// <c>"Couldn't wake {Name}; see the log for details."</c> (settled by the delivery manager,
    /// J38: Spec §10.5 says only "the error text, logged", but <see cref="WakeRecord"/> carries no
    /// error text to show).
    /// </summary>
    [Fact]
    public async Task Failed_WithNoRoomId_AddsOneWarningToast_WithTheSettledWording()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", null, null, WakeOutcome.Failed, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            Snackbar shown = snackbar.ShownSnackbars.Single();
            Assert.Equal(Severity.Warning, shown.Severity);
            Assert.Equal("Couldn't wake Nova; see the log for details.", shown.Message);
        }
    }

    /// <summary>A <see cref="WakeOutcome.Failed"/> record with a <c>RoomId</c> keeps the settled wording and adds a Room link (J38).</summary>
    [Fact]
    public async Task Failed_WithARoomId_KeepsTheWordingAndAddsARoomLink()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.Failed, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            Snackbar shown = snackbar.ShownSnackbars.Single();
            Assert.Equal(Severity.Warning, shown.Severity);
            // A Room was chosen, so this toast's content is a RenderFragment (text plus the Room
            // link) and Snackbar.Message is null - read the text from .wake-toast-text.
            cut.WaitForAssertion(() => Assert.Equal("Couldn't wake Nova; see the log for details.", cut.Find(".wake-toast-text").TextContent.Trim()));
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".wake-toast-room-link")));
            IElement roomLink = cut.Find(".wake-toast-room-link");
            Assert.Equal("SAML", roomLink.TextContent.Trim());
            Assert.Equal("/rooms/room-1", roomLink.GetAttribute("href"));
        }
    }

    /// <summary>A <see cref="WakeOutcome.Offline"/> record adds no toast: Spec §10.5 shows Offline as a notice in the Task panel, not a toast.</summary>
    [Fact]
    public async Task Offline_AddsNoToast()
    {
        (MudBunitContext ctx, _, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.Offline, DateTimeOffset.UtcNow));

            Assert.Empty(snackbar.ShownSnackbars);
        }
    }

    /// <summary>A <see cref="WakeOutcome.WakePaused"/> record adds no toast: Spec §13.8 shows it only in the Task panel.</summary>
    [Fact]
    public async Task WakePaused_AddsNoToast()
    {
        (MudBunitContext ctx, _, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.WakePaused, DateTimeOffset.UtcNow));

            Assert.Empty(snackbar.ShownSnackbars);
        }
    }

    /// <summary>A <see cref="WakeRecord"/> with a <see langword="null"/> <c>RoomId</c> still toasts, but with no Room link.</summary>
    [Fact]
    public async Task Woken_WithNoRoomId_ShowsNoRoomLink()
    {
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts();
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId id);

            activity.Record(new WakeRecord(id, "Nova", null, null, WakeOutcome.Woken, DateTimeOffset.UtcNow));

            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));
            Assert.DoesNotContain("wake-toast-room-link", cut.Markup, StringComparison.Ordinal);
        }
    }

    /// <summary>Clicking the toast's Task link raises <c>OnOpenTask</c> with the woken Task's id, so the caller can open its panel.</summary>
    [Fact]
    public async Task Woken_ClickingTheTaskLink_RaisesOnOpenTaskWithTheId()
    {
        TaskId? opened = null;
        (MudBunitContext ctx, IRenderedComponent<ContainerFragment> cut, ISnackbar snackbar, TaskActivity activity) = RenderToasts(id => opened = id);
        await using (ctx)
        {
            _ = TaskId.TryParse("PLAT-0042", out TaskId expected);

            activity.Record(new WakeRecord(expected, "Nova", "room-1", "SAML Integration", WakeOutcome.Woken, DateTimeOffset.UtcNow));
            cut.WaitForAssertion(() => Assert.Single(snackbar.ShownSnackbars));

            cut.Find(".wake-toast-task-link").Click();

            Assert.Equal(expected, opened);
        }
    }

    /// <summary>A wake recorded after the component is disposed must not throw: the page may close while a wake is in flight.</summary>
    [Fact]
    public async Task Woken_AfterDispose_DoesNotThrow()
    {
        (MudBunitContext ctx, _, _, TaskActivity activity) = RenderToasts();
        _ = TaskId.TryParse("PLAT-0042", out TaskId id);
        await ctx.DisposeAsync();

        Exception? thrown = Xunit.Record.Exception(() =>
            activity.Record(new WakeRecord(id, "Nova", "room-1", "SAML", WakeOutcome.Woken, DateTimeOffset.UtcNow)));

        Assert.Null(thrown);
    }

    /// <summary>Renders a <c>MudSnackbarProvider</c> alongside <c>WakeToasts</c> so a toast's <see cref="RenderFragment"/> body actually reaches the DOM (mudblazor.md: a popover/provider paints as a sibling, not a descendant, of the component that opened it).</summary>
    private static (MudBunitContext Ctx, IRenderedComponent<ContainerFragment> Cut, ISnackbar Snackbar, TaskActivity Activity) RenderToasts(Action<TaskId>? onOpenTask = null)
    {
        MudBunitContext ctx = new();
        TaskActivity activity = new(Options.Create(new TeamOptions()));
        ctx.Services.AddSingleton(activity);

        IRenderedComponent<ContainerFragment> cut = ctx.Render(builder =>
        {
            builder.OpenComponent<MudSnackbarProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<Agency.Huddle.App.Components.Tasks.WakeToasts>(1);
            if (onOpenTask is not null)
            {
                builder.AddAttribute(2, nameof(Agency.Huddle.App.Components.Tasks.WakeToasts.OnOpenTask), EventCallback.Factory.Create<TaskId>(onOpenTask, onOpenTask));
            }

            builder.CloseComponent();
        });

        ISnackbar snackbar = ctx.Services.GetRequiredService<ISnackbar>();
        return (ctx, cut, snackbar, activity);
    }
}
