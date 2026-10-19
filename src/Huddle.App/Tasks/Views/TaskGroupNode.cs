namespace Agency.Huddle.App.Tasks.Views;

/// <summary>One node of a Task grouping tree: a labelled group, its Task count, its child groups, and its leaf Tasks.</summary>
public sealed record TaskGroupNode(string? Label, int Count, IReadOnlyList<TaskGroupNode> Children, IReadOnlyList<TaskItem> Items);
