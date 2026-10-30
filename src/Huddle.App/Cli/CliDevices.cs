using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Agency.Huddle.App.Cli;

/// <summary>The <c>devices</c> verbs (Spec 6.13, C-10): <c>list</c>, <c>approve</c>, <c>reject</c>, <c>revoke</c>, <c>remove</c> and <c>rotate</c>, over <c>/_gateway/*</c>.</summary>
internal static class CliDevices
{
    private static readonly string[] DeviceVerbs = ["revoke", "remove", "rotate"];

    /// <summary>Runs <c>devices ...</c>.</summary>
    /// <param name="args">The arguments after <c>devices</c>.</param>
    /// <param name="context">The seams.</param>
    /// <param name="ct">Cancels the call.</param>
    public static async Task<int> RunAsync(string[] args, CliContext context, CancellationToken ct)
    {
        Parsed? parsed = Parse(args);
        if (parsed is null)
        {
            return await CliVerbs.PrintUsageAsync(context).ConfigureAwait(false);
        }

        string? baseUrl = parsed.Url ?? CliVerbs.RunningCopy(context.DataDir)?.Url;
        if (baseUrl is null)
        {
            await context.Console.Error.WriteLineAsync("No desktop copy is running. Pass --url http://127.0.0.1:PORT to reach a copy started another way.").ConfigureAwait(false);
            return 1;
        }

        string? token = await CliVerbs.ResolveTokenAsync(context).ConfigureAwait(false);
        if (token is null)
        {
            return 1;
        }

        using HttpClient client = context.Handler is null ? new HttpClient() : new HttpClient(context.Handler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            return await DispatchAsync(parsed, baseUrl.TrimEnd('/'), client, context, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            await context.Console.Error.WriteLineAsync($"Could not reach Huddle at {baseUrl}: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await context.Console.Error.WriteLineAsync($"Huddle at {baseUrl} did not answer in time.").ConfigureAwait(false);
            return 1;
        }
    }

    private static Parsed? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string verb = args[0];
        string? url = null;
        string? device = null;
        string? id = null;
        List<string> scopes = [];
        string? option = null;
        foreach (string arg in args.Skip(1))
        {
            if (option is not null)
            {
                switch (option)
                {
                    case "--url":
                        url = arg;
                        break;
                    case "--device":
                        device = arg;
                        break;
                    default:
                        scopes.Add(arg);
                        break;
                }

                option = null;
            }
            else if (arg is "--url" or "--device" or "--scope")
            {
                option = arg;
            }
            else if (!arg.StartsWith('-') && id is null)
            {
                id = arg;
            }
            else
            {
                return null;
            }
        }

        if (option is not null)
        {
            return null;
        }

        bool valid = verb switch
        {
            "list" => id is null && device is null && scopes.Count == 0,
            "approve" => device is null,
            "reject" => id is not null && device is null && scopes.Count == 0,
            _ when DeviceVerbs.Contains(verb, StringComparer.Ordinal) => device is not null && id is null && scopes.Count == 0,
            _ => false,
        };
        return valid ? new Parsed(verb, url, device, id, scopes) : null;
    }

    private static async Task<int> DispatchAsync(Parsed parsed, string baseUrl, HttpClient client, CliContext context, CancellationToken ct)
    {
        string root = baseUrl + "/_gateway/devices";
        switch (parsed.Verb)
        {
            case "list":
                return await ListAsync(client, root, context, ct).ConfigureAwait(false);
            case "approve" when parsed.Id is null:
                _ = await ListAsync(client, root, context, ct).ConfigureAwait(false);
                await context.Console.Error.WriteLineAsync("Pass a request id to approve one: huddle devices approve REQUEST_ID").ConfigureAwait(false);
                return 1;
            case "approve":
                using (StringContent? body = parsed.Scopes.Count == 0 ? null : JsonBody(parsed.Scopes))
                {
                    return await PostAsync(client, $"{root}/requests/{Uri.EscapeDataString(parsed.Id ?? string.Empty)}/approve", body, context, ct).ConfigureAwait(false);
                }

            case "reject":
                return await PostAsync(client, $"{root}/requests/{Uri.EscapeDataString(parsed.Id ?? string.Empty)}/reject", null, context, ct).ConfigureAwait(false);
            default:
                return await PostAsync(client, $"{root}/{Uri.EscapeDataString(parsed.Device ?? string.Empty)}/{parsed.Verb}", null, context, ct).ConfigureAwait(false);
        }
    }

    private static StringContent JsonBody(IReadOnlyList<string> scopes)
        => new(JsonSerializer.Serialize(new { scope = scopes }), Encoding.UTF8, "application/json");

    private static async Task<int> PostAsync(HttpClient client, string url, HttpContent? body, CliContext context, CancellationToken ct)
    {
        using HttpResponseMessage response = await client.PostAsync(new Uri(url), body, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            await context.Console.Out.WriteLineAsync("Done.").ConfigureAwait(false);
            return 0;
        }

        await context.Console.Error.WriteLineAsync(FailureText(response.StatusCode)).ConfigureAwait(false);
        return 1;
    }

    private static string FailureText(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Huddle did not accept the gateway token (401). Check Team:Gateway:Auth:Token or HUDDLE_GATEWAY_TOKEN.",
        HttpStatusCode.NotFound => "Not found (404): there is no such device or request.",
        HttpStatusCode.Forbidden => "Refused (403): the request was not allowed.",
        _ => $"The request failed ({(int)status}).",
    };

    private static async Task<int> ListAsync(HttpClient client, string root, CliContext context, CancellationToken ct)
    {
        using HttpResponseMessage response = await client.GetAsync(new Uri(root), ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await context.Console.Error.WriteLineAsync(FailureText(response.StatusCode)).ConfigureAwait(false);
            return 1;
        }

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        await context.Console.Out.WriteLineAsync("Devices:").ConfigureAwait(false);
        await PrintRowsAsync(json.RootElement, "devices", "deviceId", "pairedVia", context).ConfigureAwait(false);
        await context.Console.Out.WriteLineAsync("Pending requests:").ConfigureAwait(false);
        await PrintRowsAsync(json.RootElement, "requests", "requestId", "ingress", context).ConfigureAwait(false);
        return 0;
    }

    private static async Task PrintRowsAsync(JsonElement root, string property, string idName, string extraName, CliContext context)
    {
        if (!root.TryGetProperty(property, out JsonElement rows) || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
        {
            await context.Console.Out.WriteLineAsync("  (none)").ConfigureAwait(false);
            return;
        }

        foreach (JsonElement row in rows.EnumerateArray())
        {
            string id = Text(row, idName);
            string name = Text(row, "name");
            string scopes = row.TryGetProperty("scopes", out JsonElement list) && list.ValueKind == JsonValueKind.Array
                ? string.Join(',', list.EnumerateArray().Select(item => item.GetString()))
                : string.Empty;
            await context.Console.Out.WriteLineAsync($"  {id}  {name}  [{scopes}]  {Text(row, extraName)}").ConfigureAwait(false);
        }
    }

    private static string Text(JsonElement row, string name)
        => row.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private sealed record Parsed(string Verb, string? Url, string? Device, string? Id, IReadOnlyList<string> Scopes);
}
