namespace Agency.Huddle.App.Helpers;

/// <summary>Names one registered Helper; also its configuration key under <c>Team:Helpers</c>.</summary>
/// <param name="Value">The id, PascalCase, such as <c>NeedsHuman</c>.</param>
internal readonly record struct HelperId(string Value)
{
    /// <summary>The id as text.</summary>
    public override string ToString() => this.Value;
}
