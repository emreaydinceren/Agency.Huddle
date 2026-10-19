using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Agency.Huddle.App.Circuits;
using Agency.Huddle.Tests.Ui;

namespace Agency.Huddle.Tests.Circuits;

/// <summary>Pins <see cref="ServerNavigator"/>: it navigates while the client is connected and holds the call back, reporting that it did, once the client is gone.</summary>
public sealed class ServerNavigatorTests
{
    /// <summary>While the client is connected the navigation is issued and reported as issued.</summary>
    [Fact]
    public async Task NavigateTo_WhileConnected_Navigates()
    {
        await using BunitContext ctx = new();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        ServerNavigator navigator = new(navigation, new FakeCircuitConnection(), NullLogger<ServerNavigator>.Instance);

        bool issued = navigator.NavigateTo("/rooms/a");

        Assert.True(issued);
        Assert.Equal("http://localhost/rooms/a", navigation.Uri);
    }

    /// <summary>Once the client is gone the call is not made, the address stays where it was, and the caller is told.</summary>
    [Fact]
    public async Task NavigateTo_WhileDisconnected_DoesNotNavigate()
    {
        await using BunitContext ctx = new();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        string addressBefore = navigation.Uri;
        ServerNavigator navigator = new(navigation, new FakeCircuitConnection { IsConnected = false }, NullLogger<ServerNavigator>.Instance);

        bool issued = navigator.NavigateTo("/rooms/a");

        Assert.False(issued);
        Assert.Equal(addressBefore, navigation.Uri);
    }

    /// <summary>A circuit the client comes back to navigates again: the guard follows the connection, it does not latch.</summary>
    [Fact]
    public async Task NavigateTo_AfterTheClientReturns_NavigatesAgain()
    {
        await using BunitContext ctx = new();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        FakeCircuitConnection connection = new() { IsConnected = false };
        ServerNavigator navigator = new(navigation, connection, NullLogger<ServerNavigator>.Instance);

        bool whileGone = navigator.NavigateTo("/rooms/a");
        connection.IsConnected = true;
        bool afterReturn = navigator.NavigateTo("/rooms/b");

        Assert.False(whileGone);
        Assert.True(afterReturn);
        Assert.Equal("http://localhost/rooms/b", navigation.Uri);
    }

    /// <summary>With the real tracker behind it, the framework's connection-down callback is what stops the navigation, and its connection-up callback is what restores it.</summary>
    [Fact]
    public async Task NavigateTo_FollowsTheTrackersConnectionCallbacks()
    {
        CancellationToken ct = Xunit.TestContext.Current.CancellationToken;
        await using BunitContext ctx = new();
        NavigationManager navigation = ctx.Services.GetRequiredService<NavigationManager>();
        CircuitConnectionTracker tracker = new();
        ServerNavigator navigator = new(navigation, tracker, NullLogger<ServerNavigator>.Instance);

        await tracker.OnConnectionDownAsync(TestCircuit.Any(), ct);
        bool whileDown = navigator.NavigateTo("/rooms/a");
        await tracker.OnConnectionUpAsync(TestCircuit.Any(), ct);
        bool afterUp = navigator.NavigateTo("/rooms/b");

        Assert.False(whileDown);
        Assert.True(afterUp);
        Assert.Equal("http://localhost/rooms/b", navigation.Uri);
    }
}
