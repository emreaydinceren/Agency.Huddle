using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Data;
using Agency.Huddle.App.Teams;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.Tasks;

/// <summary>
/// Connects spend to the ledger a Needs You cap counts against the way the host does: a <see cref="PersonaSpend"/> whose increases the
/// <see cref="NeedsYouCapWatcher"/> charges to the scopes the Persona is in at that moment. Shared by the rigs that spend toward a cap.
/// </summary>
internal static class NeedsYouSpendWiring
{
    /// <summary>Builds the spend recorder and starts the watcher that charges it to the ledger.</summary>
    /// <param name="spendStore">The store the recorder writes through.</param>
    /// <param name="roomSpend">The store the recorder writes a Room's increases to, which a Project cap counts.</param>
    /// <param name="ledger">The ledger the caps read.</param>
    /// <param name="ids">The ids the ledger's Team rows are keyed by.</param>
    /// <param name="catalog">Says which Teams a Persona is in.</param>
    /// <param name="levels">What the watcher re-evaluates after each increase.</param>
    /// <returns>The recorder to spend through.</returns>
    public static PersonaSpend Attach(PersonaSpendStore spendStore, RoomSpendStore roomSpend, NeedsYouSpendStore ledger, ScopeIdStore ids, ITeamCatalog catalog, NeedsYouLevels levels)
    {
        PersonaSpend spend = new(NullLogger<PersonaSpend>.Instance, spendStore, roomSpend);
        NeedsYouCapWatcher watcher = new(spend, levels, catalog, ledger, ids, roomSpend);
        _ = watcher.StartAsync(CancellationToken.None);
        return spend;
    }

    /// <summary>Records one increase of <paramref name="amount"/>, as a fresh Adapter session reporting it as its first total.</summary>
    /// <param name="spend">The recorder.</param>
    /// <param name="persona">The Persona that spent.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="roomId">The Room the Turn ran in, or <see langword="null"/> for none.</param>
    public static void Spent(this PersonaSpend spend, string persona, decimal amount, string currency = "USD", string? roomId = null) =>
        spend.Add(persona, Guid.NewGuid().ToString("N"), amount, currency, roomId: roomId);
}
