namespace Agency.Huddle.App.Services;

public sealed class ChatException : Exception
{
    public ChatException(string code, string message)
        : base(message)
    {
        this.Code = code;
    }

    public string Code { get; }
}