namespace Agency.Huddle.App.Components.Teams;

/// <summary>How a team type is drawn on a card: its icon. Every type shares one quiet tile; only a selected card tints it.</summary>
/// <param name="Icon">A MudBlazor icon path.</param>
internal sealed record TeamTypeStyle(string Icon);
