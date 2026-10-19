namespace Agency.Huddle.Seeder.Model;

/// <summary>A Sprint: one line of <c>sprints.md</c>.</summary>
/// <param name="Number">The number; the id is <c>S{Number}</c>.</param>
/// <param name="Name">The display name.</param>
internal sealed record SeedSprint(int Number, string Name)
{
    /// <summary>The Sprint id as a Task holds it, such as <c>S2</c>.</summary>
    internal string Id => $"S{this.Number}";
}
