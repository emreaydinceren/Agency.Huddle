using Agency.Huddle.App.References;
using Agency.Huddle.App.Search;
using Agency.Huddle.Tests.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agency.Huddle.Tests.References;

/// <summary>
/// Pins <see cref="ReferenceSearch"/> (spec §5.2, D-5): the picker owns its own orchestrator, so a search in one
/// never cancels a search in the other, and both complete.
/// </summary>
public sealed class ReferenceSearchTests
{
    /// <summary>A picker search started after the Top Bar search does not cancel the Top Bar search.</summary>
    [Fact]
    public async Task PickerSearch_DoesNotCancelTheBoxSearch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider rooms = new(SearchFilter.Rooms, false);
        using SearchOrchestrator box = new([rooms], NullLogger<SearchOrchestrator>.Instance);
        using ReferenceSearch picker = new([rooms], NullLoggerFactory.Instance);

        Task boxSearch = box.SearchAsync(new SearchParse(SearchFilter.All, "nova", true), null);
        await rooms.WaitForCallsAsync(1, ct);
        Task pickerSearch = picker.SearchAsync(new SearchParse(SearchFilter.All, "nova", true));
        IReadOnlyList<GatedCall> afterPicker = await rooms.WaitForCallsAsync(2, ct);

        Assert.False(afterPicker[0].Token.IsCancellationRequested);
        foreach (GatedCall active in await rooms.WaitForCallsAsync(2, ct))
        {
            active.Complete();
        }

        foreach (GatedCall call in await rooms.WaitForCallsAsync(4, ct))
        {
            call.Complete();
        }

        await Task.WhenAll(boxSearch, pickerSearch).WaitAsync(ct);
        Assert.Equal(2, afterPicker.Count);
    }

    /// <summary>A Top Bar search started after the picker search does not cancel the picker search.</summary>
    [Fact]
    public async Task BoxSearch_DoesNotCancelThePickerSearch()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        GatedSearchProvider rooms = new(SearchFilter.Rooms, false);
        using SearchOrchestrator box = new([rooms], NullLogger<SearchOrchestrator>.Instance);
        using ReferenceSearch picker = new([rooms], NullLoggerFactory.Instance);

        Task pickerSearch = picker.SearchAsync(new SearchParse(SearchFilter.All, "nova", true));
        await rooms.WaitForCallsAsync(1, ct);
        Task boxSearch = box.SearchAsync(new SearchParse(SearchFilter.All, "nova", true), null);
        IReadOnlyList<GatedCall> afterBox = await rooms.WaitForCallsAsync(2, ct);

        Assert.False(afterBox[0].Token.IsCancellationRequested);
        foreach (GatedCall active in await rooms.WaitForCallsAsync(2, ct))
        {
            active.Complete();
        }

        foreach (GatedCall call in await rooms.WaitForCallsAsync(4, ct))
        {
            call.Complete();
        }

        await Task.WhenAll(pickerSearch, boxSearch).WaitAsync(ct);
        Assert.Equal(2, afterBox.Count);
    }

    /// <summary>A search made by the factory belongs to its caller: disposing the container that built the factory leaves it usable, so a circuit never holds one per Composer.</summary>
    [Fact]
    public void Factory_Create_IsNotTrackedByTheContainer()
    {
        ServiceCollection services = new();
        services.AddSingleton<ISearchProvider>(new GatedSearchProvider(SearchFilter.Rooms, false));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<ReferenceSearchFactory>();
        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        ReferenceSearch search = scope.ServiceProvider.GetRequiredService<ReferenceSearchFactory>().Create();

        provider.Dispose();

        // A disposed search throws ObjectDisposedException from SearchAsync; one the container still owned would be disposed by now.
        Task running = search.SearchAsync(new SearchParse(SearchFilter.All, "x", true));
        search.Dispose();
        Assert.NotNull(running);
    }
}
