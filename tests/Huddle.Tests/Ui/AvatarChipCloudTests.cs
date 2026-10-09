namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// Pins <c>AvatarChipCloud.Arrange</c> (Spec §6.5): selected chips first in the order they were pressed, the
/// unselected pool after them in pool order, and the tag cloud's cap showing the first N pool entries plus every
/// selected tag. Rows are written as comma lists; a trailing <c>*</c> marks a selected chip and a cap of -1 means no cap.
/// </summary>
public sealed class AvatarChipCloudTests
{
    /// <summary>The unselected pool comes back in pool order, none flagged, and a null cap shows everything.</summary>
    [Theory]
    [InlineData("a,b,c", "", -1, "a,b,c")]
    [InlineData("", "", -1, "")]
    [InlineData("a,b,c", "", 10, "a,b,c")]
    public void Arrange_NothingSelected_KeepsPoolOrder(string pool, string selected, int cap, string expected)
    {
        Assert.Equal(expected, Run(pool, selected, cap));
    }

    /// <summary>Selected chips lead in the order they were pressed (not pool order), flagged, and are not shown again among the unselected.</summary>
    [Theory]
    [InlineData("a,b,c,d", "c", -1, "c*,a,b,d")]
    [InlineData("a,b,c,d", "d,b", -1, "d*,b*,a,c")]
    [InlineData("a,b,c,d", "b,d", -1, "b*,d*,a,c")]
    public void Arrange_Selected_FloatLeftInPressOrder(string pool, string selected, int cap, string expected)
    {
        Assert.Equal(expected, Run(pool, selected, cap));
    }

    /// <summary>With a cap, only the first N pool entries show when nothing is selected.</summary>
    [Theory]
    [InlineData("a,b,c,d,e", "", 3, "a,b,c")]
    [InlineData("a,b,c,d,e", "", 0, "")]
    [InlineData("a,b,c", "", 5, "a,b,c")]
    public void Arrange_Cap_ShowsOnlyTheFirstN(string pool, string selected, int cap, string expected)
    {
        Assert.Equal(expected, Run(pool, selected, cap));
    }

    /// <summary>A selected tag beyond the cap stays visible, leading the cloud, while the first N pool entries still show.</summary>
    [Theory]
    [InlineData("a,b,c,d,e,f", "e", 3, "e*,a,b,c")]
    [InlineData("a,b,c,d,e,f", "f,e", 2, "f*,e*,a,b")]
    [InlineData("a,b,c,d,e,f", "e", 0, "e*")]
    public void Arrange_SelectedBeyondCap_StaysVisible(string pool, string selected, int cap, string expected)
    {
        Assert.Equal(expected, Run(pool, selected, cap));
    }

    /// <summary>A selected tag inside the cap floats left without consuming an extra slot: the first N pool entries still show.</summary>
    [Fact]
    public void Arrange_SelectedInsideCap_FloatsLeftAndKeepsTheCapWindow()
    {
        Assert.Equal("b*,a,c", Run("a,b,c,d,e", "b", 3));
    }

    /// <summary>Deselecting everything returns the capped pool to its original order.</summary>
    [Fact]
    public void Arrange_AfterDeselect_RestoresPoolOrder()
    {
        string before = Run("a,b,c,d", string.Empty, -1);

        Assert.Equal("b*,a,c,d", Run("a,b,c,d", "b", -1));
        Assert.Equal("a,b,c,d", before);
        Assert.Equal(before, Run("a,b,c,d", string.Empty, -1));
    }

    private static string Run(string pool, string selected, int cap)
    {
        IReadOnlyList<Agency.Huddle.App.Components.Shared.ChipItem> chips = Agency.Huddle.App.Components.Shared.AvatarChipCloud.Arrange(Split(pool), Split(selected), cap < 0 ? null : cap);
        return string.Join(',', chips.Select(static chip => chip.Selected ? chip.Text + "*" : chip.Text));
    }

    private static string[] Split(string list) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
