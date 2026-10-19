namespace Agency.Huddle.Seeder.Model;

/// <summary>A file the Human attached in a chat, saved where the app's upload flow would save it.</summary>
/// <param name="RoomKey">The key of the Room the file was attached in.</param>
/// <param name="FileName">The file name; a Message cites it with the <c>{{upload:FileName}}</c> token.</param>
/// <param name="Content">The bytes.</param>
internal sealed record SeedUpload(string RoomKey, string FileName, byte[] Content);
