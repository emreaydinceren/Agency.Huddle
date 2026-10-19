namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The Board's arrangement: its swimlanes, its visible columns, and how many Tasks a hidden column hides.</summary>
public sealed record BoardModel(IReadOnlyList<BoardLane> Lanes, IReadOnlyList<BoardColumn> VisibleColumns, int HiddenTaskCount);
