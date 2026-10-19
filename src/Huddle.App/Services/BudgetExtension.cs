namespace Agency.Huddle.App.Services;

/// <summary>
/// The outcome of <see cref="ChatService.ExtendBudgetAsync"/>: what happened, the Room's Budget
/// afterward, and - only when something was actually redelivered - the facts of that delivery.
/// <see cref="Redelivered"/> is non-null exactly when <see cref="Result"/> is <see cref="ExtendResult.Granted"/>.
/// The caller applies the Reply Gate itself; <see cref="ChatService"/> must never decide whether anyone
/// will reply - ADR-0003, ADR-0004 and ADR-0005 each reject a server-computed <c>shouldReply</c>, because
/// the server labels, it never decides.
/// </summary>
/// <param name="Result">Which of the three outcomes occurred.</param>
/// <param name="Budget">The Room's Budget after the call, whichever outcome occurred.</param>
/// <param name="Redelivered">The facts of the re-delivered Message, or <see langword="null"/> when nothing was redelivered.</param>
public sealed record BudgetExtension(
    ExtendResult Result,
    RoomBudget Budget,
    RedeliveryFacts? Redelivered);