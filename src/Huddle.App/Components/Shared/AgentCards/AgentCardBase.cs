using Microsoft.AspNetCore.Components;
using MudBlazor;
using Agency.Huddle.App.Data;

#pragma warning disable CA1716 // The namespace follows the Components/Shared folder (dotnet_style_namespace_match_folder); nothing outside this assembly consumes it.
namespace Agency.Huddle.App.Components.Shared.AgentCards;
#pragma warning restore CA1716

/// <summary>
/// The skeleton the five Transcript agent cards share (UX-R01): the <see cref="RoomId"/> parameter, the
/// loaded-Room guard that reloads when a component is reused across Rooms, the archived-Room check
/// (UX-035), subscribe and unsubscribe, ignoring change events while the card's own call is running, and
/// <see cref="RunBusyAsync"/>, which locks the card, reports a failure through a keyed snackbar and
/// reloads. A card derives from this, says what it subscribes to and how it loads, and renders an
/// <see cref="AgentCard"/>.
/// </summary>
/// <remarks>
/// A card's own call usually takes the very thing the card shows, which raises the store's change event
/// while this card is subscribed. <see cref="RefreshOnChange"/> ignores that event while busy and
/// <c>RunBusyAsync</c> reloads after the call, so a click never hides the busy view before it can be seen,
/// and another tab's change still updates the card normally.
/// </remarks>
public abstract class AgentCardBase : ComponentBase, IDisposable
{
    private readonly HashSet<string> busyKeys = new(StringComparer.Ordinal);
    private string? loadedRoomId;
    private Room? room;

    /// <summary>The Room whose waiting request, if any, this card shows.</summary>
    [Parameter, EditorRequired]
    public string RoomId { get; set; } = "";

    /// <summary>The Team Directory the Room is read from. Injected; <c>required</c> only so the property needs no null-forgiving initialiser.</summary>
    [Inject]
    public required ITeamDirectory TeamDirectory { get; set; }

    /// <summary>Reports a failed call. Injected; <c>required</c> only so the property needs no null-forgiving initialiser.</summary>
    [Inject]
    public required ISnackbar Snackbar { get; set; }

    /// <summary>Whether any call of this card is running.</summary>
    protected bool Busy => this.busyKeys.Count > 0;

    /// <summary>Whether the Room is known and not archived; a card renders nothing otherwise (UX-035).</summary>
    protected bool RoomIsOpen => this.room is { Archived: false };

    /// <summary>Unsubscribes, because the event hub is a singleton and a component that does not let go of it never dies.</summary>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Unsubscribes when <paramref name="disposing"/>; there is nothing unmanaged to release.</summary>
    /// <param name="disposing">Whether this is a call to <see cref="Dispose()"/> rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.Unsubscribe();
        }
    }

    /// <summary>Attaches this card to the events or store changes it follows; undone by <see cref="Unsubscribe"/>.</summary>
    protected abstract void Subscribe();

    /// <summary>Detaches everything <see cref="Subscribe"/> attached.</summary>
    protected abstract void Unsubscribe();

    /// <summary>Reads what is waiting in <see cref="RoomId"/> into the card's state.</summary>
    protected abstract Task LoadAsync();

    /// <summary>Whether the call named <paramref name="key"/> is running, for choosing its busy label.</summary>
    /// <param name="key">The key passed to <see cref="RunBusyAsync"/>.</param>
    protected bool IsBusy(string key)
    {
        return this.busyKeys.Contains(key);
    }

    /// <summary>
    /// Reloads after a change someone else made. Ignored while <see cref="Busy"/>, and, when
    /// <paramref name="changedRoomId"/> is given, for any other Room.
    /// </summary>
    /// <param name="changedRoomId">The Room the event names, or <see langword="null"/> when the event is not per Room.</param>
    protected void RefreshOnChange(string? changedRoomId = null)
    {
        if (this.Busy || (changedRoomId is not null && !string.Equals(changedRoomId, this.RoomId, StringComparison.Ordinal)))
        {
            return;
        }

        _ = RendererDispatch.RunAsync(this.InvokeAsync, async () =>
        {
            await this.LoadAsync();
            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Runs one click's work with the card locked, shows a failure through a keyed snackbar, then reloads.
    /// A click while another action of the card runs does nothing, unless <paramref name="allowParallel"/>.
    /// The key is released before the reload, so a reload that fails is reported and never leaves the card stuck busy.
    /// </summary>
    /// <param name="work">The call to the service. It uses <see cref="CancellationToken.None"/> on purpose: once the card is taken, abandoning the post when a circuit drops would lose the answer.</param>
    /// <param name="key">Names the call so a card with several can label the one that is running.</param>
    /// <param name="allowParallel">Whether the call may run beside another key, for a card whose parts act independently (Elicitation's forms). The same key never runs twice.</param>
    protected async Task RunBusyAsync(Func<Task> work, string key = "", bool allowParallel = false)
    {
        ArgumentNullException.ThrowIfNull(work);
        if ((!allowParallel && this.Busy) || !this.busyKeys.Add(key))
        {
            return;
        }

        try
        {
            await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.ReportFailure(ex);
        }
        finally
        {
            _ = this.busyKeys.Remove(key);
            try
            {
                await this.LoadAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                this.ReportFailure(ex);
            }
        }
    }

    /// <summary>Shows <paramref name="ex"/> through the one keyed error snackbar, so repeats collapse.</summary>
    /// <param name="ex">The failure.</param>
    private void ReportFailure(Exception ex)
    {
        this.Snackbar.Add(ex.Message, Severity.Error, configure: null, key: "agent-card-error");
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.Subscribe();
    }

    /// <summary>Reloads the Room and what waits in it whenever <see cref="RoomId"/> changes, since a component is reused across Rooms.</summary>
    protected override async Task OnParametersSetAsync()
    {
        if (string.Equals(this.RoomId, this.loadedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        this.loadedRoomId = this.RoomId;
        this.room = await this.TeamDirectory.GetRoomAsync(this.RoomId);
        await this.LoadAsync();
    }
}
