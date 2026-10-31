using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Acp;

/// <summary>Exercises the reserved <c>_role</c> and <c>_packs</c> frontmatter keys (Spec §Hired).</summary>
public sealed class PersonaFrontmatterRoleTests
{
    private const string Header = "---\nName: Dev\nTitle: Developer\nAlias: dev\n";

    /// <summary>Both keys read back into <see cref="PersonaIdentity"/>.</summary>
    [Fact]
    public void TryReadIdentity_RoleAndPacks_ReadsBoth()
    {
        string text = Header + "_role: development/developer@1.0.0\n_packs: [msbuild-authoring@1.0.0, git@2.1.0]\n---\nbody";

        bool ok = PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out string error);

        Assert.True(ok, error);
        Assert.NotNull(identity);
        Assert.Equal(new RoleRef("development", "developer", "1.0.0"), identity.Role);
        Assert.Equal(
            [new VersionedRef("msbuild-authoring", "1.0.0"), new VersionedRef("git", "2.1.0")],
            identity.Packs);
    }

    /// <summary>Absent keys give a null role and an empty pack list.</summary>
    [Fact]
    public void TryReadIdentity_NoKeys_RoleNullPacksEmpty()
    {
        bool ok = PersonaFrontmatter.TryReadIdentity(Header + "---\nbody", out PersonaIdentity? identity, out _);

        Assert.True(ok);
        Assert.NotNull(identity);
        Assert.Null(identity.Role);
        Assert.NotNull(identity.Packs);
        Assert.Empty(identity.Packs);
    }

    /// <summary>An unparseable <c>_role</c> is ignored and the file still loads.</summary>
    [Fact]
    public void TryReadIdentity_BadRole_IgnoredAndStillLoads()
    {
        string text = Header + "_role: nonsense\n---\nbody";

        bool ok = PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? identity, out string error);

        Assert.True(ok, error);
        Assert.NotNull(identity);
        Assert.Null(identity.Role);
    }

    /// <summary>Compose then read returns the same role and packs.</summary>
    [Fact]
    public void Compose_RoleAndPacks_RoundTrips()
    {
        PersonaIdentity identity = new(
            "Dev",
            "Developer",
            "dev",
            [],
            Role: new RoleRef("development", "developer", "1.0.0"),
            Packs: [new VersionedRef("msbuild-authoring", "1.0.0")]);

        string text = PersonaFrontmatter.Compose(identity, "body");
        bool ok = PersonaFrontmatter.TryReadIdentity(text, out PersonaIdentity? read, out string error);

        Assert.True(ok, error);
        Assert.NotNull(read);
        Assert.Equal(identity.Role, read.Role);
        Assert.Equal(identity.Packs, read.Packs);
    }

    /// <summary>Compose writes neither key when unset.</summary>
    [Fact]
    public void Compose_NothingSet_WritesNoReservedKeys()
    {
        string text = PersonaFrontmatter.Compose(new PersonaIdentity("Dev", "Developer", "dev", []), "body");

        Assert.DoesNotContain("_role", text, StringComparison.Ordinal);
        Assert.DoesNotContain("_packs", text, StringComparison.Ordinal);
    }

    /// <summary>The job description never shows <c>_role</c>, <c>_packs</c> or any unknown <c>_x</c> key.</summary>
    [Fact]
    public void ComposeJobDescription_ReservedKeys_Excluded()
    {
        string text = Header + "_role: development/developer@1.0.0\n_packs: [a@1.0.0]\n_x: secret\nconsult_when: 'For code'\n---\nbody";

        string description = PersonaFrontmatter.ComposeJobDescription(text);

        string withoutReserved = PersonaFrontmatter.ComposeJobDescription(
            Header + "consult_when: 'For code'\n---\nbody");

        Assert.Equal(withoutReserved, description);
    }

    /// <summary>An unknown <c>_x</c> key survives rewriting another field.</summary>
    [Fact]
    public void WriteScalarField_UnknownUnderscoreKey_Survives()
    {
        string text = Header + "_role: development/developer@1.0.0\n_x: keep-me\n---\nbody";

        string updated = PersonaFrontmatter.WriteScalarField(text, "title", "Senior Developer");

        Assert.Equal(
            Header.Replace("Developer", "'Senior Developer'", StringComparison.Ordinal)
                + "_role: development/developer@1.0.0\n_x: keep-me\n---\nbody",
            updated);
    }
}
