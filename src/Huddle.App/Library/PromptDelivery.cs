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
