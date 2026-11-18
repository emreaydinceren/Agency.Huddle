namespace Agency.Huddle.App.Acp;

/// <summary>Where a <see cref="PromptPart"/>'s text came from.</summary>
public abstract record PromptPartSource
{
    /// <summary>A catalog prompt, possibly overridden by the Human.</summary>
    /// <param name="Key">The prompt's key.</param>
    /// <param name="Overridden">Whether the Human's override is in effect.</param>
    internal sealed record Prompt(string Key, bool Overridden) : PromptPartSource;

    /// <summary>A file the Human wrote, such as an <c>AGENTS.md</c>.</summary>
    /// <param name="Path">The file's path as shown to the Agent.</param>
    internal sealed record File(string Path) : PromptPartSource;

    /// <summary>The Persona's own text.</summary>
    /// <param name="Name">The Persona's name.</param>
    internal sealed record Persona(string Name) : PromptPartSource;

    /// <summary>An index built from a folder's files.</summary>
    /// <param name="Kind">Which index: <c>memory</c> or <c>teamMemory</c>.</param>
    /// <param name="Count">How many entries (or groups) it lists.</param>
    internal sealed record Index(string Kind, int Count) : PromptPartSource;

    /// <summary>A text shipped inside the application that the Human cannot edit, such as the Huddle rules.</summary>
    /// <param name="Key">The text's key, for example <c>huddleRules.member</c>.</param>
    internal sealed record Embedded(string Key) : PromptPartSource;

    /// <summary>The list of offered tools.</summary>
    /// <param name="Count">How many tools are offered.</param>
    internal sealed record Tools(int Count) : PromptPartSource;
}
