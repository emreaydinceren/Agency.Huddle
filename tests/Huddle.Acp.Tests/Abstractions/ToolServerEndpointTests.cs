namespace Agency.Huddle.Acp.Tests.Abstractions;

using System;
using System.Collections.Generic;
using Agency.Huddle.Acp.Abstractions;
using Xunit;

public sealed class ToolServerEndpointTests
{
    [Fact]
    public void NullHeaders_ExposesEmptyNonNullHeaders()
    {
        ToolServerEndpoint endpoint = new ToolServerEndpoint("team", new Uri("http://127.0.0.1:5057/mcp"));

        Assert.NotNull(endpoint.Headers);
        Assert.Empty(endpoint.Headers);
    }

    [Fact]
    public void HeadersProvided_ExposesSameEntries()
    {
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authorization"] = "Bearer test-token",
        };

        ToolServerEndpoint endpoint = new ToolServerEndpoint("team", new Uri("http://127.0.0.1:5057/mcp"), headers);

        Assert.True(endpoint.Headers.TryGetValue("Authorization", out string? value));
        Assert.Equal("Bearer test-token", value);
    }
}
