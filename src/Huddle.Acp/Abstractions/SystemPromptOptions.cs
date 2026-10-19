namespace Agency.Huddle.Acp.Abstractions;

using System;

/// <summary>Describes an optional per-session system prompt (persona) and how it combines with the agent's own.</summary>
public sealed class SystemPromptOptions
{
    public SystemPromptOptions(string text, SystemPromptMode mode = SystemPromptMode.Append)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text must not be null or whitespace.", nameof(text));
        }

        if (!Enum.IsDefined(mode))
        {
#pragma warning disable HAA0601 // Exception path only: the boxed value lives just long enough to build the exception.
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Undefined SystemPromptMode value.");
#pragma warning restore HAA0601
        }

        this.Text = text;
        this.Mode = mode;
    }

    public string Text { get; }

    public SystemPromptMode Mode { get; }
}