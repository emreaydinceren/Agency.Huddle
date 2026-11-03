namespace Agency.Huddle.App.Feed;

/// <summary>The number on each Category segment under the Agent and Team filters. The numbers are not meant to add up.</summary>
/// <param name="All">Every item.</param>
/// <param name="NeedsYou">Unresolved Notifications.</param>
/// <param name="Tasks">Items in the Tasks segment.</param>
/// <param name="Documents">Items in the Documents segment.</param>
/// <param name="Rooms">Items in the Rooms segment.</param>
/// <param name="Gates">Items in the Gates segment.</param>
/// <param name="Helpers">Items in the Helpers segment.</param>
/// <param name="Devices">Items in the Devices segment (zero when a caller builds the counts without it).</param>
/// <param name="SecondBrain">Items in the Second Brain segment (zero when a caller builds the counts without it).</param>
internal sealed record FeedCounts(int All, int NeedsYou, int Tasks, int Documents, int Rooms, int Gates, int Helpers, int Devices = 0, int SecondBrain = 0);
