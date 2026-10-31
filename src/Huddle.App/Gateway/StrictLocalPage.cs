namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The page a browser gets when strict local mode refuses it: self-contained (the gate refuses <c>/auth.css</c> too), no script,
/// and nothing from the request is echoed into it.
/// </summary>
internal static class StrictLocalPage
{
    /// <summary>The page.</summary>
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="light dark">
        <title>Huddle is local only</title>
        <style>
        body { font-family: system-ui, sans-serif; margin: 0; padding: 2rem 1rem; background: #fafafa; color: #1c1c1c; }
        main { max-width: 32rem; margin: 10vh auto 0; }
        h1 { font-size: 1.5rem; margin: 0 0 1rem; }
        p { line-height: 1.5; margin: 0; }
        @media (prefers-color-scheme: dark) { body { background: #17181b; color: #e6e6e6; } }
        </style>
        </head>
        <body>
        <main>
        <h1>Huddle is local only</h1>
        <p>This Huddle only accepts connections from the computer it runs on. To use it from this device, turn off Strict local in Settings › Remote access on that computer, then restart Huddle.</p>
        </main>
        </body>
        </html>
        """;

    /// <summary>Whether the page fits the request: a GET or HEAD whose <c>Accept</c> header includes <c>text/html</c>.</summary>
    /// <param name="request">The refused request.</param>
    public static bool Suits(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
