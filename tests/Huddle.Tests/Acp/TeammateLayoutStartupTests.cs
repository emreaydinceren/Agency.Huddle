using Agency.Huddle.App.Acp;
using Agency.Huddle.Tests.Ui;
using Microsoft.Extensions.DependencyInjection;

namespace Agency.Huddle.Tests.Acp;

/// <summary>
/// Pins Spec §6.15's start-up ordering: <see cref="TeammateLayoutMigration"/> runs before
/// <see cref="PersonaStore"/>'s constructor scan, so an old <c>Teams/</c> layout already on disk is
/// migrated before the first thing that reads Personas resolves.
/// </summary>
public sealed class TeammateLayoutStartupTests
{
    /// <summary>
    /// An old-layout definition seeded before the host starts is migrated to
    /// <c>Teammates/&lt;Name&gt;/&lt;Name&gt;.md</c> before <see cref="PersonaStore"/>'s constructor
    /// scan runs, so the first resolve already finds it at its new path.
    /// </summary>
    [Fact]
    public void Startup_OldLayout_IsMigratedBeforePersonasLoad()
    {
        using TeamWebApplicationFactory factory = new();
        WriteTeamsFile(factory.DataDirPath, "Nova.md", PersonaText("Nova"));

        PersonaStore personaStore = factory.Services.GetRequiredService<PersonaStore>();

        Assert.NotNull(personaStore.Get("Nova"));
        Assert.True(File.Exists(Path.Combine(factory.TeammatesDirPath, "Nova", "Nova.md")));
    }

    /// <summary>
    /// When the migration cannot complete (its target is blocked, as in
    /// <c>TeammateLayoutMigrationTests.Run_MoveFails_ThrowsAndLeavesOldLayoutReadable</c>), resolving
    /// the host throws, and walking <see cref="Exception.InnerException"/> from that failure finds an
    /// exception naming the offending path.
    /// </summary>
    [Fact]
    public void Startup_MigrationFails_AppDoesNotStart()
    {
        using TeamWebApplicationFactory factory = new();
        WriteTeamsFile(factory.DataDirPath, "Nova.md", PersonaText("Nova"));
        Directory.CreateDirectory(factory.TeammatesDirPath);
        File.WriteAllText(Path.Combine(factory.TeammatesDirPath, "Nova"), "this is a file, not a folder");

        Exception thrown = Assert.ThrowsAny<Exception>(() => factory.Services);

        Exception? current = thrown;
        while (current is not null && !current.Message.Contains("Nova", StringComparison.Ordinal))
        {
            current = current.InnerException;
        }

        Assert.NotNull(current);
    }

    /// <summary>Writes a file under the old <c>{DataDir}/Teams/</c> layout, creating parent folders as needed.</summary>
    private static void WriteTeamsFile(string dataDir, string relativePath, string text)
    {
        string path = Path.Combine(dataDir, "Teams", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>A minimal valid Persona/Teammate definition body for <paramref name="name"/>.</summary>
    private static string PersonaText(string name) => $"---\nName: {name}\nTitle: {name}\nAlias: {name}\n---\nYou are {name}.";
}
