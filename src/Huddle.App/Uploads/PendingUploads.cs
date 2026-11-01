using Agency.Huddle.App.Library;
using Agency.Huddle.App.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Agency.Huddle.App.Uploads;

/// <summary>
/// The attachment chips of each Room for one circuit (ChatUploads spec 6.4). Scoped rather than component
/// state, so the chips survive a Tab switch that unmounts the composer. Nothing is deleted on dispose: the
/// Human may be reloading, and the files of unsent chips stay in the Room's upload folder.
/// </summary>
/// <param name="uploads">Saves each file.</param>
/// <param name="options">Supplies <see cref="LibraryOptions.MaxUploadsPerMessage"/>.</param>
/// <param name="logger">Reports a <see cref="Changed"/> subscriber that threw; defaults to a logger that discards.</param>
internal sealed class PendingUploads(ChatUploadService uploads, IOptions<TeamOptions> options, ILogger<PendingUploads>? logger = null)
{
    private readonly ILogger<PendingUploads> logger = logger ?? NullLogger<PendingUploads>.Instance;
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<Entry>> byRoom = new(StringComparer.Ordinal);

    /// <summary>Raised after a chip is added, changes state or is removed.</summary>
    public event Action? Changed;

    /// <summary>A snapshot of the chips of <paramref name="roomId"/>, in the order they were added.</summary>
    /// <param name="roomId">The Room to list.</param>
    /// <returns>The chips; empty when the Room has none.</returns>
    public IReadOnlyList<PendingUpload> For(string roomId)
    {
        lock (this.gate)
        {
            return this.byRoom.TryGetValue(roomId, out List<Entry>? list) ? [.. list.Select(static e => e.Chip)] : [];
        }
    }

    /// <summary>
    /// Reserves a chip in the <see cref="UploadState.Uploading"/> state, then saves the file. The count cap is
    /// enforced on the reserved chips, so a chip still uploading counts. Past the cap nothing is reserved and the
    /// returned chip is <see cref="UploadState.Failed"/> with the refusal text.
    /// </summary>
    /// <param name="roomId">The Room the file is attached in.</param>
    /// <param name="browserName">The name the browser gave the file.</param>
    /// <param name="openStream">Opens the bytes; called once the chip is reserved, with the chip's cancellation token.</param>
    /// <param name="ct">Cancels the upload and rethrows; removing the chip cancels it without throwing.</param>
    /// <returns>The chip as it ended: <see cref="UploadState.Done"/>, or <see cref="UploadState.Failed"/>.</returns>
    public async Task<PendingUpload> AddAsync(string roomId, string browserName, Func<CancellationToken, Stream> openStream, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(openStream);

        int cap = options.Value.Library.MaxUploadsPerMessage;
        Entry entry = new(new PendingUpload(Guid.NewGuid(), browserName, null, 0, null, UploadState.Uploading, null), ct);
        lock (this.gate)
        {
            if (!this.byRoom.TryGetValue(roomId, out List<Entry>? list))
            {
                list = [];
                this.byRoom[roomId] = list;
            }

            if (list.Count(static e => e.Chip.State != UploadState.Failed) >= cap)
            {
                entry.Dispose();
                return entry.Chip with { State = UploadState.Failed, Error = cap == 1 ? "A Message can carry up to 1 file." : string.Create(CultureInfo.InvariantCulture, $"A Message can carry up to {cap} files.") };
            }

            list.Add(entry);
        }

        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        try
        {
            Stream stream = openStream(entry.Cancellation.Token);
            await using var streamScope = stream.ConfigureAwait(false);
            LibraryResult<LibraryUpload> result = await uploads.SaveAsync(roomId, browserName, stream, entry.Cancellation.Token).ConfigureAwait(false);
            return await this.Settle(roomId, entry, result).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return entry.Chip with { State = UploadState.Failed, Error = "Cancelled." };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Opening the browser stream can throw (the file passed the browser's own size cap check) and the save can
            // fail on disk; either way the chip must end Failed rather than stay Uploading and block Send forever.
            return await this.Settle(roomId, entry, LibraryResult<LibraryUpload>.Fail(string.Create(CultureInfo.InvariantCulture, $"Couldn't save {browserName}."))).ConfigureAwait(false);
        }
        finally
        {
            entry.Dispose();
        }
    }

    /// <summary>
    /// Drops a chip. A saved file is deleted (a plain delete: it was never sent, so it is not yet the Human's
    /// document); a chip still uploading has its upload cancelled.
    /// </summary>
    /// <param name="roomId">The Room the chip belongs to.</param>
    /// <param name="id">The chip to drop.</param>
    /// <param name="ct">Cancels the delete.</param>
    /// <returns>A task that completes once the chip is gone and its file deleted.</returns>
    public async Task RemoveAsync(string roomId, Guid id, CancellationToken ct)
    {
        Entry? removed = null;
        lock (this.gate)
        {
            if (this.byRoom.TryGetValue(roomId, out List<Entry>? list))
            {
                int index = list.FindIndex(e => e.Chip.Id == id);
                if (index >= 0)
                {
                    removed = list[index];
                    list.RemoveAt(index);
                }
            }
        }

        if (removed is null)
        {
            return;
        }

        removed.Cancel();
        if (removed.Chip.File is { } file)
        {
            await uploads.DeleteUnsentAsync(file, ct).ConfigureAwait(false);
        }

        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
    }

    /// <summary>
    /// Hands over the named completed chips of <paramref name="roomId"/> in order and removes them, and any failed
    /// chip among them, from the Room's list. A chip added since the caller took its snapshot is left alone.
    /// </summary>
    /// <param name="roomId">The Room being sent in.</param>
    /// <param name="ids">The chips that went into the Message.</param>
    /// <returns>The <see cref="UploadState.Done"/> chips among <paramref name="ids"/>; empty when there are none.</returns>
    public IReadOnlyList<PendingUpload> TakeAll(string roomId, IReadOnlyCollection<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        IReadOnlyList<PendingUpload> done;
        lock (this.gate)
        {
            if (!this.byRoom.TryGetValue(roomId, out List<Entry>? list))
            {
                return [];
            }

            List<Entry> taken = [.. list.Where(e => ids.Contains(e.Chip.Id) && e.Chip.State != UploadState.Uploading)];
            if (taken.Count == 0)
            {
                return [];
            }

            _ = list.RemoveAll(taken.Contains);
            if (list.Count == 0)
            {
                _ = this.byRoom.Remove(roomId);
            }

            done = [.. taken.Where(static e => e.Chip.State == UploadState.Done).Select(static e => e.Chip)];
        }

        EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        return done;
    }

    /// <summary>Records how an upload ended on its chip, or cleans up after a chip removed in the meantime.</summary>
    /// <param name="roomId">The Room the chip belongs to.</param>
    /// <param name="entry">The chip that finished.</param>
    /// <param name="result">The writer's result.</param>
    /// <returns>The chip as it now stands.</returns>
    private async Task<PendingUpload> Settle(string roomId, Entry entry, LibraryResult<LibraryUpload> result)
    {
        PendingUpload settled = result.Value is { } upload
            ? entry.Chip with { DisplayName = Path.GetFileName(upload.File.FullPath), File = upload.File, Size = upload.Size, Kind = upload.Kind, State = UploadState.Done }
            : entry.Chip with { State = UploadState.Failed, Error = result.Error };

        bool stillListed;
        lock (this.gate)
        {
            stillListed = this.byRoom.TryGetValue(roomId, out List<Entry>? list) && list.Contains(entry);
            if (stillListed)
            {
                entry.Chip = settled;
            }
        }

        if (stillListed)
        {
            EventRaise.Isolated(this.Changed, ex => this.logger.LogError(ex, "A {Event} handler threw and was skipped.", nameof(this.Changed)));
        }
        else if (settled.File is { } orphan)
        {
            await uploads.DeleteUnsentAsync(orphan, CancellationToken.None).ConfigureAwait(false);
        }

        return settled;
    }

    /// <summary>A chip with the token that cancels its upload.</summary>
    private sealed class Entry : IDisposable
    {
        private readonly CancellationTokenSource cancellation;

        public Entry(PendingUpload chip, CancellationToken ct)
        {
            this.Chip = chip;
            this.cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
        }

        public PendingUpload Chip { get; set; }

        public CancellationTokenSource Cancellation => this.cancellation;

        public void Cancel()
        {
            try
            {
                this.cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The upload already finished and released its token, so there is nothing left to cancel.
            }
        }

        public void Dispose() => this.cancellation.Dispose();
    }
}
