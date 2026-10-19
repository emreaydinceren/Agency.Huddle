namespace Agency.Huddle.Tests.Acp;

public sealed class AcpReferenceTests
{
    [Fact]
    public void TeamApp_ReferencesAcp()
    {
        Assert.Equal("Huddle.Acp", typeof(Agency.Huddle.Acp.Abstractions.IAgentHost).Assembly.GetName().Name);
    }
}