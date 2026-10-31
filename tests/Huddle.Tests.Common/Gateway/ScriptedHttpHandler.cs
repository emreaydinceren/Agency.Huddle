using System.Net;
using System.Text;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>One request a <see cref="ScriptedHttpHandler"/> saw.</summary>
/// <param name="Method">The method.</param>
/// <param name="Uri">The absolute URI.</param>
/// <param name="Authorization">The <c>Authorization</c> header value, or <see langword="null"/>.</param>
/// <param name="InstanceToken">The <c>X-Huddle-Token</c> header value, or <see langword="null"/>.</param>
/// <param name="Body">The body text, or <see langword="null"/> when there was none.</param>
internal sealed record ScriptedRequest(HttpMethod Method, Uri Uri, string? Authorization, string? InstanceToken, string? Body);

/// <summary>An <see cref="HttpMessageHandler"/> that records each request (headers and body) and answers from a script, for client-side tests.</summary>
/// <param name="respond">Builds the response for a request.</param>
internal sealed class ScriptedHttpHandler(Func<ScriptedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    private readonly List<ScriptedRequest> requests = [];

    /// <summary>Gets the requests in call order.</summary>
    public IReadOnlyList<ScriptedRequest> Requests => this.requests;

    /// <summary>A handler that always answers <paramref name="status"/> with a JSON body.</summary>
    /// <param name="status">The status.</param>
    /// <param name="json">The body.</param>
    public static ScriptedHttpHandler Json(HttpStatusCode status, string json)
        => new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    /// <summary>A handler that always throws <paramref name="exception"/>.</summary>
    /// <param name="exception">The failure.</param>
    public static ScriptedHttpHandler Throwing(Exception exception) => new(_ => throw exception);

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        string? authorization = request.Headers.Authorization?.ToString();
        string? token = request.Headers.TryGetValues("X-Huddle-Token", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
        ScriptedRequest seen = new(request.Method, request.RequestUri ?? new Uri("about:blank"), authorization, token, body);
        this.requests.Add(seen);
        return respond(seen);
    }
}
