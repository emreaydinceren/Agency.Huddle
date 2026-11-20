namespace Agency.Huddle.App.Tasks.Wake;

/// <summary>The production set of wake rules.</summary>
internal static class TaskWakeRules
{
    /// <summary>The names of the rules <see cref="TaskTriggerService"/> runs, in the order it runs them.</summary>
    public static IReadOnlyList<string> ProductionOrder { get; } = ["Assignee", "Manager", "RequestingManager", "Mention"];
}
