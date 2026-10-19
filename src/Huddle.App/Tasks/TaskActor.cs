namespace Agency.Huddle.App.Tasks;

/// <summary>Who made a change. Names are Persona Names, or the Human's Name.</summary>
public sealed record TaskActor(TaskActorKind Kind, string Name, string? UserId);
