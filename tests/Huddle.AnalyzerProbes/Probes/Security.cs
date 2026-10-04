using System.Data;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Xml;
using Microsoft.Extensions.Logging;

namespace Agency.Huddle.AnalyzerProbes;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public class SecurityA
{
    public string Password() { /* probe: S2068 */ string password = "Sup3rS3cret!"; return password; }

    public void Sql(IDbCommand command, string name)
    {
        // probe: S2077
        command.CommandText = "SELECT * FROM users WHERE name = '" + name + "'";
        command.ExecuteReader();
    }

    public int Seeded() { /* probe: S2245 */ Random random = new Random(1); return random.Next(); }

    public void Xml()
    {
        XmlDocument document = new();
        // probe: S2755
        document.XmlResolver = new XmlUrlResolver();
    }

    public SslProtocols Weak() { /* probe: S4423 */ return SslProtocols.Tls; }

    public RSA SmallKey() { /* probe: S4426 */ return RSA.Create(1024); }

    public HashAlgorithm Md5() { /* probe: S4790 */ return MD5.Create(); }

    public void Trust(System.Net.Http.HttpClientHandler handler) { /* probe: S4830 */ handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true; }

    public string TempDir() { /* probe: S5443 */ return Path.Combine("/tmp", "work.txt"); }

    public string TempFile() { /* probe: S5445 */ return Path.GetTempFileName(); }

    public SymmetricAlgorithm Des() { /* probe: S5547 */ return DES.Create(); }

    public Process? Spawn() { /* probe: S4036 */ return Process.Start("git"); }

    public byte[] Derive(string password, byte[] salt) { /* probe: S5344 */ return new Rfc2898DeriveBytes(password, salt, 1000, HashAlgorithmName.SHA1).GetBytes(16); }

    public async Task LogAndThrow(ILogger<SecurityA> logger)
    {
        try { await Task.Delay(1); }
        // probe: S2139
        catch (IOException ex)
        {
            logger.LogError(ex, "failed");
            throw;
        }
    }
}

// probe: S2257
public sealed class CustomHash : HashAlgorithm
{
    public override void Initialize() { }
    protected override void HashCore(byte[] array, int ibStart, int cbSize) { }
    protected override byte[] HashFinal() => new byte[1];
}
