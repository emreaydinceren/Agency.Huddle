namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>One checked token of a text.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The token as written, after any trimming.</param>
internal sealed record MemoryTidyToken(MemoryTidyTokenKind Kind, string Text)
{
    /// <summary>The text two tokens are compared by: the kind, then the text, lower-cased for a link or an address.</summary>
    internal string Key => this.Kind switch
    {
        MemoryTidyTokenKind.Link => "L:" + this.Text.ToLowerInvariant(),
        MemoryTidyTokenKind.Address => "A:" + this.Text.ToLowerInvariant(),
        MemoryTidyTokenKind.Date => "D:" + this.Text,
        _ => "N:" + this.Text,
    };
}
