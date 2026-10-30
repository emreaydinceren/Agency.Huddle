using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Agency.Huddle.App.Acp;
using Agency.Huddle.App.Components.Library;
using Agency.Huddle.App.Library;
using Agency.Huddle.Tests.Library;

namespace Agency.Huddle.Tests.Ui.Library;

/// <summary>Pins the Properties block's <c>tags</c> row against the tag index (Spec §6.17): it draws what <see cref="NoteTagReader"/> reads and never presents a skipped value as a chip.</summary>
public sealed class LibraryPropertiesTagsTests
{
    private static IRenderedComponent<LibraryProperties> Render(MudBunitContext ctx, string block)
    {
        ctx.Services.AddSingleton<ILibraryNoteResolver>(new FakeLibraryNoteResolver());
        ctx.Services.AddSingleton(TimeProvider.System);
        IReadOnlyList<NoteProperty> properties = PropertyTyping.Apply(
            NoteProperties.Read(NoteFrontmatter.Locate($"---\n{block}\n---\n")),
            new Dictionary<string, NotePropertyKind>());
        IReadOnlyList<string> tags = NoteTagReader.Read(block, NoteFrontmatterForm.Yaml);
        return ctx.Render<LibraryProperties>(p => p
            .Add(c => c.Properties, properties)
            .Add(c => c.Tags, tags));
    }

    /// <summary>A block list, a quoted <c>#</c> and a comma string all draw as chips without the quotes or the hash.</summary>
    [Theory]
    [InlineData("tags:\n- a\n- \"#b\"", "a|b")]
    [InlineData("tags: a, b", "a|b")]
    [InlineData("tags: [x, y]", "x|y")]
    public async Task TagsRow_DrawsWhatTheIndexReads(string block, string expected)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, block);

        Assert.Equal(expected, string.Join("|", cut.FindAll(".library-properties-chip").Select(static e => e.TextContent.Trim())));
    }

    /// <summary>A value the index skipped (an unquoted <c>#</c> item, or a value with a space) shows as plain text, never as a chip.</summary>
    [Theory]
    [InlineData("tags:\n- #a")]
    [InlineData("tags: [\"b c\"]")]
    public async Task TagsRow_SkippedValue_IsPlainText(string block)
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, block);

        Assert.Empty(cut.FindAll(".library-properties-chip"));
        string raw = PersonaFrontmatter.Parse($"---\n{block}\n---\n").Fields[0].Value;
        Assert.Equal(raw, cut.Find(".library-properties-value").TextContent.Trim());
    }

    /// <summary>Another key written as a list still draws chips: only the <c>tags</c> row follows the index.</summary>
    [Fact]
    public async Task OtherListKey_StillDrawsChips()
    {
        await using MudBunitContext ctx = new();

        IRenderedComponent<LibraryProperties> cut = Render(ctx, "aliases: [one, two]");

        Assert.Equal(2, cut.FindAll(".library-properties-chip").Count);
    }
}
