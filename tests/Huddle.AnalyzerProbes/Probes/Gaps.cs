using System.ComponentModel.DataAnnotations;
using System.Security.AccessControl;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Agency.Huddle.AnalyzerProbes;

// Probes for rules that needed a second, more careful attempt before they fired. The shape that works
// is the point: several obvious violations of these rules do not raise them.
// The file is meant to be wrong; do not copy anything from it.

public class ValidatedModel
{
    [Required]
    public string? Name { get; set; }

    public int Count { get; set; }
}

public class GapsA
{
    // probe: S1172
    private static int Helper(int used, int unused) { return used; }

    public int Call() => Helper(1, 2);

    public object Cast() { /* probe: S1944 */ return (IDisposable)new GapsA(); }

    public void Permissions()
    {
        // probe: S2612
        FileSecurity security = new();
        security.AddAccessRule(new FileSystemAccessRule("Everyone", FileSystemRights.FullControl, AccessControlType.Allow));
    }

    public Uri ClearText() { /* probe: S5332 */ return new Uri("http://api.mycompany.net/path"); }

    public byte[] Padding(RSA rsa, byte[] data) { /* probe: S5542 */ return rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1); }
}

public class GapsInit
{
    // probe: S3604
    public int Prop { get; } = 5;

    public GapsInit() { Prop = 1; }
}

public class GapsController : Controller
{
    // probe: S6967
    [HttpPost]
    public IActionResult Post(ValidatedModel model) { return Ok(model); }
}
