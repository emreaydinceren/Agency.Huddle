namespace Agency.Huddle.App;

/// <summary>Bound from <c>Team:Instructions</c>, per Instructions and Memory §3.2.</summary>
public sealed class InstructionsOptions
{
    /// <summary>The most characters one Instructions file contributes to the system prompt; a longer file is cut and says where the rest is. Zero or less means no cap.</summary>
    public int MaxChars { get; set; } = 8000;
}
