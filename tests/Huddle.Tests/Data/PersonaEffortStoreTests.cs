using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Exercises <see cref="PersonaEffortStore"/> against a real SQLite file under
/// <see cref="TempDataDir"/>, including the regression that justifies putting the effort in a new
/// table rather than a column on an existing one (see
/// <see cref="Constructor_AddsTheTableToADatabaseThatAlreadyHasPersonaModels"/>).
/// </summary>
public sealed class PersonaEffortStoreTests
{
    [Fact]
    public void Get_UnknownPersona_ReturnsNull()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());

        var effort = store.Get("nova");

        Assert.Null(effort);
    }

    [Fact]
    public void Set_ThenGet_ReturnsTheEffort()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());

        store.Set("nova", "high");
        var effort = store.Get("nova");

        Assert.Equal("high", effort);
    }

    [Fact]
    public void Set_Twice_ReplacesTheEffort()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());

        store.Set("nova", "high");
        store.Set("nova", "low");
        var effort = store.Get("nova");

        Assert.Equal("low", effort);
    }

    [Fact]
    public void Set_Null_ClearsTheEffort()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());
        store.Set("nova", "high");

        store.Set("nova", null);
        var effort = store.Get("nova");

        Assert.Null(effort);
    }

    [Fact]
    public void Set_Blank_ClearsTheEffort()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());
        store.Set("nova", "high");

        store.Set("nova", "   ");
        var effort = store.Get("nova");

        Assert.Null(effort);
    }

    [Fact]
    public void Remove_DeletesTheStoredEffort()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());
        store.Set("nova", "high");

        store.Remove("nova");
        var effort = store.Get("nova");

        Assert.Null(effort);
    }

    [Fact]
    public void Get_MatchesThePersonaNameCaseInsensitively()
    {
        using var dir = new TempDataDir();
        var store = new PersonaEffortStore(dir.Options());
        store.Set("Nova", "high");

        var effort = store.Get("nova");

        Assert.Equal("high", effort);
    }

    /// <summary>
    /// The regression that justifies a new table instead of a column on an existing one: a database
    /// that already has a <c>persona_models</c> table (created by <see cref="PersonaModelStore"/>)
    /// must still gain the <c>persona_efforts</c> table when <see cref="PersonaEffortStore"/> is
    /// constructed against it, with no need to delete the existing <c>App_Data</c>. This is exactly
    /// the regression <see cref="PersonaModelStore"/>'s own class doc warns a column-on-an-existing-
    /// table approach would reintroduce, proved here a second time against <c>persona_models</c>
    /// itself.
    /// </summary>
    [Fact]
    public void Constructor_AddsTheTableToADatabaseThatAlreadyHasPersonaModels()
    {
        using var dir = new TempDataDir();
        var models = new PersonaModelStore(dir.Options());
        models.Set("nova", "claude-opus-4");

        var efforts = new PersonaEffortStore(dir.Options());
        efforts.Set("nova", "high");

        Assert.Equal("claude-opus-4", models.Get("nova"));
        Assert.Equal("high", efforts.Get("nova"));
    }
}
