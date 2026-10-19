namespace Agency.Huddle.App.Tasks;

/// <summary>A single field change in a Task.</summary>
public sealed record FieldChange(TaskField Field, string? Old, string? New);
