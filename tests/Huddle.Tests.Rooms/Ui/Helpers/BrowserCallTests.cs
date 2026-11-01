using Agency.Huddle.App.Components.Shared;
using Microsoft.JSInterop;

namespace Agency.Huddle.Tests.Ui.Helpers;

/// <summary>
/// Pins <see cref="BrowserCall"/>: it absorbs <see cref="JSDisconnectedException"/>, the dead-circuit case,
/// and nothing else, so a real JavaScript failure still reaches the caller.
/// </summary>
public sealed class BrowserCallTests
{
    /// <summary>A call that completes reports success.</summary>
    [Fact]
    public async Task TryAsync_CallSucceeds_ReturnsTrue()
    {
        bool ran = false;

        bool ok = await BrowserCall.TryAsync(() =>
        {
            ran = true;
            return ValueTask.CompletedTask;
        });

        Assert.True(ok);
        Assert.True(ran);
    }

    /// <summary>A circuit that is already gone is swallowed and reported as not run.</summary>
    [Fact]
    public async Task TryAsync_CircuitDisconnected_ReturnsFalse()
    {
        bool ok = await BrowserCall.TryAsync(
            static () => ValueTask.FromException(new JSDisconnectedException("gone")));

        Assert.False(ok);
    }

    /// <summary>A JavaScript error is not a dead circuit, so it propagates.</summary>
    [Fact]
    public async Task TryAsync_JsError_Propagates()
    {
        await Assert.ThrowsAsync<JSException>(async () => await BrowserCall.TryAsync(
            static () => ValueTask.FromException(new JSException("boom"))));
    }

    /// <summary>The generic overload hands back the value on success.</summary>
    [Fact]
    public async Task TryAsyncOfT_CallSucceeds_ReturnsValue()
    {
        (bool ok, int value) = await BrowserCall.TryAsync(static () => ValueTask.FromResult(7));

        Assert.True(ok);
        Assert.Equal(7, value);
    }

    /// <summary>The generic overload reports a dead circuit as not run, with no value.</summary>
    [Fact]
    public async Task TryAsyncOfT_CircuitDisconnected_ReturnsFalse()
    {
        (bool ok, string? value) = await BrowserCall.TryAsync(
            static () => ValueTask.FromException<string>(new JSDisconnectedException("gone")));

        Assert.False(ok);
        Assert.Null(value);
    }

    /// <summary>The generic overload also lets a JavaScript error propagate.</summary>
    [Fact]
    public async Task TryAsyncOfT_JsError_Propagates()
    {
        await Assert.ThrowsAsync<JSException>(async () => await BrowserCall.TryAsync(
            static () => ValueTask.FromException<int>(new JSException("boom"))));
    }
}
