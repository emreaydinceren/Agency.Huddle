namespace Agency.Huddle.App.Tasks.Views;

/// <summary>One column of a Board View, mapped to the Task states shown in it.</summary>
public sealed record BoardColumn(string Label, IReadOnlyList<TaskState> States, bool Hidden = false);
