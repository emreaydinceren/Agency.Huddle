using Agency.Huddle.App.Data;
using Agency.Huddle.App.Library;
using Agency.Huddle.App.Uploads;

namespace Agency.Huddle.Tests.Uploads;

/// <summary>An <see cref="IRoomUploadFolders"/> that reports a fixed file count and records the Rooms it was asked to recycle.</summary>
internal sealed class FakeRoomUploadFolders : IRoomUploadFolders
{
    /// <summary>The count <see cref="CountFiles"/> returns for every Room.</summary>
    public int Count { get; set; }

    /// <summary>The ids of the Rooms passed to <see cref="RecycleAsync"/>, in call order.</summary>
    public List<string> Recycled { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<LibraryPath> For(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return [];
    }

    /// <inheritdoc />
    public int CountFiles(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);
        return this.Count;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> RecycleAsync(Room room, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        this.Recycled.Add(room.Id);
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}
