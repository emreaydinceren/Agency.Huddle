using Agency.Huddle.App.Library;

namespace Agency.Huddle.App.Uploads;

/// <summary>One attachment chip above the composer.</summary>
/// <param name="Id">Names the chip when removing it.</param>
/// <param name="DisplayName">The browser's file name until the save finishes, then the saved name.</param>
/// <param name="File">The saved file; <see langword="null"/> until <see cref="UploadState.Done"/>.</param>
/// <param name="Size">The bytes saved; 0 until <see cref="UploadState.Done"/>.</param>
/// <param name="Kind">What the first bytes say the file is; <see langword="null"/> until <see cref="UploadState.Done"/>.</param>
/// <param name="State">Where the chip is in its life.</param>
/// <param name="Error">The refusal text of a <see cref="UploadState.Failed"/> chip, otherwise <see langword="null"/>.</param>
internal sealed record PendingUpload(Guid Id, string DisplayName, LibraryPath? File, long Size, UploadKind? Kind, UploadState State, string? Error);
