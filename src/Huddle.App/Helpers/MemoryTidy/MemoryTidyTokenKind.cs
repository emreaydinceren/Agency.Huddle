namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>What kind of thing a checked token is, which decides how two tokens are compared (Memory Tidy spec §6.10).</summary>
internal enum MemoryTidyTokenKind
{
    /// <summary>A whole date-shaped run or a time; compared as written.</summary>
    Date,

    /// <summary>A whole number with its separators and sign; compared as written.</summary>
    Number,

    /// <summary>A link, trimmed of trailing punctuation; compared ignoring case.</summary>
    Link,

    /// <summary>An e-mail address; compared ignoring case.</summary>
    Address,
}
