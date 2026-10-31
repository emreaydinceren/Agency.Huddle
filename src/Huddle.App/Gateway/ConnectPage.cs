namespace Agency.Huddle.App.Gateway;

/// <summary>
/// The HTML of <c>GET /auth</c> (Spec 6.6, "the connect page"): a small self-contained page with no Blazor. Everything it does is in
/// <c>wwwroot/auth.js</c>, which learns the host, the auth mode and any verified Tailscale login from <c>GET /auth/status</c>.
/// </summary>
internal static class ConnectPage
{
    /// <summary>The page.</summary>
    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="light dark">
        <title>Connect to Huddle</title>
        <link rel="stylesheet" href="/auth.css">
        </head>
        <body>
        <main class="card">
          <h1>Connect to Huddle</h1>
          <p id="where" class="muted"></p>
          <p id="tailscale" class="muted" hidden></p>
          <p id="busy" role="status" hidden>Connecting&hellip;</p>
          <form id="form" hidden>
            <label id="label" for="secret">Gateway token</label>
            <input id="secret" type="password" autocomplete="off" autocapitalize="off" spellcheck="false" required>
            <button type="submit">Connect</button>
          </form>
          <section id="waiting" hidden>
            <p><strong>Waiting for approval&hellip;</strong></p>
            <p>Approve this device in Settings &rsaquo; Remote access on a device with admin access, or run <code>huddle devices approve</code> on the host.</p>
            <p class="muted">Request <code id="requestId"></code></p>
            <button id="checkNow" type="button">Check now</button>
          </section>
          <p id="message" role="alert" hidden></p>
        </main>
        <script type="importmap">{"imports":{"@noble/hashes/crypto":"/lib/noble/hashes/crypto.js"}}</script>
        <script type="module" src="/auth.js"></script>
        </body>
        </html>
        """;
}
