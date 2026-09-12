using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Exercises <see cref="PersonaModelStore"/> against a real SQLite file under
/// <see cref="TempDataDir"/>, including the regression that justifies putting the model in a new
/// table rather than a column on an existing one (see
/// <see cref="Constructor_AddsTheTableToAnExistingDatabase"/>).
/// </summary>
public sealed class PersonaModelStoreTests
{
    [Fact]
    public void Get_UnknownPersona_ReturnsNull()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());

        var model = store.Get("nova");

        Assert.Null(model);
    }

    [Fact]
    public void Set_ThenGet_ReturnsTheModel()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());

        store.Set("nova", "claude-opus-4");
        var model = store.Get("nova");

        Assert.Equal("claude-opus-4", model);
    }

    [Fact]
    public void Set_Twice_ReplacesTheModel()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());

        store.Set("nova", "claude-opus-4");
        store.Set("nova", "claude-sonnet-5");
        var model = store.Get("nova");

        Assert.Equal("claude-sonnet-5", model);
    }

    [Fact]
    public void Set_Null_ClearsTheModel()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());
        store.Set("nova", "claude-opus-4");

        store.Set("nova", null);
        var model = store.Get("nova");

        Assert.Null(model);
    }

    [Fact]
    public void Set_Blank_ClearsTheModel()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());
        store.Set("nova", "claude-opus-4");

        store.Set("nova", "   ");
        var model = store.Get("nova");

        Assert.Null(model);
    }

    [Fact]
    public void Remove_DeletesTheStoredModel()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());
        store.Set("nova", "claude-opus-4");

        store.Remove("nova");
        var model = store.Get("nova");

        Assert.Null(model);
    }

    [Fact]
    public void Get_MatchesThePersonaNameCaseInsensitively()
    {
        using var dir = new TempDataDir();
        var store = new PersonaModelStore(dir.Options());
        store.Set("Nova", "claude-opus-4");

        var model = store.Get("nova");

        Assert.Equal("claude-opus-4", model);
    }

    /// <summary>
    /// The regression that justifies a new table instead of a column on <c>users</c>: a database
    /// created before this change (by <see cref="SqliteTeamDirectory"/> alone) must still gain the
    /// <c>persona_models</c> table when <see cref="PersonaModelStore"/> is constructed against it,
    /// with no need to delete the existing <c>App_Data</c>.
    /// </summary>
    [Fact]
    public async Task Constructor_AddsTheTableToAnExistingDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        using var dir = new TempDataDir();
        var directory = new SqliteTeamDirectory(dir.Options());
        await directory.InitializeAsync("You", ct);

        var store = new PersonaModelStore(dir.Options());
        store.Set("nova", "claude-opus-4");
        var model = store.Get("nova");

        Assert.Equal("claude-opus-4", model);

        var human = await directory.GetHumanAsync(ct);
        Assert.Equal("You", human.Name);
    }
}