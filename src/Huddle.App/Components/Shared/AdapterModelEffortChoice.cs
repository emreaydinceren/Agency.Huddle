namespace Agency.Huddle.App.Components;

/// <summary>
/// What an <c>AdapterModelEffortPicker</c> now holds, raised after every choice with the cascade already applied (a changed
/// Adapter clears the rest, a changed Model clears Effort). Public because it is a component parameter's type; in
/// <c>Agency.Huddle.App.Components</c>, like <see cref="NoticeLevel"/>, because a public type in a namespace ending
/// <c>Shared</c> fails CA1716.
/// </summary>
/// <param name="Adapter">The Adapter id, or <see langword="null"/> for the default Adapter.</param>
/// <param name="Model">The Model id, or <see langword="null"/> for the Adapter's own default.</param>
/// <param name="Effort">The Effort, or <see langword="null"/> for the Model's default.</param>
/// <param name="WorkMode">The Work mode id, or <see langword="null"/> for the Adapter's own; always <see langword="null"/> when the picker offers no Work mode.</param>
public sealed record AdapterModelEffortChoice(string? Adapter, string? Model, string? Effort, string? WorkMode);
