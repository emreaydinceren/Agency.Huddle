namespace Agency.Huddle.App.Data;

public static class KnownIds
{
    /// <summary>The id of the Human.</summary>
    public const string Human = "human";

    /// <summary>
    /// The sender of a Message Huddle wrote itself (ADR-0065): neither the Human nor an Agent. It has no <c>User</c> row and is never
    /// a Room Member, and only <c>ChatService.PostAsHuddleAsync</c> may post under it.
    /// </summary>
    public const string Huddle = "huddle";

    /// <summary>The name a Huddle Message carries as its sender.</summary>
    public const string HuddleName = "Huddle";
}