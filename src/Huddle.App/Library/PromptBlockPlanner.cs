using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Library;

/// <summary>
/// What an Adapter and its profile allow a Turn's prompt to carry beyond text, built once per Room
/// Session (design §6.4, §8.1). The booleans are already combined with the Adapter Profile's
/// <c>PromptBlocks</c> kill switch, so the planner never sees the profile.
/// </summary>
/// <param name="ReadsFiles">Whether the Adapter has file tools, so a path line is enough for a text file.</param>
/// <param name="Images">Whether an image may go as a block: the Adapter advertised <c>image</c> and the profile allows blocks.</param>
/// <param name="EmbeddedText">Whether a document's text may go as an embedded resource: the Adapter advertised <c>embeddedContext</c>, the profile allows blocks, and it has no file tools.</param>
/// <param name="MaxImagesPerTurn">The most image blocks in one prompt; zero or less sends none.</param>
/// <param name="MaxImageBytesPerTurn">The most raw image bytes in one prompt; zero or less sends none.</param>
internal sealed record PromptDelivery(bool ReadsFiles, bool Images, bool EmbeddedText, int MaxImagesPerTurn, long MaxImageBytesPerTurn);

/// <summary>How one Library document reaches the model.</summary>
public enum LibraryDocumentDelivery
{
    /// <summary>Today's line: the path, its location and size (and, for an Adapter with no file tools, the inline text).</summary>
    Path,

    /// <summary>The document travels as a Prompt block; its line says it is included with the message.</summary>
    Block,

    /// <summary>An image the model cannot be given: its line says so, so the model does not try to read it.</summary>
    Unavailable,
}

/// <summary>Why an image that could have been a block was not sent. Never set for an image the design excludes by rule.</summary>
public enum PromptBlockWithheld
{
    /// <summary>Nothing was withheld.</summary>
    None,

    /// <summary>The Turn already held <see cref="PromptDelivery.MaxImagesPerTurn"/> images.</summary>
    ImageCountCap,

    /// <summary>Adding this image would pass <see cref="PromptDelivery.MaxImageBytesPerTurn"/>.</summary>
    TurnBytesCap,

    /// <summary>The reader refused: not a supported raster image.</summary>
    NotAnImage,

    /// <summary>The reader refused: over the per-image byte cap.</summary>
    TooLarge,

    /// <summary>The reader refused: over the edge cap.</summary>
    TooManyPixels,

    /// <summary>The reader refused: the file could not be read.</summary>
    Unreadable,
}

/// <summary>One document offered to the planner.</summary>
/// <param name="Path">The resolved Library path.</param>
/// <param name="Kind">What the Library says the file is. Only <see cref="LibraryFileKind.Image"/> can be an image block.</param>
/// <param name="FromTrigger">Whether the path was written in the Message that started the Turn (D-2).</param>
/// <param name="InlineText">The document's text, already cut to the inline limit, or <see langword="null"/> when it was not read.</param>
internal sealed record PlanInput(LibraryPath Path, LibraryFileKind Kind, bool FromTrigger, string? InlineText = null);

/// <summary>The planner's decision for one document.</summary>
/// <param name="Delivery">How the document reaches the model.</param>
/// <param name="Block">The Prompt block when <paramref name="Delivery"/> is <see cref="LibraryDocumentDelivery.Block"/>; otherwise <see langword="null"/>.</param>
/// <param name="Withheld">Why an eligible image was not sent, or <see cref="PromptBlockWithheld.None"/>.</param>
internal sealed record PlannedDocument(LibraryDocumentDelivery Delivery, AgentPromptBlock? Block = null, PromptBlockWithheld Withheld = PromptBlockWithheld.None);

/// <summary>
/// Decides, per Library document, whether it travels as a Prompt block, as today's path line, or as
/// "an image you cannot see" (design §6.4). Pure apart from the injected reader: the table is tested
/// without a filesystem. Walks in first-seen order, and a refused or over-cap image does not use up
/// the budget, so a later, smaller one can still fit.
/// </summary>
internal static class PromptBlockPlanner
{
    /// <summary>Plans every document in <paramref name="inputs"/>, in order.</summary>
    /// <param name="inputs">The documents, in first-seen order.</param>
    /// <param name="delivery">What the Adapter and its profile allow.</param>
    /// <param name="readImage">Reads an image's bytes, or refuses; called only for an image the table already allows.</param>
    /// <param name="ct">Cancels the reads.</param>
    /// <returns>One decision per input, in the same order.</returns>
    internal static async Task<IReadOnlyList<PlannedDocument>> PlanAsync(
        IReadOnlyList<PlanInput> inputs,
        PromptDelivery delivery,
        Func<LibraryPath, CancellationToken, Task<LibraryImageResult>> readImage,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(readImage);

        List<PlannedDocument> planned = new(inputs.Count);
        int imageCount = 0;
        long imageBytes = 0;

        foreach (PlanInput input in inputs)
        {
            ct.ThrowIfCancellationRequested();

            if (input.Kind == LibraryFileKind.Image)
            {
                PlannedDocument image = await PlanImageAsync(input, delivery, imageCount, imageBytes, readImage, ct);
                if (image.Block is AgentImageBlock block)
                {
                    imageCount++;
                    imageBytes += block.Data.Length;
                }

                planned.Add(image);
                continue;
            }

            planned.Add(PlanText(input, delivery));
        }

        return planned;
    }

    private static async Task<PlannedDocument> PlanImageAsync(
        PlanInput input,
        PromptDelivery delivery,
        int imageCount,
        long imageBytes,
        Func<LibraryPath, CancellationToken, Task<LibraryImageResult>> readImage,
        CancellationToken ct)
    {
        PlannedDocument fallback = new(delivery.ReadsFiles ? LibraryDocumentDelivery.Path : LibraryDocumentDelivery.Unavailable);
        if (!delivery.Images || !input.FromTrigger || delivery.MaxImagesPerTurn < 1 || delivery.MaxImageBytesPerTurn < 1)
        {
            return fallback;
        }

        if (imageCount >= delivery.MaxImagesPerTurn)
        {
            return fallback with { Withheld = PromptBlockWithheld.ImageCountCap };
        }

        LibraryImageResult result;
        try
        {
            result = await readImage(input.Path, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A read that fails outright costs one image the model did not see, never the Turn.
            return fallback with { Withheld = PromptBlockWithheld.Unreadable };
        }

        if (result is not LibraryImageRead read)
        {
            LibraryImageRefusal reason = (result as LibraryImageRefused)?.Reason ?? LibraryImageRefusal.Unreadable;
            return fallback with { Withheld = WithheldFor(reason) };
        }

        if (imageBytes + read.Bytes.Length > delivery.MaxImageBytesPerTurn)
        {
            return fallback with { Withheld = PromptBlockWithheld.TurnBytesCap };
        }

        return new PlannedDocument(LibraryDocumentDelivery.Block, new AgentImageBlock(read.MimeType, read.Bytes));
    }

    private static PlannedDocument PlanText(PlanInput input, PromptDelivery delivery)
    {
        bool isText = input.Kind is LibraryFileKind.Markdown or LibraryFileKind.Text;
        if (!isText || !delivery.EmbeddedText || delivery.ReadsFiles || !input.FromTrigger || input.InlineText is null)
        {
            return new PlannedDocument(LibraryDocumentDelivery.Path);
        }

        string mimeType = input.Kind == LibraryFileKind.Markdown ? "text/markdown" : "text/plain";
        return new PlannedDocument(
            LibraryDocumentDelivery.Block,
            new AgentTextResourceBlock(new Uri(input.Path.FullPath).AbsoluteUri, mimeType, input.InlineText));
    }

    private static PromptBlockWithheld WithheldFor(LibraryImageRefusal reason) => reason switch
    {
        LibraryImageRefusal.NotAnImage => PromptBlockWithheld.NotAnImage,
        LibraryImageRefusal.TooLarge => PromptBlockWithheld.TooLarge,
        LibraryImageRefusal.TooManyPixels => PromptBlockWithheld.TooManyPixels,
        _ => PromptBlockWithheld.Unreadable,
    };
}
