namespace Agency.Huddle.App.Library;

/// <summary>Bound from <c>Team:Library:Uploads</c>, per spec 6.1 and 6.6: where chat uploads are saved in the Library.</summary>
public sealed class LibraryUploadsOptions
{
    /// <summary>The Uploads root's folder, relative to <c>DataDir</c>.</summary>
    public string Dir { get; set; } = "Uploads";
}
