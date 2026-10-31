using Agency.Huddle.App.Teams;

namespace Agency.Huddle.Tests.Teams;

/// <summary>
/// Pins Task 82 (HR-7, decision D6) for <see cref="FileConnectorStatus"/>: a connector is connected when
/// <c>connectors.json</c> in the data folder lists it; a missing, malformed or wrongly shaped file means
/// nothing is connected; and the file is read again on every call, so an edit shows without a restart.
/// </summary>
public sealed class FileConnectorStatusTests
{
    /// <summary>A connector listed in the file is connected and one that is not listed is not.</summary>
    [Fact]
    public void IsConnected_ListedConnector_True()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "connectors.json"), """{"connected": ["email:read", "calendar:read"]}""");
        FileConnectorStatus status = new(dir.Options());

        Assert.True(status.IsConnected("email:read"));
        Assert.True(status.IsConnected("calendar:read"));
        Assert.False(status.IsConnected("files:write"));
    }

    /// <summary>With no file nothing is connected.</summary>
    [Fact]
    public void IsConnected_MissingFile_False()
    {
        using TempDataDir dir = new();
        FileConnectorStatus status = new(dir.Options());

        Assert.False(status.IsConnected("email:read"));
    }

    /// <summary>A file that is not JSON connects nothing and does not throw.</summary>
    [Fact]
    public void IsConnected_MalformedFile_False()
    {
        using TempDataDir dir = new();
        File.WriteAllText(Path.Combine(dir.Path, "connectors.json"), "{ not json");
        FileConnectorStatus status = new(dir.Options());

        Assert.False(status.IsConnected("email:read"));
    }

    /// <summary>JSON of the wrong shape connects nothing.</summary>
    [Fact]
    public void IsConnected_WrongShape_False()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "connectors.json");
        FileConnectorStatus status = new(dir.Options());

        File.WriteAllText(path, """{"connected": "email:read"}""");
        bool asString = status.IsConnected("email:read");
        File.WriteAllText(path, """["email:read"]""");
        bool asArray = status.IsConnected("email:read");
        File.WriteAllText(path, """{"connected": [1, 2]}""");
        bool asNumbers = status.IsConnected("1");

        Assert.False(asString);
        Assert.False(asArray);
        Assert.False(asNumbers);
    }

    /// <summary>The file is read on every call: adding the id, then breaking the file, changes the answer.</summary>
    [Fact]
    public void IsConnected_FileChanges_RereadsEachCall()
    {
        using TempDataDir dir = new();
        string path = Path.Combine(dir.Path, "connectors.json");
        FileConnectorStatus status = new(dir.Options());

        bool before = status.IsConnected("email:read");
        File.WriteAllText(path, """{"connected": ["email:read"]}""");
        bool added = status.IsConnected("email:read");
        File.WriteAllText(path, "not json");
        bool broken = status.IsConnected("email:read");

        Assert.False(before);
        Assert.True(added);
        Assert.False(broken);
    }

    /// <summary>The status reports the path of the file so the UI can tell the user where to add an id.</summary>
    [Fact]
    public void Location_IsConnectorsJsonInDataDir()
    {
        using TempDataDir dir = new();
        FileConnectorStatus status = new(dir.Options());

        Assert.Equal(Path.Combine(dir.Path, "connectors.json"), status.Location);
    }

    /// <summary>The status that reports nothing connected has no file to point at.</summary>
    [Fact]
    public void EmptyConnectorStatus_Location_IsNull()
    {
        EmptyConnectorStatus status = new();

        Assert.Null(status.Location);
    }
}
