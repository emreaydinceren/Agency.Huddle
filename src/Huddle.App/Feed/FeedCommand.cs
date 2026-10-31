namespace Agency.Huddle.App.Feed;

/// <summary>One thing the writer does, queued in order: a closed record hierarchy (spec 6.2).</summary>
/// <param name="At">When the command was queued, taken from the injected TimeProvider at the call.</param>
#pragma warning disable S1694 // Spec 6.2 fixes a closed record hierarchy (the PageSubject pattern); an interface would lose the base record's value equality.
internal abstract record FeedCommand(DateTimeOffset At);
#pragma warning restore S1694
