using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using Agency.Huddle.App.Avatars;
using static Agency.Huddle.Tests.Ui.TeammateCardTestSupport;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// The avatar editor of <see cref="Agency.Huddle.App.Components.Shared.TeammateCard"/> - the three-way choice, live preview, upload validation - split out of <see cref="TeammateCardTests"/> so xUnit can run the classes in parallel.
/// </summary>
public sealed class TeammateCardAvatarTests
{
    /// <summary>
    /// A minimal but genuine PNG - the 8-byte signature plus a 13-byte IHDR chunk - the same bytes
    /// <see cref="AvatarEndpointTests"/> uses, so an upload test is honest about what a real PNG
    /// header looks like rather than an arbitrary byte string that happens to start right.
    /// </summary>
    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x00, 0x00, 0x00, 0x00, 0x3A, 0x7E, 0x9B,
        0x55,
    ];

    /// <summary>Create mode offers the three-way avatar choice - initials, a short label, or an uploaded image - alongside the rest of the Create form.</summary>
    [Fact]
    public async Task CreateMode_ShowsTheThreeWayAvatarChoice()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");

        Assert.Contains("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("An image", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>View mode renders the avatar itself but none of its editing controls - the avatar counterpart of Model and Effort rendering as plain text there, never their selects.</summary>
    [Fact]
    public async Task ViewMode_ShowsNoAvatarEditingControls()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");

        Assert.DoesNotContain("Initials of the name", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("A short label", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose an image", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The frozen-parameter proof, and the most valuable test in this file: MudBlazor freezes a
    /// dialog's own <c>[Parameter]</c>s at the moment it opens (see the file-level comment on
    /// <c>TeammateCard.razor</c>), so choosing "A short label" and typing can only reach the preview
    /// <c>TeammateAvatar</c> renders because that preview is an ordinary CHILD render reading a LOCAL
    /// field (<c>CurrentAvatar</c>), never a re-pushed parameter, inside this SAME already-open dialog.
    /// </summary>
    [Fact]
    public async Task EditMode_ChoosingLabelAndTyping_UpdatesThePreviewAvatarInsideTheSameOpenDialog()
    {
        await using var factory = new TeamWebApplicationFactory();
        await SeedPersonaAsync(factory, "coo", "x");
        await using var ctx = NewContext(factory);

        var cut = await OpenViewCardAsync(ctx, factory, "coo");
        await ClickButtonAsync(cut, "Edit");

        await SelectAvatarChoiceAsync(cut, "A short label");
        await SetImmediateTextValueAsync(cut, "Label", "AB");

        Assert.Equal("AB", cut.Find(".mud-avatar").TextContent.Trim());
    }

    /// <summary>Choosing a file but Cancelling leaves <c>{DataDir}/avatars</c> untouched - the buffer-until-save proof: nothing reaches disk before Save (Part C's own contract).</summary>
    [Fact]
    public async Task CreateMode_ChooseAFileThenCancel_LeavesTheAvatarsDirectoryEmpty()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SelectAvatarChoiceAsync(cut, "An image");

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(MinimalPng, "avatar.png", contentType: "image/png"));

        await ClickButtonAsync(cut, "Cancel");

        // Program.cs creates {DataDir}/avatars unconditionally at startup so the static-file
        // middleware always has a directory to point at (see AvatarEndpointTests) - so its mere
        // existence proves nothing here. Emptiness is the actual proof: nothing was ever WRITTEN
        // into it, because Cancel never reached SaveAsync, the only place that calls WriteImage.
        var avatarsDir = Path.Combine(DataDirOf(factory), "avatars");
        Assert.Empty(Directory.EnumerateFileSystemEntries(avatarsDir));
    }

    /// <summary>An oversized upload sets <c>avatarError</c> and keeps the card open, rather than throwing or silently accepting it.</summary>
    [Fact]
    public async Task CreateMode_OversizedImage_SetsAnErrorAndKeepsTheCardOpen()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SelectAvatarChoiceAsync(cut, "An image");

        var oversized = new byte[AvatarImage.MaxBytes + 1];
        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary(oversized, "big.png", contentType: "image/png"));

        Assert.Contains("larger than", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll(".mud-dialog-container"));
    }

    /// <summary>An upload whose bytes are not a PNG, JPEG or WebP signature sets <c>avatarError</c> with the exact wording <c>AvatarImage.SniffExtension</c>'s caller promises.</summary>
    [Fact]
    public async Task CreateMode_UploadedFileIsNotARecognisedImage_SetsAnError()
    {
        await using var factory = new TeamWebApplicationFactory();
        await using var ctx = NewContext(factory);

        var cut = RenderPage(ctx, factory);
        await ClickButtonAsync(cut, "New teammate");
        await SelectAvatarChoiceAsync(cut, "An image");

        var inputFile = cut.FindComponent<InputFile>();
        inputFile.UploadFiles(InputFileContent.CreateFromBinary([1, 2, 3, 4], "fake.png", contentType: "image/png"));

        Assert.Contains("That file is not a PNG, JPEG or WebP image.", cut.Markup, StringComparison.Ordinal);
    }
}
