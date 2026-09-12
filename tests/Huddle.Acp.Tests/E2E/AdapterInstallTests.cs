namespace Agency.Huddle.Acp.Tests.E2E;

using Xunit;

public sealed class AdapterInstallTests
{
    [Fact(SkipUnless = nameof(E2E.Enabled), SkipType = typeof(E2E), Skip = "Set TEAM_E2E=1 to run")]
    public void DistIndexJs_Exists()
    {
        Assert.True(
            File.Exists(E2E.AdapterEntry),
            $"Adapter entry point not found at '{E2E.AdapterEntry}'. Run tools/acp/install.ps1 to install it.");
    }
}
