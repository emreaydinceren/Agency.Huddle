namespace Agency.Huddle.App.Uploads;

/// <summary>Where one attachment chip is in its life (ChatUploads spec 6.4).</summary>
internal enum UploadState
{
    /// <summary>The bytes are still being saved.</summary>
    Uploading,

    /// <summary>The file is saved and will travel with the next Message.</summary>
    Done,

    /// <summary>The file was refused or could not be saved; <see cref="PendingUpload.Error"/> says why.</summary>
    Failed,
}
