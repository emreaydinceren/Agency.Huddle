using System.Globalization;
using System.Text;
using Agency.Huddle.App.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Agency.Huddle.App.Feed;

/// <summary>
/// The Feed's table in <c>team.db</c> (spec 6.1) and its reads (spec 6.7). A singleton, synchronous like
/// <c>CadenceStateStore</c>, on plain <c>Microsoft.Data.Sqlite</c>. The write methods are internal and called
/// only by <see cref="FeedWriter"/>.
/// </summary>
internal sealed class FeedStore
{
    private const string DdlCreate = """
        CREATE TABLE IF NOT EXISTS feed_events (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at    TEXT    NOT NULL,
            source_type   TEXT    NOT NULL,
            target_key    TEXT    NOT NULL,
            actor         TEXT    NOT NULL,
            notify        INTEGER NOT NULL DEFAULT 0,
            seen_at       TEXT,
            dismissed_at  TEXT,
            resolved_at   TEXT,
            payload       TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_feed_events_created_at ON feed_events(created_at);
        CREATE INDEX IF NOT EXISTS ix_feed_events_target     ON feed_events(target_key, source_type);
        CREATE INDEX IF NOT EXISTS ix_feed_events_open       ON feed_events(resolved_at, seen_at) WHERE notify = 1;
        CREATE TABLE IF NOT EXISTS feed_state (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        );
        """;

    private const string Columns = "id, created_at, source_type, target_key, actor, notify, seen_at, dismissed_at, resolved_at, payload";
    private const string TabSeenKey = "tab_seen_up_to";
    private const string TeamExpression = "(CASE WHEN json_valid(payload) THEN json_extract(payload, '$.data.team') END)";

    private readonly string connectionString;
    private readonly Agency.Huddle.App.Teams.ITeamRenames? renames;

    /// <summary>Opens the database and creates the tables when they are missing.</summary>
    /// <param name="options">Supplies the data folder holding <c>team.db</c>.</param>
    /// <param name="renames">The rename journal, so a Team filter also finds rows recorded under an earlier name.</param>
    public FeedStore(IOptions<TeamOptions> options, Agency.Huddle.App.Teams.ITeamRenames? renames = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.renames = renames;
        this.connectionString = SqliteTable.Open(options.Value, DdlCreate);
    }

    /// <summary>One page of the Feed, newest first. Keyset by id, so live inserts never shift a page.</summary>
    /// <param name="query">Which page and which filters.</param>
    /// <returns>The items, newest first.</returns>
    public IReadOnlyList<FeedItem> Page(FeedQuery query)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        StringBuilder sql = new($"SELECT {Columns} FROM feed_events WHERE 1 = 1");
        if (query.BeforeId is { } before)
        {
            _ = sql.Append(" AND id < CAST($before AS INTEGER)");
            _ = command.Parameters.AddWithValue("$before", Text(before));
        }

        AppendActorAndTeam(sql, command, query.Agent, query.Team);
        _ = sql.Append(" AND ").Append(CategoryCondition(query.Category, command));
        if (query.ExcludeDismissed)
        {
            _ = sql.Append(" AND dismissed_at IS NULL");
        }

        _ = sql.Append(" ORDER BY id DESC LIMIT CAST($limit AS INTEGER)");
        _ = command.Parameters.AddWithValue("$limit", Text(query.Limit));
        command.CommandText = sql.ToString();
        return ReadItems(command);
    }

    /// <summary>The Notifications for the panel: notify, not dismissed, newest first.</summary>
    /// <param name="limit">The most to return.</param>
    /// <returns>The Notifications.</returns>
    public IReadOnlyList<FeedItem> Notifications(int limit)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM feed_events WHERE notify = 1 AND dismissed_at IS NULL ORDER BY id DESC LIMIT CAST($limit AS INTEGER);";
        _ = command.Parameters.AddWithValue("$limit", Text(limit));
        return ReadItems(command);
    }

    /// <summary>Notify, unseen, undismissed, unresolved: the number on the Notifications button.</summary>
    /// <returns>The count.</returns>
    public int UnseenCount()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM feed_events WHERE notify = 1 AND seen_at IS NULL AND dismissed_at IS NULL AND resolved_at IS NULL;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Unresolved Notifications of one type, for the startup reconcilers (spec 6.10).</summary>
    /// <param name="type">The source type.</param>
    /// <returns>The unresolved Notifications, newest first.</returns>
    public IReadOnlyList<FeedItem> Unresolved(FeedSourceType type)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM feed_events WHERE notify = 1 AND resolved_at IS NULL AND source_type = $type ORDER BY id DESC;";
        _ = command.Parameters.AddWithValue("$type", FeedSourceTypes.ToWire(type));
        return ReadItems(command);
    }

    /// <summary>Up to <paramref name="limit"/> items about the item's target or the extra targets, newest first, the item itself included (spec 6.13).</summary>
    /// <param name="item">The item whose neighbours are wanted.</param>
    /// <param name="extraTargets">More target keys to include (its origin Room, the files it links).</param>
    /// <param name="limit">The most to return.</param>
    /// <returns>The related items.</returns>
    public IReadOnlyList<FeedItem> Related(FeedItem item, IReadOnlyList<string> extraTargets, int limit)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        List<string> targets = [item.TargetKey, .. extraTargets];
        string[] names = [.. targets.Select(static (_, index) => "$t" + index.ToString(CultureInfo.InvariantCulture))];
        for (int i = 0; i < targets.Count; i++)
        {
            _ = command.Parameters.AddWithValue(names[i], targets[i]);
        }

        _ = command.Parameters.AddWithValue("$limit", Text(limit));
        StringBuilder sql = new("SELECT ");
        _ = sql.Append(Columns).Append(" FROM feed_events WHERE target_key IN (").AppendJoin(", ", names).Append(") ORDER BY id DESC LIMIT CAST($limit AS INTEGER);");
        command.CommandText = sql.ToString();
        return ReadItems(command);
    }

    /// <summary>The Feed Tab's seen mark (<c>feed_state</c>), or 0 before the first view.</summary>
    /// <returns>The highest Feed item id the Tab has shown.</returns>
    public long TabSeenUpTo()
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM feed_state WHERE key = $key;";
        _ = command.Parameters.AddWithValue("$key", TabSeenKey);
        return command.ExecuteScalar() is string text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) ? id : 0;
    }

    /// <summary>The number on each Category segment under the Agent and Team filters, in one query.</summary>
    /// <param name="agent">Only items by this actor, or <see langword="null"/>.</param>
    /// <param name="team">Only items about this Team, or <see langword="null"/>.</param>
    /// <returns>The seven counts.</returns>
    public FeedCounts Counts(string? agent, string? team)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteCommand command = connection.CreateCommand();
        StringBuilder sql = new("SELECT ");
        for (int i = 0; i < FeedCategories.Order.Count; i++)
        {
            _ = sql.Append(i == 0 ? string.Empty : ", ")
                .Append("COALESCE(SUM(CASE WHEN ").Append(CategoryCondition(FeedCategories.Order[i], command)).Append(" THEN 1 ELSE 0 END), 0)");
        }

        _ = sql.Append(" FROM feed_events WHERE 1 = 1");
        AppendActorAndTeam(sql, command, agent, team);
        command.CommandText = sql.ToString();

        using SqliteDataReader reader = command.ExecuteReader();
        _ = reader.Read();
        return new FeedCounts(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8));
    }

    /// <summary>Applies a batch of commands in order, in one transaction. Called only by <see cref="FeedWriter"/>.</summary>
    /// <param name="commands">The commands, in queue order.</param>
    /// <returns>The inserted rows, and whether an update touched a row.</returns>
    internal FeedApplyResult Apply(IReadOnlyList<FeedCommand> commands)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        List<FeedItem> added = [];
        bool changed = false;
        foreach (FeedCommand command in commands)
        {
            if (command is PostCommand post)
            {
                added.Add(Insert(connection, transaction, post));
            }
            else
            {
                changed |= Update(connection, transaction, command) > 0;
            }
        }

        transaction.Commit();
        return new FeedApplyResult(added, changed);
    }

    /// <summary>Deletes what the retention windows have expired, in its own transaction. Called only by <see cref="FeedWriter"/>.</summary>
    /// <param name="dismissedCutoff">Dismissed Notifications dismissed before this go; <see langword="null"/> keeps them all.</param>
    /// <param name="activityCutoff">Activity items created before this go; <see langword="null"/> keeps them all.</param>
    /// <returns>How many rows were deleted.</returns>
    internal int DeleteExpired(DateTimeOffset? dismissedCutoff, DateTimeOffset? activityCutoff)
    {
        using SqliteConnection connection = SqliteTable.Connect(this.connectionString);
        using SqliteTransaction transaction = connection.BeginTransaction();
        int deleted = 0;
        if (dismissedCutoff is { } dismissed)
        {
            deleted += Run(connection, transaction, "DELETE FROM feed_events WHERE notify = 1 AND dismissed_at IS NOT NULL AND dismissed_at < $cutoff;", ("$cutoff", FormatTime(dismissed)));
        }

        if (activityCutoff is { } activity)
        {
            deleted += Run(connection, transaction, "DELETE FROM feed_events WHERE notify = 0 AND created_at < $cutoff;", ("$cutoff", FormatTime(activity)));
        }

        transaction.Commit();
        return deleted;
    }

    private static FeedItem Insert(SqliteConnection connection, SqliteTransaction transaction, PostCommand post)
    {
        FeedMessage message = post.Message;
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO feed_events(created_at, source_type, target_key, actor, notify, payload)
            VALUES($at, $type, $target, $actor, CAST($notify AS INTEGER), $payload)
            RETURNING id;
            """;
        _ = command.Parameters.AddWithValue("$at", FormatTime(post.At));
        _ = command.Parameters.AddWithValue("$type", FeedSourceTypes.ToWire(message.SourceType));
        _ = command.Parameters.AddWithValue("$target", message.TargetKey);
        _ = command.Parameters.AddWithValue("$actor", message.Actor.Name);
        _ = command.Parameters.AddWithValue("$notify", message.Notify ? "1" : "0");
        _ = command.Parameters.AddWithValue("$payload", FeedPayloads.Serialize(message.Actor.Kind, message.Payload));
        long id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        return new FeedItem(id, post.At.ToUniversalTime(), message.SourceType, message.TargetKey, message.Actor, message.Notify, null, null, null, message.Payload);
    }

    private static int Update(SqliteConnection connection, SqliteTransaction transaction, FeedCommand command) => command switch
    {
        ResolveCommand resolve => Run(
            connection,
            transaction,
            "UPDATE feed_events SET resolved_at = $at WHERE target_key = $key AND source_type = $type AND notify = 1 AND resolved_at IS NULL;",
            ("$at", FormatTime(resolve.At)),
            ("$key", resolve.TargetKey),
            ("$type", FeedSourceTypes.ToWire(resolve.Type))),
        MarkSeenCommand seen => Run(
            connection,
            transaction,
            "UPDATE feed_events SET seen_at = $at WHERE id <= CAST($upTo AS INTEGER) AND notify = 1 AND seen_at IS NULL;",
            ("$at", FormatTime(seen.At)),
            ("$upTo", Text(seen.UpToId))),
        DismissCommand dismiss => Run(
            connection,
            transaction,
            "UPDATE feed_events SET dismissed_at = $at WHERE id = CAST($id AS INTEGER) AND notify = 1 AND dismissed_at IS NULL;",
            ("$at", FormatTime(dismiss.At)),
            ("$id", Text(dismiss.Id))),
        MarkTabSeenCommand tab => MarkTabSeen(connection, transaction, tab),
        _ => 0,
    };

    private static int MarkTabSeen(SqliteConnection connection, SqliteTransaction transaction, MarkTabSeenCommand tab)
    {
        _ = Run(
            connection,
            transaction,
            """
            INSERT INTO feed_state(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            WHERE CAST(excluded.value AS INTEGER) > CAST(feed_state.value AS INTEGER);
            """,
            ("$key", TabSeenKey),
            ("$value", Text(tab.UpToId)));

        // The Tab's own mark is not a change to any item, so it raises no ItemsChanged.
        return 0;
    }

    private static int Run(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            _ = command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteNonQuery();
    }

    private void AppendActorAndTeam(StringBuilder sql, SqliteCommand command, string? agent, string? team)
    {
        if (agent is not null)
        {
            _ = sql.Append(" AND actor = $agent COLLATE NOCASE");
            _ = command.Parameters.AddWithValue("$agent", agent);
        }

        if (team is not null)
        {
            List<string> names = [team];
            if (this.renames is not null)
            {
                string current = this.renames.Resolve(team);
                names.Add(current);
                names.AddRange(this.renames.EarlierNames(current));
            }

            string[] distinct = [.. names.Distinct(StringComparer.OrdinalIgnoreCase)];
            _ = sql.Append(" AND (");
            for (int i = 0; i < distinct.Length; i++)
            {
                string parameter = string.Create(CultureInfo.InvariantCulture, $"$team{i}");
                _ = sql.Append(i == 0 ? string.Empty : " OR ").Append(TeamExpression).Append(" = ").Append(parameter).Append(" COLLATE NOCASE");
                _ = command.Parameters.AddWithValue(parameter, distinct[i]);
            }

            _ = sql.Append(')');
        }
    }

    private static string CategoryCondition(FeedCategory category, SqliteCommand command)
    {
        if (category == FeedCategory.All)
        {
            return "1 = 1";
        }

        if (category == FeedCategory.NeedsYou)
        {
            return "(notify = 1 AND resolved_at IS NULL)";
        }

        IReadOnlyList<FeedSourceType> types = FeedCategories.SourceTypes(category);
        string[] names = new string[types.Count];
        for (int i = 0; i < types.Count; i++)
        {
            names[i] = string.Create(CultureInfo.InvariantCulture, $"$c{(int)category}_{i}");
            _ = command.Parameters.AddWithValue(names[i], FeedSourceTypes.ToWire(types[i]));
        }

        return $"source_type IN ({string.Join(", ", names)})";
    }

    private static List<FeedItem> ReadItems(SqliteCommand command)
    {
        List<FeedItem> items = [];
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadItem(reader));
        }

        return items;
    }

    private static FeedItem ReadItem(SqliteDataReader reader)
    {
        FeedSourceType? sourceType = FeedSourceTypes.TryParse(reader.GetString(2), out FeedSourceType parsed) ? parsed : null;
        string actorName = reader.GetString(4);
        FeedEnvelope envelope = FeedPayloads.Read(reader.GetString(9), sourceType);
        FeedActorKind kind = envelope.ActorKind ?? (string.Equals(actorName, FeedActor.Huddle.Name, StringComparison.Ordinal) ? FeedActorKind.Huddle : FeedActorKind.Agent);
        return new FeedItem(
            reader.GetInt64(0),
            ParseTime(reader.GetString(1)),
            sourceType,
            reader.GetString(3),
            new FeedActor(kind, actorName),
            reader.GetInt32(5) != 0,
            reader.IsDBNull(6) ? null : ParseTime(reader.GetString(6)),
            reader.IsDBNull(7) ? null : ParseTime(reader.GetString(7)),
            reader.IsDBNull(8) ? null : ParseTime(reader.GetString(8)),
            envelope.Payload);
    }

    /// <summary>A number as text. Bound as text and read back with <c>CAST(... AS INTEGER)</c>, which boxes nothing.</summary>
    private static string Text(long number) => number.ToString(CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
