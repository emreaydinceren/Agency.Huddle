namespace Agency.Huddle.Acp.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

public sealed class ScaffoldingTests
{
    [Fact]
    public void TeamAcp_HasInternalsVisibleToTests()
    {
        Assembly assembly = Assembly.Load("Huddle.Acp");

        Assert.Contains(
            assembly.GetCustomAttributes<InternalsVisibleToAttribute>(),
            attribute => attribute.AssemblyName == "Huddle.Acp.Tests");
    }

    [Fact]
    public void TeamConsole_HasInternalsVisibleToTests()
    {
        Assembly assembly = Assembly.Load("Huddle.Console");

        Assert.Contains(
            assembly.GetCustomAttributes<InternalsVisibleToAttribute>(),
            attribute => attribute.AssemblyName == "Huddle.Acp.Tests");
    }

    [Fact]
    public void DotAcp_ConnectionTypeIsLoadable()
    {
        Assert.NotNull(typeof(dotacp.client.Connection));
    }
}
