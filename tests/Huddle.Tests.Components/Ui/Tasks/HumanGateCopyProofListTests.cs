namespace Agency.Huddle.Tests.Ui.Tasks;

using HumanGateCopy = Agency.Huddle.App.Components.Tasks.HumanGateCopy;

/// <summary>Pins the Proof list formatters of <c>HumanGateCopy</c> (UI copy rows 11-13) against their full literals.</summary>
public sealed class HumanGateCopyProofListTests
{
    /// <summary>The Ask button text and accessible name follow row 11.</summary>
    [Fact]
    public void AskButton_AndAccessibleName_MatchRowEleven()
    {
        Assert.Equal("Ask Nova", HumanGateCopy.AskButton("Nova"));
        Assert.Equal("Ask Nova for citation check", HumanGateCopy.AskAccessibleName("Nova", "citation check"));
    }

    /// <summary>The Ask Message follows row 12.</summary>
    [Fact]
    public void AskMessage_MatchesRowTwelve()
    {
        Assert.Equal("@Nova, PLAT-0001 needs citation check before it can come to me.", HumanGateCopy.AskMessage("Nova", "PLAT-0001", "citation check"));
    }

    /// <summary>The Ask snackbars follow row 13.</summary>
    [Fact]
    public void AskSnackbars_MatchRowThirteen()
    {
        Assert.Equal("Asked Nova in Platform.", HumanGateCopy.AskedSnackbar("Nova", "Platform"));
        Assert.Equal("Couldn't reach Nova.", HumanGateCopy.AskFailedSnackbar("Nova"));
    }
}
