using System.Reflection;
using Agency.Huddle.App;
using Agency.Huddle.App.Avatars;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Pins requirement R-1: the Avatar Library catalogue lives only while the dialog is open. The loader holds
/// no catalogue, nothing in the Avatars namespace keeps one in a static field, and each load reads the file afresh.
/// </summary>
public sealed class AvatarLibraryMemoryTests : IDisposable
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly string root = Path.Combine(Path.GetTempPath(), "huddle-avatar-memory-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the per-test library folder.</summary>
    public AvatarLibraryMemoryTests()
    {
        Directory.CreateDirectory(this.root);
    }

    /// <summary>Removes the per-test folder and everything under it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(this.root))
        {
            Directory.Delete(this.root, recursive: true);
        }
    }

    /// <summary>The loader's only instance state is its locator and its logger, and it has no static state.</summary>
    [Fact]
    public void Loader_IsStateless_HoldsNoCatalogue()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        FieldInfo[] fields = typeof(AvatarLibraryLoader).GetFields(all);

        FieldInfo[] variable = [.. fields.Where(f => !f.IsLiteral)];

        Assert.DoesNotContain(variable, f => f.IsStatic);
        Assert.Equal(2, variable.Length);
        Assert.Contains(variable, f => f.FieldType == typeof(AvatarLibraryLocator));
        Assert.Contains(variable, f => f.FieldType == typeof(Microsoft.Extensions.Logging.ILogger<AvatarLibraryLoader>));
    }

    /// <summary>No type in the Avatars namespace has a static field that is, or contains, a catalogue or avatar record.</summary>
    [Fact]
    public void NoStaticCatalogueField()
    {
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        List<string> offenders = [];
        foreach (Type type in typeof(AvatarCatalog).Assembly.GetTypes().Where(t => string.Equals(t.Namespace, "Agency.Huddle.App.Avatars", StringComparison.Ordinal)))
        {
            foreach (FieldInfo field in type.GetFields(statics))
            {
                bool isDelegateCache = typeof(Delegate).IsAssignableFrom(field.FieldType);
                if (!isDelegateCache && HoldsCatalogueData(field.FieldType))
                {
                    offenders.Add(type.Name + "." + field.Name);
                }
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>Editing <c>master.json</c> between two loads shows in the second load: nothing is cached.</summary>
    [Fact]
    public async Task Reopen_ReadsFreshFile()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        File.WriteAllBytes(Path.Combine(this.root, "a.png"), PngBytes);
        File.WriteAllBytes(Path.Combine(this.root, "b.png"), PngBytes);
        string master = Path.Combine(this.root, "master.json");
        File.WriteAllText(master, """{"avatars":[{"file":"a.png","profession":"Chef"}]}""");
        TeamOptions options = new() { AvatarLibrary = new AvatarLibraryOptions { Dir = this.root } };
        AvatarLibraryLoader loader = new(new AvatarLibraryLocator(Options.Create(options)), NullLogger<AvatarLibraryLoader>.Instance);

        AvatarLibraryLoadResult first = await loader.LoadAsync(ct);
        File.WriteAllText(master, """{"avatars":[{"file":"a.png","profession":"Chef"},{"file":"b.png","profession":"Pilot"}]}""");
        AvatarLibraryLoadResult second = await loader.LoadAsync(ct);

        Assert.NotNull(first.Catalog);
        Assert.Equal(1, first.Catalog.Count);
        Assert.NotNull(second.Catalog);
        Assert.Equal(2, second.Catalog.Count);
        Assert.NotSame(first.Catalog, second.Catalog);
    }

    private static bool HoldsCatalogueData(Type type)
    {
        if (type == typeof(AvatarCatalog) || type == typeof(LibraryAvatar))
        {
            return true;
        }

        if (type.HasElementType && type.GetElementType() is Type element)
        {
            return HoldsCatalogueData(element);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(HoldsCatalogueData);
    }
}
