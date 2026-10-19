namespace Agency.Huddle.Contracts;

public static class ErrorCodes
{
    public const string ExpectedHello = "expectedHello";
    public const string InvalidName = "invalidName";
    public const string NameReserved = "nameReserved";
    public const string BadMessage = "badMessage";
    public const string UnknownRoom = "unknownRoom";
    public const string NotMember = "notMember";
    public const string NotSupported = "notSupported";
    public const string VersionMismatch = "versionMismatch";

    /// <summary>
    /// The Room has spent its Budget of agent-authored Messages and will take no more until a Human
    /// speaks there. Terminal for the caller: retrying spends the Turn the refusal exists to save,
    /// so the accompanying message says so in words a model will read as final.
    /// </summary>
    public const string BudgetExhausted = "budgetExhausted";
}