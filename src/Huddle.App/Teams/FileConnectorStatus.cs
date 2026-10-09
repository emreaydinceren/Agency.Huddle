using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Teams;

/// <summary>
/// Reads which connectors are connected from <c>connectors.json</c> in the data folder, such as
/// <c>{"connected": ["email:read"]}</c> (HR-7, decision D6). The file is read on every call, so an edit
/// shows at once; a missing, malformed or wrongly shaped file means nothing is connected.
/// </summary>
/// <param name="options">The Team options; <see cref="TeamOptions.DataDir"/> holds the file.</param>
internal sealed class FileConnectorStatus(IOptions<TeamOptions> options) : IConnectorStatus
{
    private const string ConnectedProperty = "connected";

    /// <inheritdoc />
    public string? Location => Path.Combine(options.Value.DataDir, TeamPaths.ConnectorsFileName);

    /// <inheritdoc />
    public bool IsConnected(string connector)
    {
        ArgumentNullException.ThrowIfNull(connector);
        string path = this.Location ?? string.Empty;
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(FileConnectorStatus.ConnectedProperty, out JsonElement list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), connector, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
