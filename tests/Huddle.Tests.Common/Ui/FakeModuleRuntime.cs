using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace Agency.Huddle.Tests.Ui;

/// <summary>
/// A runtime for <see cref="Agency.Huddle.App.Components.Shared.JsModule"/> tests: it answers <c>import</c> with a new
/// <see cref="FakeModuleReference"/> each time, records the module paths, and can fail the next import.
/// </summary>
internal sealed class FakeModuleRuntime : IJSRuntime
{
    /// <summary>Gets the module paths imported, oldest first.</summary>
    public List<string> Imports { get; } = [];

    /// <summary>Gets or sets an exception the next import throws instead of resolving.</summary>
    public Exception? FailNext { get; set; }

    /// <summary>Gets or sets what every call on an imported module throws; with none set a call throws <see cref="NotSupportedException"/>.</summary>
    public Exception? CallFailure { get; set; }

    /// <summary>
    /// Gets a gate that holds every import pending until <see cref="ImportGate.Open"/>, like a fetch that stalls;
    /// <see langword="null"/> resolves imports at once. A separate object, because <c>BL0016</c> reads any call on an
    /// <see cref="IJSRuntime"/> as an interop call that needs a try/catch.
    /// </summary>
    public ImportGate? Gate { get; init; }

    /// <inheritdoc />
#pragma warning disable BL0016 // The fake runtime's own overload forwarding to its sibling, not a call into a browser.
    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, object?[]? args) =>
        this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);
#pragma warning restore BL0016

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Assert.Equal("import", identifier);
        this.Imports.Add(Assert.IsType<string>(Assert.Single(args ?? [])));
        if (this.FailNext is { } failure)
        {
            this.FailNext = null;
            return ValueTask.FromException<TValue>(failure);
        }

        FakeModuleReference reference = new(this);
        if (this.Gate is { } gate)
        {
#pragma warning disable BL0016 // Inside the fake runtime itself: this awaits the test's gate, not a call into a browser.
            return WhenOpenAsync(gate, reference);
#pragma warning restore BL0016
        }

        return ValueTask.FromResult((TValue)(object)reference);

        static async ValueTask<TValue> WhenOpenAsync(ImportGate gate, FakeModuleReference reference)
        {
            await gate.Opened;
            return (TValue)(object)reference;
        }
    }

    /// <summary>Holds imports pending until the test opens it.</summary>
    internal sealed class ImportGate
    {
        private readonly TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets a task that completes when the gate opens.</summary>
        internal Task Opened => this.opened.Task;

        /// <summary>Lets every held and later import resolve.</summary>
        public void Open() => this.opened.TrySetResult();
    }

    /// <summary>A module reference whose every call throws the runtime's <see cref="CallFailure"/>.</summary>
    /// <param name="runtime">The runtime that imported it.</param>
    internal sealed class FakeModuleReference(FakeModuleRuntime runtime) : IJSObjectReference
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <inheritdoc />
#pragma warning disable BL0016 // The fake reference's own overload forwarding to its sibling, not a call into a browser.
        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, object?[]? args) =>
            this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);
#pragma warning restore BL0016

        /// <inheritdoc />
        public ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            ValueTask.FromException<TValue>(runtime.CallFailure ?? new NotSupportedException("These tests only drive failing module calls."));
    }
}
