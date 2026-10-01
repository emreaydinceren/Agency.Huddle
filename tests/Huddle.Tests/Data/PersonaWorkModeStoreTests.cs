using Agency.Huddle.App.Data;

namespace Agency.Huddle.Tests.Data;

/// <summary>
/// Exercises <see cref="PersonaWorkModeStore"/> against a real SQLite file under
/// <see cref="TempDataDir"/>, including the regression that justifies a new table rather than a
/// column: a database that predates the table must still gain it.
/// </summary>
public sealed class PersonaWorkModeStoreTests
{
    /// <summary>A Persona with no stored Work Mode reads as null, meaning the Adapter's default.</summary>
    [Fact]
    public void Get_UnknownPersona_ReturnsNull()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());

        string? workMode = store.Get("nova");

        Assert.Null(workMode);
    }

    /// <summary>A stored Work Mode reads back unchanged.</summary>
    [Fact]
    public void Set_ThenGet_ReturnsTheWorkMode()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());

        store.Set("nova", "plan");
        string? workMode = store.Get("nova");

        Assert.Equal("plan", workMode);
    }

    /// <summary>Setting twice replaces the first value.</summary>
    [Fact]
    public void Set_Twice_ReplacesTheWorkMode()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());

        store.Set("nova", "plan");
        store.Set("nova", "acceptEdits");
        string? workMode = store.Get("nova");

        Assert.Equal("acceptEdits", workMode);
    }

    /// <summary>A null value deletes the row instead of storing it.</summary>
    [Fact]
    public void Set_Null_ClearsTheWorkMode()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());
        store.Set("nova", "plan");

        store.Set("nova", null);
        string? workMode = store.Get("nova");

        Assert.Null(workMode);
    }

    /// <summary>A blank value deletes the row: an empty string must never reach the database.</summary>
    [Fact]
    public void Set_Blank_ClearsTheWorkMode()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());
        store.Set("nova", "plan");

        store.Set("nova", "   ");
        string? workMode = store.Get("nova");

        Assert.Null(workMode);
    }

    /// <summary>Remove deletes the stored value.</summary>
    [Fact]
    public void Remove_DeletesTheStoredWorkMode()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());
        store.Set("nova", "plan");

        store.Remove("nova");
        string? workMode = store.Get("nova");

        Assert.Null(workMode);
    }

    /// <summary>The Persona name is matched case-insensitively, as for Model and Effort.</summary>
    [Fact]
    public void Get_MatchesThePersonaNameCaseInsensitively()
    {
        using TempDataDir dir = new();
        PersonaWorkModeStore store = new(dir.Options());
        store.Set("Nova", "plan");

        string? workMode = store.Get("nova");

        Assert.Equal("plan", workMode);
    }

    /// <summary>
    /// A database that already has the Model and Effort tables must still gain the Work Mode table
    /// when the store is constructed against it, with no need to delete <c>App_Data</c>. A column on
    /// an existing table would silently not appear, because the schema is created with
    /// <c>CREATE TABLE IF NOT EXISTS</c>.
    /// </summary>
    [Fact]
    public void Constructor_AddsTheTableToADatabaseThatAlreadyHasModelsAndEfforts()
    {
        using TempDataDir dir = new();
        PersonaModelStore models = new(dir.Options());
        PersonaEffortStore efforts = new(dir.Options());
        models.Set("nova", "claude-opus-4");
        efforts.Set("nova", "high");

        PersonaWorkModeStore workModes = new(dir.Options());
        workModes.Set("nova", "plan");

        Assert.Equal("claude-opus-4", models.Get("nova"));
        Assert.Equal("high", efforts.Get("nova"));
        Assert.Equal("plan", workModes.Get("nova"));
    }
}
