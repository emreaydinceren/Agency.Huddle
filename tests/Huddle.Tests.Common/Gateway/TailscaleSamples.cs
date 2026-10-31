namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Captured-shape samples of the Tailscale CLI's JSON (the format is undocumented and "subject to change", Spec 6.3). The <c>serve status</c> shape follows
/// Tailscale's ServeConfig: <c>TCP</c> by port, <c>Web</c> keyed <c>host:port</c> with <c>Handlers</c> by path, each handler a <c>Proxy</c>, <c>Path</c> or <c>Text</c>.
/// </summary>
internal static class TailscaleSamples
{
    /// <summary>The MagicDNS name of the sample node, without the trailing dot.</summary>
    public const string DnsName = "desktop-a.tail1234.ts.net";

    /// <summary>A running node owned by user 1234.</summary>
    public const string StatusRunning = """
        {"Version":"1.78.1-t1a2b3c","TUN":true,"BackendState":"Running","HaveNodeKey":true,"AuthURL":"",
         "TailscaleIPs":["100.101.102.103","fd7a:115c:a1e0::6a01:6667"],
         "Self":{"ID":"nABCD1CNTRL","PublicKey":"nodekey:0000","HostName":"desktop-a","DNSName":"desktop-a.tail1234.ts.net.","OS":"windows","UserID":1234,
                 "TailscaleIPs":["100.101.102.103","fd7a:115c:a1e0::6a01:6667"],"Online":true},
         "MagicDNSSuffix":"tail1234.ts.net",
         "Peer":{},
         "User":{"1234":{"ID":1234,"LoginName":"Ada@example.com","DisplayName":"Ada","ProfilePicURL":""}}}
        """;

    /// <summary>A node that has not logged in.</summary>
    public const string StatusNeedsLogin = """
        {"Version":"1.78.1-t1a2b3c","BackendState":"NeedsLogin","AuthURL":"https://login.tailscale.com/a/abc123","TailscaleIPs":null,"Self":null,"Peer":null,"User":null}
        """;

    /// <summary>A node whose Tailscale is switched off.</summary>
    public const string StatusStopped = """
        {"Version":"1.78.1-t1a2b3c","BackendState":"Stopped","AuthURL":"","TailscaleIPs":null,
         "Self":{"ID":"nABCD1CNTRL","HostName":"desktop-a","DNSName":"desktop-a.tail1234.ts.net.","UserID":1234,"TailscaleIPs":["100.101.102.103"],"Online":false},
         "Peer":{},"User":{"1234":{"ID":1234,"LoginName":"Ada@example.com","DisplayName":"Ada"}}}
        """;

    /// <summary>The route Huddle's claim creates: tsPort 5101 to the managed listener on 41233.</summary>
    public const string ServeClaimed = """
        {"TCP":{"5101":{"HTTPS":true}},
         "Web":{"desktop-a.tail1234.ts.net:5101":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:41233"}}}}}
        """;

    /// <summary>Port 5101 belongs to somebody else's route (a different local port); a second, hand-made route on 443 proxies to Huddle's app port 5101.</summary>
    public const string ServeForeignAndStray = """
        {"TCP":{"5101":{"HTTPS":true},"443":{"HTTPS":true}},
         "Web":{"desktop-a.tail1234.ts.net:5101":{"Handlers":{"/":{"Proxy":"http://127.0.0.1:3000"}}},
                "desktop-a.tail1234.ts.net:443":{"Handlers":{"/":{"Proxy":"http://localhost:5101"},"/files":{"Path":"C:\\Share"}}}}}
        """;

    /// <summary>A whois answer for a user-owned node.</summary>
    public const string WhoisUser = """
        {"Node":{"ID":77,"StableID":"nPHONE1CNTRL","Name":"phone.tail1234.ts.net.","Tags":null},
         "UserProfile":{"ID":1234,"LoginName":"ada@example.com","DisplayName":"Ada"},"CapMap":{}}
        """;

    /// <summary>A whois answer for a tagged node: it has no user identity.</summary>
    public const string WhoisTagged = """
        {"Node":{"ID":78,"Name":"server.tail1234.ts.net.","Tags":["tag:server"]},
         "UserProfile":{"ID":99,"LoginName":"tagged-devices","DisplayName":"Tagged devices"},"CapMap":{}}
        """;
}
