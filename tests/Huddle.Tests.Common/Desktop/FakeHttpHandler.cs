using System.Net;

namespace Agency.Huddle.Tests.Desktop;

/// <summary>What a <see cref="FakeHttpHandler"/> saw for one request, captured before the request is disposed.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The absolute request URI.</param>
/// <param name="TokenValues">Every value of the <c>X-Huddle-Token</c> header, empty when absent.</param>
internal sealed record RecordedRequest(HttpMethod Method, Uri? Uri, IReadOnlyList<string> TokenValues);

/// <summary>An <see cref="HttpMessageHandler"/> that records each request and answers (or throws) as scripted.</summary>
/// <param name="respond">Produces the response for a request, or throws to simulate a transport failure.</param>
internal sealed class FakeHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    private readonly List<RecordedRequest> requests = [];

    /// <summary>Gets the requests received so far, in order.</summary>
    public IReadOnlyList<RecordedRequest> Requests => this.requests;

    /// <summary>Creates a handler that always answers with <paramref name="status"/>.</summary>
    /// <param name="status">The status code to return.</param>
    /// <returns>The handler.</returns>
    public static FakeHttpHandler Returning(HttpStatusCode status)
    {
        return new FakeHttpHandler((_, _) => new HttpResponseMessage(status));
    }

    /// <summary>Creates a handler that throws <paramref name="exception"/> for every request.</summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>The handler.</returns>
    public static FakeHttpHandler Throwing(Exception exception)
    {
        return new FakeHttpHandler((_, _) => throw exception);
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> token = request.Headers.TryGetValues("X-Huddle-Token", out IEnumerable<string>? values)
            ? values.ToList()
            : [];
        this.requests.Add(new RecordedRequest(request.Method, request.RequestUri, token));
        return Task.FromResult(respond(request, cancellationToken));
    }
}
