namespace Agency.Huddle.Acp.Abstractions;

using System;
using System.Collections.Generic;

/// <summary>Identifies a running MCP tool server that an agent session should be told about.</summary>
public sealed class ToolServerEndpoint
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public ToolServerEndpoint(string name, Uri uri, IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(uri);

        this.Name = name;
        this.Uri = uri;
        this.Headers = headers is null
            ? ToolServerEndpoint.EmptyHeaders
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);
    }

    public string Name { get; }

    public Uri Uri { get; }

    public IReadOnlyDictionary<string, string> Headers { get; }
}