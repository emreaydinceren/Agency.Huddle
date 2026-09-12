namespace Agency.Huddle.Acp.Abstractions;

using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>A tool whose body executes inside our own process, exposed to an agent over MCP.</summary>
public interface IAppTool
{
    string Name { get; }

    string Description { get; }

    JsonObject InputSchema { get; }

    Task<string> InvokeAsync(JsonObject arguments, CancellationToken cancellationToken);
}