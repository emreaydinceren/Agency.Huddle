namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library:Prompts</c>, per Spec §6.6 and §7.4: the Prompt override folder.</summary>
public sealed class LibraryPromptsOptions
{
    /// <summary>The Prompt override folder, relative to <c>DataDir</c> (<c>Team:Library:Prompts:Dir</c>).</summary>
    public string Dir { get; set; } = "Prompts";
}
