using System.Reflection;
using Agency.Huddle.App.Library;

namespace Agency.Huddle.Tests.Library;

/// <summary>Pins the direction of the Library to DocHistory dependency (S-A6): the Library publishes events and names no DocHistory type.</summary>
public sealed class LibraryDependencyTests
{
    private const string DocHistoryNamespace = "Agency.Huddle.App.DocHistory";

    /// <summary>No constructor parameter of <see cref="LibraryFileService"/> has a type in the DocHistory namespace.</summary>
    [Fact]
    public void LibraryFileService_ConstructorParameters_NameNoDocHistoryType()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        List<string> offending = [.. typeof(LibraryFileService)
            .GetConstructors(all)
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => InDocHistory(parameter.ParameterType))
            .Select(parameter => $"{parameter.Name}: {parameter.ParameterType.FullName}")];

        Assert.Empty(offending);
    }

    /// <summary>No field of <see cref="LibraryFileService"/> has a type in the DocHistory namespace.</summary>
    [Fact]
    public void LibraryFileService_Fields_NameNoDocHistoryType()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        List<string> offending = [.. typeof(LibraryFileService)
            .GetFields(all)
            .Where(field => InDocHistory(field.FieldType))
            .Select(field => $"{field.Name}: {field.FieldType.FullName}")];

        Assert.Empty(offending);
    }

    /// <summary>Whether <paramref name="type"/>, or a generic argument of it, lives in the DocHistory namespace.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when it names a DocHistory type.</returns>
    private static bool InDocHistory(Type type) =>
        string.Equals(type.Namespace, DocHistoryNamespace, StringComparison.Ordinal)
        || (type.IsGenericType && type.GetGenericArguments().Any(InDocHistory));
}
