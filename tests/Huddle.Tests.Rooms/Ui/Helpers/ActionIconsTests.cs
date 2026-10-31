using MudBlazor;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="Agency.Huddle.App.Components.Shared.ActionIcons"/> to the action table in design-system.md "Icons": one action, one
/// icon, in the weight the table gives. Each row names the constant, the constant's value and the
/// MudBlazor icon the table requires (UX-083, UX-084, UX-R26).
/// </summary>
public sealed class ActionIconsTests
{
    /// <summary>The twelve action constants, each with the MudBlazor icon the design-system table names.</summary>
    /// <returns>Rows of constant name, the constant's value and the expected MudBlazor icon.</returns>
    public static TheoryData<string, string, string> Cases() => new()
    {
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Add), Agency.Huddle.App.Components.Shared.ActionIcons.Add, Icons.Material.Filled.Add },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Close), Agency.Huddle.App.Components.Shared.ActionIcons.Close, Icons.Material.Filled.Close },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Delete), Agency.Huddle.App.Components.Shared.ActionIcons.Delete, Icons.Material.Filled.Delete },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.RemoveMember), Agency.Huddle.App.Components.Shared.ActionIcons.RemoveMember, Icons.Material.Filled.PersonRemove },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.RemoveFromList), Agency.Huddle.App.Components.Shared.ActionIcons.RemoveFromList, Icons.Material.Filled.RemoveCircleOutline },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Dismiss), Agency.Huddle.App.Components.Shared.ActionIcons.Dismiss, Icons.Material.Filled.Check },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Edit), Agency.Huddle.App.Components.Shared.ActionIcons.Edit, Icons.Material.Filled.Edit },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Copy), Agency.Huddle.App.Components.Shared.ActionIcons.Copy, Icons.Material.Filled.ContentCopy },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.CopyLink), Agency.Huddle.App.Components.Shared.ActionIcons.CopyLink, Icons.Material.Filled.Link },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Duplicate), Agency.Huddle.App.Components.Shared.ActionIcons.Duplicate, Icons.Material.Filled.FileCopy },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.MoreActions), Agency.Huddle.App.Components.Shared.ActionIcons.MoreActions, Icons.Material.Filled.MoreVert },
        { nameof(Agency.Huddle.App.Components.Shared.ActionIcons.Search), Agency.Huddle.App.Components.Shared.ActionIcons.Search, Icons.Material.Filled.Search },
    };

    /// <summary>Each action constant holds the icon the design-system table gives that action.</summary>
    /// <param name="name">The constant's name, shown in a failure.</param>
    /// <param name="actual">The constant's value.</param>
    /// <param name="expected">The MudBlazor icon the table requires.</param>
    [Theory]
    [MemberData(nameof(ActionIconsTests.Cases))]
    public void Constant_HoldsTheIconTheTableGives(string name, string actual, string expected)
    {
        Assert.True(string.Equals(actual, expected, StringComparison.Ordinal), $"ActionIcons.{name} does not hold the design-system icon.");
    }
}
