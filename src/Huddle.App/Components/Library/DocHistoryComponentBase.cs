using Agency.Huddle.App.Components.Shared;
using Agency.Huddle.App.DocHistory;
using Agency.Huddle.App.Library;
using Microsoft.AspNetCore.Components;

namespace Agency.Huddle.App.Components.Library;

/// <summary>
/// What the byline, the History and the History panel share (DocHistory spec §6.7, §6.8, §6.10): the entries of
/// one document, read again when the document changes and when <see cref="DocHistoryStore.Changed"/> reports an
/// entry for it, so a new entry appears without a reload.
/// </summary>
public abstract class DocHistoryComponentBase : ComponentBase, IDisposable
{
    private LibraryPath? loadedPath;
    private bool subscribed;

    /// <summary>The store the entries are read from; each component injects it and returns it here.</summary>
    private protected abstract DocHistoryStore HistoryStore { get; }

    /// <summary>The document's entries, oldest first; empty for none.</summary>
    protected IReadOnlyList<DocHistoryEntry> Entries { get; private set; } = [];

    /// <summary>The document whose history is shown, or <see langword="null"/> for none.</summary>
    protected abstract LibraryPath? HistoryPath { get; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        this.HistoryStore.Changed += this.OnStoreChanged;
        this.subscribed = true;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        LibraryPath? path = this.HistoryPath;
        if (LibraryPathComparer.Instance.Equals(this.loadedPath, path))
        {
            return;
        }

        this.loadedPath = path;
        this.Reload();
    }

    /// <summary>Stops listening to the store.</summary>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops listening to the store.</summary>
    /// <param name="disposing">Whether this is a call to <see cref="Dispose()"/> rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && this.subscribed)
        {
            this.HistoryStore.Changed -= this.OnStoreChanged;
            this.subscribed = false;
        }
    }

    /// <summary>Reads the entries again from the store.</summary>
    protected void Reload()
    {
        this.Entries = this.loadedPath is { } path ? this.HistoryStore.Read(path) : [];
        this.OnEntriesLoaded();
    }

    /// <summary>Called after <see cref="Entries"/> was read, so a component can derive what it shows from them.</summary>
    protected virtual void OnEntriesLoaded()
    {
    }

    /// <summary>Redraws when an entry was added, moved or dropped for this document.</summary>
    /// <param name="changed">The document the store changed.</param>
    private void OnStoreChanged(LibraryPath changed)
    {
        if (!LibraryPathComparer.Instance.Equals(this.loadedPath, changed))
        {
            return;
        }

        _ = RendererDispatch.RunAsync(this.InvokeAsync, () =>
        {
            this.Reload();
            this.StateHasChanged();
            return Task.CompletedTask;
        });
    }
}
