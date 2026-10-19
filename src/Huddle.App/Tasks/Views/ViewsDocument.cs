namespace Agency.Huddle.App.Tasks.Views;

/// <summary>The shape of <c>views.json</c>: a version number and the Views it holds.</summary>
public sealed record ViewsDocument(int Version, IReadOnlyList<TaskView> Views);
