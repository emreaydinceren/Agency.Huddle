namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One checked token of a text.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The token as written, after any trimming.</param>
internal sealed record MemoryTidyToken(MemoryTidyTokenKind Kind, string Text)
{
    /// <summary>The most characters of a token that a refusal or the list quotes; a link can be thousands long.</summary>
    internal const int MaxQuoted = 80;

    /// <summary>The token as a refusal and the list quote it: cut to <see cref="MaxQuoted"/> characters, with <c>...</c> after a cut one.</summary>
    internal string Quoted => this.Text.Length <= MaxQuoted ? this.Text : this.Text[..MaxQuoted] + "...";

    /// <summary>The text two tokens are compared by: the kind, then the text, lower-cased for a link or an address.</summary>
    internal string Key => this.Kind switch
    {
        MemoryTidyTokenKind.Link => "L:" + this.Text.ToLowerInvariant(),
        MemoryTidyTokenKind.Address => "A:" + this.Text.ToLowerInvariant(),
        MemoryTidyTokenKind.Date => "D:" + this.Text,
        _ => "N:" + this.Text,
    };
}
