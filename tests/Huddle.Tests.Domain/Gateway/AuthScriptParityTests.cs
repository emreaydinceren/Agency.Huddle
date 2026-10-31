using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Agency.Huddle.App.Gateway;

namespace Agency.Huddle.Tests.Gateway;

/// <summary>
/// Spec 6.6: the connect page's JavaScript (<c>wwwroot/auth-core.js</c>, run under <c>node</c>) and the server's C# agree byte for byte
/// on the v3 payload and the device id, and the server verifies what the script signs. Needs <c>node</c> on PATH; the test fails, not skips, without it.
/// </summary>
public sealed class AuthScriptParityTests
{
    private const string Runner = """
        import { register } from 'node:module';
        import { pathToFileURL } from 'node:url';
        import path from 'node:path';

        const wwwroot = process.argv[2];
        const hooks = `
        const crypto = ${JSON.stringify(pathToFileURL(path.join(wwwroot, 'lib/noble/hashes/crypto.js')).href)};
        export async function resolve(specifier, context, next) {
            return specifier === '@noble/hashes/crypto' ? { url: crypto, shortCircuit: true } : next(specifier, context);
        }
        export async function load(url, context, next) {
            return next(url, url.endsWith('.js') ? { ...context, format: 'module' } : context);
        }`;
        register('data:text/javascript,' + encodeURIComponent(hooks));

        const core = await import(pathToFileURL(path.join(wwwroot, 'auth-core.js')).href);
        const vectors = JSON.parse(await new Promise((resolve) => {
            let text = '';
            process.stdin.on('data', (chunk) => { text += chunk; });
            process.stdin.on('end', () => resolve(text));
        }));
        const results = [];
        for (const v of vectors) {
            const seed = Uint8Array.from(Buffer.from(v.seedHex, 'hex'));
            const publicKey = await core.getPublicKey(seed);
            const deviceId = await core.deviceIdFromPublicKey(publicKey);
            const payload = core.buildPayloadV3(deviceId, v.scopes, v.signedAtMs, v.credential, v.nonce, v.platform, v.deviceFamily);
            const signature = await core.signPayload(seed, payload);
            results.push({ payload, publicKey: core.toBase64Url(publicKey), deviceId, signature: core.toBase64Url(signature) });
        }
        process.stdout.write(JSON.stringify(results));
        """;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Vector(string SeedHex, string[] Scopes, long SignedAtMs, string Credential, string Nonce, string Platform, string DeviceFamily);

    private sealed record Output(string Payload, string PublicKey, string DeviceId, string Signature);

    private static readonly Vector[] Vectors =
    [
        new(new string('0', 62) + "01", ["operator.write", "operator.read"], 1_761_815_400_000, "tok-123", "bm9uY2U", "windows", "desktop"),
        new(new string('a', 64), ["operator.write", "operator.admin", "operator.read", "operator.pairing"], 1_761_815_400_123, string.Empty, "AbC-_9", "  Win32 ", "\tMobile  "),
        new("00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", ["operator.admin"], 1, "dev-token", "n", "MacIntel", "Desktop"),
    ];

    /// <summary>For three fixed vectors the script's payload string is exactly the one <see cref="DeviceAuth.BuildPayloadV3"/> builds.</summary>
    [Fact]
    public async Task BuildPayloadV3_ScriptAndServer_ProduceIdenticalBytes()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Output[] results = await RunScriptAsync(ct);

        for (int index = 0; index < Vectors.Length; index++)
        {
            Vector vector = Vectors[index];
            string expected = DeviceAuth.BuildPayloadV3(results[index].DeviceId, vector.Scopes, vector.SignedAtMs, vector.Credential, vector.Nonce, vector.Platform, vector.DeviceFamily);
            Assert.Equal(expected, results[index].Payload);
        }
    }

    /// <summary>The script's device id is the one <see cref="DeviceAuth.DeviceIdOf"/> derives from the public key the script produced, and that key is the seed's.</summary>
    [Fact]
    public async Task DeviceId_ScriptAndServer_AgreeForTheSameSeed()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Output[] results = await RunScriptAsync(ct);

        for (int index = 0; index < Vectors.Length; index++)
        {
            TestDevice device = new(Convert.FromHexString(Vectors[index].SeedHex));
            Assert.Equal(device.PublicKeyText, results[index].PublicKey);
            Assert.Equal(device.DeviceId, results[index].DeviceId);
            Assert.Equal(DeviceAuth.DeviceIdOf(Base64Url.DecodeFromChars(results[index].PublicKey)), results[index].DeviceId);
        }
    }

    /// <summary>The server's verifier accepts every signature the script produced, as a handshake whose fields are the vector's.</summary>
    [Fact]
    public async Task Signature_ScriptSigned_VerifiesWithTheServer()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        Output[] results = await RunScriptAsync(ct);

        for (int index = 0; index < Vectors.Length; index++)
        {
            Vector vector = Vectors[index];
            ConnectRequest request = new(
                results[index].DeviceId,
                results[index].PublicKey,
                results[index].Signature,
                vector.SignedAtMs,
                vector.Nonce,
                vector.Platform,
                vector.DeviceFamily,
                DeviceAuth.Role,
                vector.Scopes,
                new ConnectAuth(DeviceToken: vector.Credential.Length == 0 ? null : vector.Credential));

            Assert.Null(DeviceAuth.Verify(request, vector.SignedAtMs));
        }
    }

    private static async Task<Output[]> RunScriptAsync(CancellationToken ct)
    {
        string wwwroot = Path.Combine(FindRepoRoot(), "src", "Huddle.App", "wwwroot");
        using TempDataDir dir = new();
        string script = Path.Combine(dir.Path, "auth-parity.mjs");
        await File.WriteAllTextAsync(script, Runner, ct);

        ProcessStartInfo startInfo = new("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            WorkingDirectory = dir.Path,
        };
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(wwwroot);

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("node did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(ct);
        Task<string> error = process.StandardError.ReadToEndAsync(ct);
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(Vectors, Json).AsMemory(), ct);
        process.StandardInput.Close();
        await process.WaitForExitAsync(ct);

        Assert.True(process.ExitCode == 0, $"node exited {process.ExitCode}: {await error}");
        Output[]? results = JsonSerializer.Deserialize<Output[]>(await output, Json);
        Assert.NotNull(results);
        Assert.Equal(Vectors.Length, results.Length);
        return results;
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Huddle.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"Could not find Huddle.slnx above '{AppContext.BaseDirectory}'.");
    }
}
