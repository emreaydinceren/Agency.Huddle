using Agency.Huddle.Acp.Abstractions;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Prompts;
using System.Globalization;
using System.Text;

namespace Agency.Huddle.App.Acp.Sessions;

/// <summary>
/// Frames the text and Prompt blocks delivered to the model for one Turn: the Room label, any hook
/// blocks, Library documents, Transcript or Catch-up, and the triggering Message. Pure: it reads only
/// its arguments, which is why every prompt it builds can be pinned byte for byte by a golden.
/// </summary>
internal static class TurnPromptFramer
{
    /// <summary>
    /// What precedes a prompt that would otherwise open with a slash. An Adapter reads a command only
    /// at the very start of a prompt, so this makes the text plain. A Prompt template can be edited to
    /// open with another Agent's text, and <c>PromptValidator</c> reports but never refuses, so the
    /// guard is applied to every prompt rather than trusted to the templates (Commands spec, D-9).
    /// </summary>
    internal const string CommandGuardMarker = "Message: ";

    /// <summary>Builds the prompt text delivered to the model for one Turn.</summary>
    /// <param name="item">The Turn's Room, sender, text, any catch-up context, and its <see cref="WorkItemKind"/>.</param>
    /// <param name="prompts">Resolves each <c>turn.*</c> prompt's current text — a configured override, or the <see cref="PromptCatalog"/> default.</param>
    /// <returns>
    /// For a <see cref="WorkItemKind.Command"/>, the bare command: <c>/compact</c>, or
    /// <c>/compact arguments</c>, and nothing else. For a <see cref="WorkItemKind.Greeting"/>, the
    /// <c>turn.greeting</c> prompt alone. Otherwise: with
    /// no catch-up context, the <c>turn.message</c> line alone; with catch-up context, a
    /// <c>turn.catchUpHeader</c> line, one <c>turn.catchUpLine</c> per missed message, a blank line,
    /// then the <c>turn.message</c> line.
    /// </returns>
    internal static string BuildPrompt(WorkItem item, IPromptSource prompts)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(prompts);

        if (item.Kind == WorkItemKind.Command)
        {
            // Exactly the command, one text block, no Room label: the Adapter recognises it only at
            // the start of the prompt, and its input is the rest of this line.
            AdapterCommandCall command = item.Command
                ?? throw new InvalidOperationException("A Command work item carries no command.");
            return command.Arguments.Length == 0 ? $"/{command.Name}" : $"/{command.Name} {command.Arguments}";
        }

        string prompt = TurnPromptFramer.BuildFramedPrompt(item, prompts);
        return prompt.AsSpan().TrimStart().StartsWith('/') ? CommandGuardMarker + prompt : prompt;
    }

    /// <summary>
    /// The Prompt blocks that follow the prompt's text on the wire (design §6.6): the block of each of
    /// the Turn's Library documents, in order. Only an ordinary Message Turn can carry one: a
    /// <see cref="WorkItemKind.Command"/> is the bare command and a <see cref="WorkItemKind.Greeting"/>
    /// has no triggering Message, and neither collects Library documents in the first place.
    /// </summary>
    /// <param name="item">The Turn's work item.</param>
    /// <returns>The blocks, possibly none.</returns>
    internal static IReadOnlyList<AgentPromptBlock> BuildPromptBlocks(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Kind != WorkItemKind.Message || item.LibraryDocuments is not { } report)
        {
            return [];
        }

        return [.. report.Items.Select(document => document.Block).OfType<AgentPromptBlock>()];
    }

    private static string BuildFramedPrompt(WorkItem item, IPromptSource prompts)
    {
        // The Room's id rides along with its name because it is the only way an Agent can learn one.
        // mcp__team__post_message and mcp__team__invite_agent both take a room id, and nothing else
        // in a turn carries it: without this they reach only Rooms the Agent created itself.
        var room = TurnPromptFramer.RoomLabel(item, prompts);

        if (item.Kind == WorkItemKind.Greeting)
        {
            // No triggering Message and no catch-up (Spec §6.14): the trigger is this Prompt, not
            // Transcript text, and a Greeting is by definition the first Turn in an empty Room. FC
            // §6.8: the File Changes block never applies to a Greeting either.
            return prompts.Render("turn.greeting", new Dictionary<string, string> { ["{{roomLabel}}"] = room });
        }

        var builder = new StringBuilder();

        // Spec 6.7: hook blocks (File Changes is one of them) lead the framed prompt, each followed by a blank line.
        if (item.HookBlocks is { } hookBlocks)
        {
            foreach (string hookBlock in hookBlocks)
            {
                builder.Append(hookBlock);
                builder.Append("\n\n");
            }
        }

        TurnPromptFramer.AppendLibraryDocumentsBlock(builder, item.LibraryDocuments, prompts);

        // Spec section 9.3: after the Library documents block and before any catch-up or the Message.
        // Absent means unchanged, so every prompt without a header stays byte-identical.
        if (item.ReferencedItems is { } referencedItems)
        {
            builder.Append(referencedItems);
            builder.Append("\n\n");
        }

        // RS §6.5: the Transcript block replaces the catch-up buffer on this Turn only, and only
        // when it actually holds Messages — an empty Transcript (the read was refused, timed out, or
        // this Room's first Turn ever has nothing before the trigger) falls through to today's
        // buffer-based catch-up untouched, keeping every pre-D24 prompt byte-identical.
        if (item.Transcript is { Messages.Count: > 0 } transcript)
        {
            TurnPromptFramer.AppendTranscriptBlock(builder, room, transcript, prompts);
            builder.Append(TurnPromptFramer.RenderMessage(prompts, room, item.SenderName, item.Text));
            return builder.ToString();
        }

        var ownPostLines = item.OwnPostLines ?? [];
        if (item.MissedMessages.Count == 0 && ownPostLines.Count == 0)
        {
            builder.Append(TurnPromptFramer.RenderMessage(prompts, room, item.SenderName, item.Text));
            return builder.ToString();
        }

        TurnPromptFramer.AppendCatchUpAndMessage(builder, room, item, ownPostLines, prompts);

        return builder.ToString();
    }

    /// <summary>Writes the Catch-up block (missed Messages and own posts) and then the triggering Message.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="room">The Room label.</param>
    /// <param name="item">The item being prompted.</param>
    /// <param name="ownPostLines">This Agent's own earlier posts, rendered as lines.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendCatchUpAndMessage(StringBuilder builder, string room, WorkItem item, IReadOnlyList<string> ownPostLines, IPromptSource prompts)
    {
        // RS §6.7: a block of only own posts still gets turn.catchUpHeader, exactly like a block of
        // only missed Messages - the header introduces the whole Catch-up block, not just one kind
        // of line inside it.
        builder.Append(prompts.Render("turn.catchUpHeader", new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');
        foreach (var missed in item.MissedMessages)
        {
            builder.Append(prompts.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = missed.SenderName, ["{{text}}"] = missed.Text }));
            builder.Append('\n');
        }

        foreach (var line in ownPostLines)
        {
            builder.Append(prompts.Render("turn.ownPostLine", new Dictionary<string, string> { ["{{text}}"] = line }));
            builder.Append('\n');
        }

        builder.Append('\n');
        builder.Append(TurnPromptFramer.RenderMessage(prompts, room, item.SenderName, item.Text));
    }

    /// <summary>
    /// Writes RS §6.5's Transcript block: the fresh-or-resumed header, an omitted-count line when
    /// <see cref="TranscriptCatchUp.Omitted"/> is positive, one <c>turn.catchUpLine</c> per Message
    /// (the Agent's own Messages appear under its own Name, per RS §6.9), then a blank line.
    /// </summary>
    private static void AppendTranscriptBlock(StringBuilder builder, string room, TranscriptCatchUp transcript, IPromptSource prompts)
    {
        var headerKey = transcript.Resumed ? "turn.transcriptResumedHeader" : "turn.transcriptHeader";
        builder.Append(prompts.Render(headerKey, new Dictionary<string, string> { ["{{roomLabel}}"] = room }));
        builder.Append('\n');

        if (transcript.Omitted > 0)
        {
            builder.Append(prompts.Render(
                "turn.transcriptOmitted",
                new Dictionary<string, string> { ["{{count}}"] = transcript.Omitted.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        if (transcript.OmittedOwn > 0)
        {
            builder.Append(prompts.Render(
                "turn.transcriptOmittedOwn",
                new Dictionary<string, string> { ["{{count}}"] = transcript.OmittedOwn.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        foreach (var message in transcript.Messages)
        {
            builder.Append(prompts.Render(
                "turn.catchUpLine",
                new Dictionary<string, string> { ["{{sender}}"] = message.SenderName, ["{{text}}"] = message.Text }));
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>
    /// Spec §6.14: with a non-empty <paramref name="report"/>, writes the Library documents block
    /// directly after File Changes — the header, one line per document (a too-large document's line
    /// replaces the ordinary one), the document's text fenced and truncation-noted when inlined, an
    /// "…and N more documents." line when the cap left some out, then a blank line. Writes nothing for
    /// a <see langword="null"/> or empty report: "absent means unchanged", as for File Changes.
    /// </summary>
    private static void AppendLibraryDocumentsBlock(StringBuilder builder, LibraryDocumentsReport? report, IPromptSource prompts)
    {
        if (report is null || report.IsEmpty)
        {
            return;
        }

        builder.Append(prompts.Render("turn.libraryDocsHeader", new Dictionary<string, string>()));
        builder.Append('\n');

        foreach (var item in report.Items)
        {
            TurnPromptFramer.AppendLibraryDocument(builder, report, item, prompts);
        }

        if (report.NotListed > 0)
        {
            builder.Append(prompts.Render(
                "turn.libraryDocsMore",
                new Dictionary<string, string> { ["{{count}}"] = report.NotListed.ToString(CultureInfo.InvariantCulture) }));
            builder.Append('\n');
        }

        builder.Append('\n');
    }

    /// <summary>Writes the line for a Library document that travels as a block, or an image the model cannot be given, plus its truncation note.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="report">The report the document belongs to, for its inline-size cap.</param>
    /// <param name="item">The document to write.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendNonPathLibraryDocument(StringBuilder builder, LibraryDocumentsReport report, LibraryDocumentItem item, IPromptSource prompts)
    {
        var deliveryKey = item.Delivery == LibraryDocumentDelivery.Block ? "turn.libraryDocIncluded" : "turn.libraryImageUnavailable";
        builder.Append(prompts.Render(
            deliveryKey,
            new Dictionary<string, string>
            {
                ["{{path}}"] = item.FullPath,
                ["{{location}}"] = item.Location,
                ["{{size}}"] = item.Size,
            }));
        builder.Append('\n');

        if (item.Block is AgentTextResourceBlock && item.Truncated)
        {
            builder.Append(prompts.Render(
                "turn.libraryDocTruncated",
                new Dictionary<string, string>
                {
                    ["{{max}}"] = report.MaxInlineBytes.ToString(CultureInfo.InvariantCulture),
                    ["{{size}}"] = item.Size,
                }));
            builder.Append('\n');
        }
    }

    /// <summary>Writes one Library document's line, and its inlined text and truncation note when it has them.</summary>
    /// <param name="builder">The prompt being built.</param>
    /// <param name="report">The report the document belongs to, for its inline-size cap.</param>
    /// <param name="item">The document to write.</param>
    /// <param name="prompts">Where the prompt templates come from.</param>
    private static void AppendLibraryDocument(StringBuilder builder, LibraryDocumentsReport report, LibraryDocumentItem item, IPromptSource prompts)
    {
        // A document that travels with the message, or an image the model cannot be given, is worded
        // by that fact before anything else: an image over the editable limit is "too large to read"
        // as a path, and still arrives as a block (design §6.5).
        if (item.Delivery != LibraryDocumentDelivery.Path)
        {
            TurnPromptFramer.AppendNonPathLibraryDocument(builder, report, item, prompts);
            return;
        }

        if (item.TooLarge)
        {
            var sizeKb = (item.Length + 1023) / 1024;
            builder.Append(prompts.Render(
                "turn.libraryDocTooLarge",
                new Dictionary<string, string>
                {
                    ["{{label}}"] = item.Location,
                    ["{{path}}"] = item.FullPath,
                    ["{{sizeKb}}"] = sizeKb.ToString(CultureInfo.InvariantCulture),
                }));
            builder.Append('\n');
            return;
        }

        builder.Append(prompts.Render(
            "turn.libraryDoc",
            new Dictionary<string, string>
            {
                ["{{path}}"] = item.FullPath,
                ["{{location}}"] = item.Location,
                ["{{size}}"] = item.Size,
            }));
        builder.Append('\n');

        if (item.Text is not null)
        {
            var fence = TurnPromptFramer.ChooseFence(item.Text);
            builder.Append(prompts.Render(
                "turn.libraryDocInline",
                new Dictionary<string, string> { ["{{fence}}"] = fence, ["{{text}}"] = item.Text }));
            builder.Append('\n');

            if (item.Truncated)
            {
                builder.Append(prompts.Render(
                    "turn.libraryDocTruncated",
                    new Dictionary<string, string>
                    {
                        ["{{max}}"] = report.MaxInlineBytes.ToString(CultureInfo.InvariantCulture),
                        ["{{size}}"] = item.Size,
                    }));
                builder.Append('\n');
            }
        }
    }

    /// <summary>
    /// Corrections-B5 item 32: the fence for an inlined document's text is the longest run of
    /// backticks already in that text, plus one, never shorter than three — so the fence can never be
    /// closed early by a backtick run the text itself contains.
    /// </summary>
    /// <param name="text">The document's text about to be fenced.</param>
    /// <returns>A run of backticks safe to wrap <paramref name="text"/> in.</returns>
    private static string ChooseFence(string text)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var c in text)
        {
            if (c == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        return new string('`', Math.Max(3, longestRun + 1));
    }

    /// <summary>Builds the bracketed Room label that opens every line of a prompt.</summary>
    /// <param name="item">The work item whose Room is being labelled.</param>
    /// <param name="prompts">Resolves the <c>turn.roomLabel</c> prompt's current text.</param>
    /// <returns>The label, carrying both the Room's name and its id.</returns>
    private static string RoomLabel(WorkItem item, IPromptSource prompts) =>
        prompts.Render(
            "turn.roomLabel",
            new Dictionary<string, string>
            {
                ["{{roomName}}"] = item.RoomName,
                ["{{roomId}}"] = item.RoomId,
                ["{{teamSuffix}}"] = item.Team is null ? string.Empty : $", Team: {item.Team}",
            });

    /// <summary>Renders one <c>turn.message</c> line: a Room label, its sender, and its text.</summary>
    /// <param name="prompts">Resolves the <c>turn.message</c> prompt's current text.</param>
    /// <param name="roomLabel">The already-rendered Room label to open the line with.</param>
    /// <param name="sender">The message's sender name.</param>
    /// <param name="text">The message's text.</param>
    /// <returns>The rendered <c>turn.message</c> line.</returns>
    private static string RenderMessage(IPromptSource prompts, string roomLabel, string sender, string text) =>
        prompts.Render(
            "turn.message",
            new Dictionary<string, string> { ["{{roomLabel}}"] = roomLabel, ["{{sender}}"] = sender, ["{{text}}"] = text });
}
