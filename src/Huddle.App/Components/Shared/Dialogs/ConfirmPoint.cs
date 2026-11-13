namespace Agency.Huddle.App.Components.Shared.Dialogs;

/// <summary>One point under a confirm's message: the <paramref name="Lead"/> in bold, then the <paramref name="Text"/> straight after it. It is public because the public ConfirmDialog takes it as a parameter; the CA1716 pragma for this namespace lives in ConfirmCopy.cs.</summary>
/// <param name="Lead">The bold opening words.</param>
/// <param name="Text">The rest of the point, carrying its own leading space.</param>
public sealed record ConfirmPoint(string Lead, string Text);
