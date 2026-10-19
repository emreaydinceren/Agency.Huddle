namespace Agency.Huddle.App.Tasks.HumanGate;

/// <summary>The result of <see cref="Acceptance.Of"/>: the state, and who accepted when and what.</summary>
/// <param name="State">Whether the acceptance counts.</param>
/// <param name="At">When the latest counting acceptance was logged; <see langword="null"/> for <see cref="AcceptanceState.None"/>.</param>
/// <param name="By">Who accepted; <see langword="null"/> for <see cref="AcceptanceState.None"/>.</param>
/// <param name="Hash">The hash the acceptance carries; <see langword="null"/> for <see cref="AcceptanceState.None"/>.</param>
internal sealed record AcceptanceInfo(AcceptanceState State, DateTimeOffset? At, string? By, string? Hash)
{
    /// <summary>The result when no acceptance counts.</summary>
    public static AcceptanceInfo NoAcceptance { get; } = new(AcceptanceState.None, null, null, null);
}
