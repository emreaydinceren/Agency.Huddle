namespace Agency.Huddle.App.Helpers;

/// <summary>Maps each <see cref="HelperId"/> to its Helper; refuses a duplicate id at startup.</summary>
internal sealed class HelperRegistry
{
    private readonly List<IHelperAgent> helpers;
    private readonly Dictionary<string, IHelperAgent> byId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers every Helper in order.</summary>
    /// <param name="helpers">The registered Helpers.</param>
    public HelperRegistry(IEnumerable<IHelperAgent> helpers)
    {
        ArgumentNullException.ThrowIfNull(helpers);
        this.helpers = [.. helpers];
        foreach (IHelperAgent helper in this.helpers)
        {
            if (!this.byId.TryAdd(helper.Id.Value, helper))
            {
                throw new InvalidOperationException($"Two Helpers are registered with the id '{helper.Id.Value}'.");
            }
        }
    }

    /// <summary>Every Helper, in registration order.</summary>
    public IReadOnlyList<IHelperAgent> All => this.helpers;

    /// <summary>The Helper with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The id.</param>
    public IHelperAgent? Find(HelperId id) => this.byId.GetValueOrDefault(id.Value);
}
