namespace Agency.Huddle.App.Services;

/// <summary>Who sent a Message, as far as a Room's Budget is concerned.</summary>
internal enum BudgetSender
{
    /// <summary>The Human: a Message that resets the Budget.</summary>
    Human,

    /// <summary>An Agent: a Message that spends one of the Budget.</summary>
    Agent,

    /// <summary>Huddle itself (ADR-0065): a Message that neither spends nor resets the Budget.</summary>
    Huddle,
}
