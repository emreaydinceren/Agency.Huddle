using Agency.Huddle.Acp.Abstractions;

namespace Agency.Huddle.App.Library;

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
                PlannedDocument image = await PlanImageAsync(input, delivery, imageCount, imageBytes, readImage, ct).ConfigureAwait(false);
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
            result = await readImage(input.Path, ct).ConfigureAwait(false);
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
