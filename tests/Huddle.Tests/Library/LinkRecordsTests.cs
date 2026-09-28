using System.Reflection;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Tests for WikiLink and LibraryReference records (Spec §6.5, §6.6).</summary>
public sealed class LinkRecordsTests
{
    /// <summary>WikiLink equality is by value, not by reference.</summary>
    [Fact]
    public void WikiLink_Equality_IsByValue()
    {
        WikiLink a = new("target", Heading: null, Alias: null, IsEmbed: false, Line: 1, Start: 10, Length: 10);
        WikiLink b = new("target", Heading: null, Alias: null, IsEmbed: false, Line: 1, Start: 10, Length: 10);

        Assert.Equal(a, b);
    }

    /// <summary>LibraryReference equality is by value, not by reference.</summary>
    [Fact]
    public void LibraryReference_Equality_IsByValue()
    {
        LibraryReference a = new("rootId", "path/to/file.md", Exists: true);
        LibraryReference b = new("rootId", "path/to/file.md", Exists: true);

        Assert.Equal(a, b);
    }

    /// <summary>ILibraryReferenceResolver has exactly two members: ResolvePath and ResolveWikiLink.</summary>
    [Fact]
    public void ILibraryReferenceResolver_HasTwoMembers()
    {
        Type type = typeof(ILibraryReferenceResolver);
        BindingFlags instanceBinding = BindingFlags.Public | BindingFlags.Instance;
        MethodInfo[] methods = type.GetMethods(instanceBinding | BindingFlags.DeclaredOnly);

        Assert.Equal(2, methods.Length);

        MethodInfo resolvePath = type.GetMethod("ResolvePath", instanceBinding) ?? throw new InvalidOperationException("ResolvePath not found");
        MethodInfo resolveWikiLink = type.GetMethod("ResolveWikiLink", instanceBinding) ?? throw new InvalidOperationException("ResolveWikiLink not found");

        // Verify ResolvePath signature: LibraryReference? ResolvePath(string)
        Assert.Single(resolvePath.GetParameters());
        Assert.Equal(typeof(string), resolvePath.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(LibraryReference), resolvePath.ReturnType);

        NullabilityInfoContext nullabilityContext = new();
        NullabilityState resolvePathNullability = nullabilityContext.Create(resolvePath.ReturnParameter).ReadState;
        Assert.Equal(NullabilityState.Nullable, resolvePathNullability);

        // Verify ResolveWikiLink signature: LibraryReference? ResolveWikiLink(LibraryPath, WikiLink)
        ParameterInfo[] wikiLinkParams = resolveWikiLink.GetParameters();
        Assert.Equal(2, wikiLinkParams.Length);
        Assert.Equal(typeof(LibraryPath), wikiLinkParams[0].ParameterType);
        Assert.Equal(typeof(WikiLink), wikiLinkParams[1].ParameterType);
        Assert.Equal(typeof(LibraryReference), resolveWikiLink.ReturnType);

        NullabilityState resolveWikiLinkNullability = nullabilityContext.Create(resolveWikiLink.ReturnParameter).ReadState;
        Assert.Equal(NullabilityState.Nullable, resolveWikiLinkNullability);
    }
}
