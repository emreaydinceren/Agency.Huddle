using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Avatars;

namespace Agency.Huddle.Tests.Avatars;

/// <summary>
/// Tests for <see cref="AvatarStore"/>: that it joins the Human and every Persona's Name with an
/// override file, tolerates every shape of a missing or malformed file without throwing, never drops
/// an entry it is not currently touching, resolves lookups case-insensitively while preserving the
/// key exactly as written, and keeps an uploaded image's opaque file name independent of the Name it
/// belongs to.
/// </summary>
public sealed class AvatarStoreTests
{
    /// <summary>A missing override file is the normal first-run case: every Name resolves to <see cref="Avatar.None"/> and the constructor never creates one.</summary>
    [Fact]
    public void Get_WithNoFile_ReturnsNoneAndCreatesNothing()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        Assert.Equal(Avatar.None, store.Get("Jarvis"));
        Assert.False(File.Exists(Path.Combine(dataDir.Path, "avatars.json")));
    }

    /// <summary>Malformed JSON falls back to an empty set wholesale, and does not throw.</summary>
    [Fact]
    public void Get_WithMalformedFile_FallsBackToEmpty()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(Path.Combine(dataDir.Path, "avatars.json"), "{ this is not valid json");

        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        Assert.Equal(Avatar.None, store.Get("Jarvis"));
    }

    /// <summary>An entry for a Name this store is not currently touching survives a <see cref="AvatarStore.Save"/> of a different Name.</summary>
    [Fact]
    public void Save_WithAnUnrelatedEntryAlreadyOnDisk_LeavesItUntouched()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "avatars.json");
        File.WriteAllText(path, "{\"Ghost\":{\"background\":\"#000000\"}}");

        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        store.Save("Jarvis", new Avatar(Label: null, Image: null, Background: "#4a154b"));

        var json = File.ReadAllText(path);
        Assert.Contains("Ghost", json, StringComparison.Ordinal);
        Assert.Contains("#000000", json, StringComparison.Ordinal);
    }

    /// <summary>Saving an <see cref="Avatar.IsDefault"/> avatar removes the Name's entry entirely rather than writing an empty object.</summary>
    [Fact]
    public void Save_WithADefaultAvatar_RemovesTheEntry()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "avatars.json");
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        store.Save("Jarvis", new Avatar(Label: null, Image: null, Background: "#4a154b"));

        store.Save("Jarvis", Avatar.None);

        Assert.Equal(Avatar.None, store.Get("Jarvis"));
        Assert.DoesNotContain("Jarvis", File.ReadAllText(path), StringComparison.Ordinal);
    }

    /// <summary>A lookup matches an entry regardless of the case the Name is spelled in.</summary>
    [Fact]
    public void Get_WithDifferentCase_FindsTheStoredEntry()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        store.Save("Jarvis", new Avatar(Label: null, Image: null, Background: "#4a154b"));

        Assert.Equal("#4a154b", store.Get("jarvis").Background);
    }

    /// <summary><see cref="AvatarStore.Rename"/> moves the entry to the new Name and leaves the image file it references untouched on disk.</summary>
    [Fact]
    public void Rename_MovesTheEntryAndLeavesTheImageFileOnDisk()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        var imageFile = store.WriteImage([0x89, 0x50, 0x4E, 0x47], ".png");
        store.Save("Jarvis", new Avatar(Label: null, Image: imageFile, Background: null));

        store.Rename("Jarvis", "JarvisPrime");

        Assert.Equal(Avatar.None, store.Get("Jarvis"));
        Assert.Equal(imageFile, store.Get("JarvisPrime").Image);
        Assert.True(File.Exists(Path.Combine(store.ImageDirectory, imageFile)));
    }

    /// <summary><see cref="AvatarStore.Remove"/> deletes both the entry and the image file it references.</summary>
    [Fact]
    public void Remove_DeletesTheEntryAndItsImageFile()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        var imageFile = store.WriteImage([0x89, 0x50, 0x4E, 0x47], ".png");
        store.Save("Jarvis", new Avatar(Label: null, Image: imageFile, Background: null));

        store.Remove("Jarvis");

        Assert.Equal(Avatar.None, store.Get("Jarvis"));
        Assert.False(File.Exists(Path.Combine(store.ImageDirectory, imageFile)));
    }

    /// <summary>
    /// A single-codepoint emoji label round-trips through the file unescaped, because
    /// <c>avatars.json</c> is meant to be hand-readable. Deliberately U+2615 (within the Basic
    /// Multilingual Plane), not a full-colour emoji built from a UTF-16 surrogate pair: System.Text.Json
    /// writes every character outside the BMP as a <c>\uXXXX\uXXXX</c> escape regardless of
    /// <see cref="System.Text.Encodings.Web.JavaScriptEncoder"/> configuration, so that half of the
    /// AvatarStore's encoder choice is not something this test - or any encoder setting - can prove.
    /// </summary>
    [Fact]
    public void Save_WithAnEmojiLabel_RoundTripsUnescaped()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "avatars.json");
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        store.Save("Jarvis", new Avatar(Label: "☕", Image: null, Background: null));

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);
        Assert.Equal("☕", store.Get("Jarvis").Label);
    }

    /// <summary>A label of seven text elements read from the file is trimmed to three, the same tolerance an unknown theme id gets.</summary>
    [Fact]
    public void Get_WithALabelOfSevenTextElements_TrimsToThree()
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "avatars.json");
        File.WriteAllText(path, "{\"Jarvis\":{\"label\":\"1234567\"}}");

        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        Assert.Equal("123", store.Get("Jarvis").Label);
    }

    /// <summary><see cref="AvatarStore.WriteImage"/> returns an opaque file name, never the Teammate's Name, and creates the file on disk.</summary>
    [Fact]
    public void WriteImage_ReturnsAnOpaqueNameAndCreatesTheFile()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        var fileName = store.WriteImage([0x89, 0x50, 0x4E, 0x47], ".png");

        Assert.NotEqual("Jarvis.png", fileName);
        Assert.True(File.Exists(Path.Combine(store.ImageDirectory, fileName)));
    }

    /// <summary><see cref="AvatarStore.DeleteImage"/> is a no-op, never a throw, for a <see langword="null"/> or already-absent file name.</summary>
    [Fact]
    public void DeleteImage_WithNullOrMissingFile_DoesNothing()
    {
        using var dataDir = new TempDataDir();
        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        store.DeleteImage(null);
        store.DeleteImage("does-not-exist.png");

        Assert.False(File.Exists(Path.Combine(store.ImageDirectory, "does-not-exist.png")));
    }

    /// <summary>
    /// A hand-edited <c>background</c> that is not a colour resolves to <see langword="null"/> rather
    /// than reaching a render, because <c>MudColor</c>'s string constructor throws on one and a Razor
    /// render that throws on Blazor Server tears down the circuit - so one mistyped colour would take
    /// out the page rather than one avatar. The rest of the entry survives, and the file is untouched.
    /// </summary>
    /// <param name="background">A <c>background</c> value <c>MudColor</c> cannot parse.</param>
    [Theory]
    [InlineData("not-a-colour")]
    [InlineData("zzz")]
    [InlineData("")]
    public void Get_WithABackgroundThatIsNotAColour_FallsBackToTheThemeAndKeepsTheRest(string background)
    {
        using var dataDir = new TempDataDir();
        var path = Path.Combine(dataDir.Path, "avatars.json");
        var json = string.Create(
            CultureInfo.InvariantCulture,
            $"{{\"Jarvis\":{{\"label\":\"JA\",\"background\":\"{background}\"}}}}");
        File.WriteAllText(path, json);

        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);
        var avatar = store.Get("Jarvis");

        Assert.Null(avatar.Background);
        Assert.Equal("JA", avatar.Label);
        Assert.Equal(json, File.ReadAllText(path));
    }

    /// <summary>A well-formed <c>background</c> is kept exactly as written, so the guard above rejects only what genuinely cannot be parsed.</summary>
    [Fact]
    public void Get_WithAValidBackground_KeepsItExactly()
    {
        using var dataDir = new TempDataDir();
        File.WriteAllText(
            Path.Combine(dataDir.Path, "avatars.json"),
            "{\"Jarvis\":{\"background\":\"#4a154b\"}}");

        using var store = new AvatarStore(dataDir.Options(), NullLogger<AvatarStore>.Instance);

        Assert.Equal("#4a154b", store.Get("Jarvis").Background);
    }
}
