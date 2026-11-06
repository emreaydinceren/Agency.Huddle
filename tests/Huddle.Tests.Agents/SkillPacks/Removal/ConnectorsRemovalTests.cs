using Agency.Huddle.App.Data;
using Agency.Huddle.App.SkillSources;

namespace Agency.Huddle.Tests.SkillPacks.Removal;

/// <summary>
/// Removing a Pack from the Agents that hold it rewrites their files through <c>PersonaStore.Update</c>, which never writes
/// the account-connectors row (docs/Huddle.AgentConnectors-Specifications.md section 10).
/// </summary>
public sealed class ConnectorsRemovalTests : IDisposable
{
    private readonly RemovalFixture fixture = new();

    /// <summary>Removing a Pack from a holder that has no row leaves the holder with no row, in either holder choice.</summary>
    /// <param name="removeToo"><see langword="true"/> to remove the Pack's Skills from the holders too, <see langword="false"/> to keep them.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovePack_HolderWithoutRow_StaysOff(bool removeToo)
    {
        HolderChoice choice = removeToo ? HolderChoice.RemoveToo : HolderChoice.Keep;
        await this.fixture.SeedAsync();
        string before = await this.fixture.HolderAsync("Jane", "documents", "own-skill");
        PersonaConnectorsStore rows = new(this.fixture.Host.Options);
        Assert.False(rows.IsOn("Jane"));

        RemovalOutcome outcome = this.fixture.Remover.RemovePack(this.fixture.Pack("documents"), choice);

        Assert.True(outcome.Ok);
        Assert.NotEqual(before, this.fixture.TextOf("Jane"));
        Assert.False(rows.IsOn("Jane"));
        Assert.False(this.fixture.Host.Personas.Get("Jane")?.Connectors);
    }

    /// <inheritdoc/>
    public void Dispose() => this.fixture.Dispose();
}
