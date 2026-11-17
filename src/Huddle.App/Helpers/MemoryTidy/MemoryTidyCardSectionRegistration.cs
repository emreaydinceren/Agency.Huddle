using Agency.Huddle.App.Components.Settings.Helpers;

namespace Agency.Huddle.App.Helpers.MemoryTidy;

/// <summary>Gives the Memory Tidy card its extra section: the next and last pass, Tidy now and the link to the last list.</summary>
internal sealed class MemoryTidyCardSectionRegistration : IHelperCardSection
{
    /// <inheritdoc />
    public HelperId Helper => MemoryTidyHelper.HelperId;

    /// <inheritdoc />
    public Type Component => typeof(MemoryTidyCardSection);
}
