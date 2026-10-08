using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>Exercises <see cref="VersionedRef"/> and <see cref="RoleRef"/> parsing and formatting.</summary>
public sealed class RefsTests
{
    /// <summary>A pack reference parses and formats back to the same text.</summary>
    [Fact]
    public void VersionedRef_Valid_RoundTrips()
    {
        bool ok = VersionedRef.TryParse("msbuild-authoring@1.0.0", out VersionedRef? value);

        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal(new VersionedRef("msbuild-authoring", "1.0.0"), value);
        Assert.Equal("msbuild-authoring@1.0.0", value.ToString());
    }

    /// <summary>Malformed pack references are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("noversion")]
    [InlineData("@1.0.0")]
    [InlineData("a@")]
    [InlineData("a@b@c")]
    public void VersionedRef_Invalid_Rejected(string text)
    {
        Assert.False(VersionedRef.TryParse(text, out _));
    }

    /// <summary>A role reference parses and formats back to the same text.</summary>
    [Fact]
    public void RoleRef_Valid_RoundTrips()
    {
        bool ok = RoleRef.TryParse("development/developer@1.0.0", out RoleRef? value);

        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal(new RoleRef("development", "developer", "1.0.0"), value);
        Assert.Equal("development/developer@1.0.0", value.ToString());
    }

    /// <summary>Malformed role references are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("developer@1.0.0")]
    [InlineData("development/@1.0.0")]
    [InlineData("/developer@1.0.0")]
    [InlineData("development/developer")]
    [InlineData("a/b/c@1")]
    public void RoleRef_Invalid_Rejected(string text)
    {
        Assert.False(RoleRef.TryParse(text, out _));
    }
}
