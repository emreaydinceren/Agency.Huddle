using System.Text.Json;

namespace Agency.Huddle.App.Desktop;

/// <summary>The client of <see cref="DesktopBootstrapEndpoint"/>: the hand-off and the <c>dashboard</c> verb ask a running copy for a bootstrap URL.</summary>
internal static class DesktopBootstrapClient
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Asks the copy described by <paramref name="info"/> for an admin bootstrap URL.</summary>
    /// <param name="info">The copy's record: its address and instance token.</param>
    /// <param name="handler">The HTTP handler, or <see langword="null"/> for a real network handler.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The URL, or <see langword="null"/> when the call failed (a refusal, an older copy, a connection error, an unusable answer).</returns>
    public static async Task<string?> RequestUrlAsync(InstanceInfo info, HttpMessageHandler? handler, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (!Uri.TryCreate(info.Url.TrimEnd('/') + DesktopBootstrapEndpoint.Route, UriKind.Absolute, out Uri? target))
        {
            return null;
        }

        using HttpClient client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = RequestTimeout;
        using HttpRequestMessage request = new(HttpMethod.Post, target);
        request.Headers.Add(DesktopStopEndpoint.TokenHeader, info.StopToken);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("url", out JsonElement url)
                && url.ValueKind == JsonValueKind.String
                && url.GetString() is { Length: > 0 } text
                    ? text
                    : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The call timed out; the caller falls back to the plain page.
            return null;
        }
    }
}
