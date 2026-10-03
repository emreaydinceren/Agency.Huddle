using System.DirectoryServices;
using System.Security.Cryptography.Xml;
using System.Xml;
using Microsoft.EntityFrameworkCore;

namespace Agency.Huddle.AnalyzerProbes.Packages;

// Each "// probe: <RuleId>" tag is answered by that rule firing on the next few lines.
// The file is meant to be wrong; do not copy anything from it.

public class PackageProbes
{
    public DirectoryEntry Anonymous() { /* probe: S4433 */ return new DirectoryEntry("LDAP://ldap.mycompany.net", null, null, AuthenticationTypes.Anonymous); }

    public bool Signature(XmlDocument document)
    {
        // probe: S6377
        SignedXml signed = new(document);
        return signed.CheckSignature();
    }
}

// probe: S3363
public class Event
{
    public DateTime Id { get; set; }
}

public class ProbeContext : DbContext
{
    public DbSet<Event> Events => this.Set<Event>();
}
