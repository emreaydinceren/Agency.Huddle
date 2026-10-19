namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The Tasks in one lane that fall under one Board column.</summary>
public sealed record BoardCell(BoardColumn Column, IReadOnlyList<TaskItem> Items);
