using System.Text.Json;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Drives the real handshake over HTTP as a browser would: fetches a challenge, signs it with a <see cref="TestDevice"/> and posts it.
/// Reused by the gate tests of Stage C.
/// </summary>
internal sealed class GatewayTestClient
{
    /// <summary>The JSON options the wire uses: camelCase.</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The <c>Origin</c> a browser sends for the test server's own page (D-1: a POST to <c>/auth/*</c> must carry one).</summary>
    public const string OriginHeader = "http://localhost";

    private readonly HttpClient http;

    /// <summary>Wraps a client. The device is random unless one is given.</summary>
    /// <param name="http">The client; its base address is the server.</param>
    /// <param name="device">The device identity.</param>
    public GatewayTestClient(HttpClient http, TestDevice? device = null)
    {
        this.http = http;
        this.Device = device ?? new TestDevice();
    }

    /// <summary>The device this client signs as.</summary>
    public TestDevice Device { get; }

    /// <summary>Fetches a fresh challenge.</summary>
    /// <param name="ct">Cancellation.</param>
    public async Task<ChallengeNonceStore.Challenge> GetChallengeAsync(CancellationToken ct)
    {
        using HttpResponseMessage response = await this.http.GetAsync(new Uri("/auth/challenge", UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
        ChallengeNonceStore.Challenge? challenge = await response.Content.ReadFromJsonAsync<ChallengeNonceStore.Challenge>(Json, ct);
        return challenge ?? throw new InvalidOperationException("The challenge body was empty.");
    }

    /// <summary>Fetches a challenge, signs it, and posts the handshake.</summary>
    /// <param name="auth">The credentials.</param>
    /// <param name="ct">Cancellation.</param>
    /// <param name="scopes">The scopes asked for; default read and write.</param>
    /// <param name="returnPath">The <c>return</c> query value to send, or <see langword="null"/> for none.</param>
    public async Task<HttpResponseMessage> ConnectAsync(ConnectAuth auth, CancellationToken ct, IReadOnlyList<string>? scopes = null, string? returnPath = null)
    {
        ChallengeNonceStore.Challenge challenge = await this.GetChallengeAsync(ct);
        ConnectRequest request = this.Device.Build(
            challenge.Nonce,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            scopes ?? [GatewayScopes.Read, GatewayScopes.Write],
            auth);
        return await this.PostAsync(request, ct, returnPath);
    }

    /// <summary>Posts any handshake body, for tests that tamper with one.</summary>
    /// <param name="request">The body.</param>
    /// <param name="ct">Cancellation.</param>
    /// <param name="returnPath">The <c>return</c> query value to send, or <see langword="null"/> for none.</param>
    public async Task<HttpResponseMessage> PostAsync(ConnectRequest request, CancellationToken ct, string? returnPath = null)
    {
        string uri = returnPath is null ? "/auth/connect" : "/auth/connect?return=" + Uri.EscapeDataString(returnPath);
        using HttpRequestMessage message = new(HttpMethod.Post, new Uri(uri, UriKind.Relative))
        {
            Content = JsonContent.Create(request, options: Json),
        };
        message.Headers.TryAddWithoutValidation("Origin", OriginHeader);
        return await this.http.SendAsync(message, ct);
    }
}
