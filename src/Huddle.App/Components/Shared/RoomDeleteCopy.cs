namespace Agency.Huddle.App.Components.Shared;

/// <summary>The words of a Room's delete confirmation (ChatUploads spec 6.9 and 7).</summary>
internal static class RoomDeleteCopy
{
    /// <summary>The confirmation prompt: the plain question, plus the Recycle Bin sentence when the Room has uploads.</summary>
    /// <param name="roomName">The Room's display name.</param>
    /// <param name="uploadCount">The number of uploaded files the delete would recycle.</param>
    /// <returns>The prompt text.</returns>
    public static string Prompt(string roomName, int uploadCount) =>
        uploadCount switch
        {
            <= 0 => $"Delete \"{roomName}\"?",
            1 => $"Delete \"{roomName}\"? Its uploaded file goes to the Recycle Bin.",
            _ => $"Delete \"{roomName}\"? Its {uploadCount} uploaded files go to the Recycle Bin.",
        };
}
