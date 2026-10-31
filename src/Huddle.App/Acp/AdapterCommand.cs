namespace Agency.Huddle.App.Acp;

/// <summary>One Adapter command an Agent currently offers: advertised by its Adapter and allowed by its Adapter Profile.</summary>
/// <param name="Name">The command's name without the leading slash, in the Adapter's own casing, which is what is sent back to it.</param>
/// <param name="Description">The Adapter's own one-line description. Text only: never rendered as Markdown or HTML.</param>
/// <param name="InputHint">The Adapter's placeholder for the command's free-text input, or <see langword="null"/> when it takes none.</param>
internal sealed record AdapterCommand(string Name, string Description, string? InputHint);
